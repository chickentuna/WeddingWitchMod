using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;

namespace WeddingWitchArchipelago.Archipelago;

/// Owns the Archipelago session. Everything the game patches touch goes through
/// ApState, which forwards here — so the patches never see the network layer.
public class ArchipelagoClient
{
    public const string GameName = "Wedding Witch";

    private ArchipelagoSession session;

    public bool Connected => session?.Socket?.Connected == true;

    public SlotSettings Settings { get; private set; } = SlotSettings.Defaults();

    public string Connect(string host, int port, string slot, string password)
    {
        Disconnect();

        try
        {
            session = ArchipelagoSessionFactory.CreateSession(host, port);

            // AllItems rather than RemoteItems: no upgrade is granted locally, so
            // every level — including ones this slot placed itself — has to arrive
            // through the session.
            var result = session.TryConnectAndLogin(
                GameName, slot, ItemsHandlingFlags.AllItems,
                password: string.IsNullOrEmpty(password) ? null : password);

            if (result is LoginFailure failure)
            {
                var reason = string.Join("; ", failure.Errors);
                session = null;
                return string.IsNullOrEmpty(reason) ? "Connection refused" : reason;
            }

            var success = (LoginSuccessful)result;
            Settings = SlotSettings.FromSlotData(success.SlotData);

            Plugin.Logger.LogInfo(
                $"Connected to {host}:{port} as {slot} — goal {Settings.GoalDifficulty} in {Settings.GoalForms} form(s)");

            // Before anything reads or writes progress, move the save file the
            // multiworld owns into this room's own store.
            ApProfile.Enter(session.RoomState?.Seed, slot);

            // The server is authoritative on what has already been done, and this
            // has to happen before the item stream is attached: subscribing first
            // lets the replay land on a network thread while AdoptSession is still
            // clearing levels on this one.
            ApState.AdoptSession(this, CheckedLocationNames());

            session.Items.ItemReceived += OnItemReceived;
            session.Socket.SocketClosed += OnSocketClosed;

            // Drain anything the helper queued before we subscribed.
            OnItemReceived(session.Items);

            ScoutRevealedLocations();
            ApState.RequestReload();
            return null;
        }
        catch (Exception ex)
        {
            session = null;
            return ex.Message;
        }
    }

    /// The server tracks checks by id; everything above this layer works in names.
    private IEnumerable<string> CheckedLocationNames()
    {
        foreach (var id in session.Locations.AllLocationsChecked)
        {
            var name = session.Locations.GetLocationNameFromId(id, GameName);
            if (!string.IsNullOrEmpty(name)) yield return name;
        }
    }

    public void Disconnect()
    {
        if (session == null) return;
        try
        {
            session.Items.ItemReceived -= OnItemReceived;
            session.Socket.SocketClosed -= OnSocketClosed;
            session.Socket.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Error while disconnecting: {ex.Message}");
        }
        session = null;
        ApState.ReleaseSession();
        ApState.RequestReload();
    }

    // ---- outbound -------------------------------------------------------

    public void SendCheck(string locationName)
    {
        if (!Connected) return;
        var id = session.Locations.GetLocationIdFromName(GameName, locationName);
        if (id <= 0)
        {
            Plugin.Logger.LogError(
                $"Unknown location '{locationName}' — mod and apworld names have drifted");
            return;
        }
        session.Locations.CompleteLocationChecks(id);
    }

    public void SendGoal()
    {
        if (Connected) session.SetGoalAchieved();
    }

    /// One batch scout of every potion screen and shop shelf, so both can name what
    /// they are offering. HintCreationPolicy.None matters — scouting this many
    /// locations as hints would flood the room's hint list and spoil it for everyone.
    private void ScoutRevealedLocations()
    {
        var idToName = new Dictionary<long, string>();
        foreach (var name in Locations.AllScoutable(Settings.PotionSlotLocations,
                                                    Settings.ShopLocations))
        {
            var id = session.Locations.GetLocationIdFromName(GameName, name);
            if (id > 0) idToName[id] = name;
        }
        if (idToName.Count == 0) return;

        ScoutCache.Clear();
        var ids = new long[idToName.Count];
        idToName.Keys.CopyTo(ids, 0);

        session.Locations
            .ScoutLocationsAsync(HintCreationPolicy.None, ids)
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.Result == null)
                {
                    Plugin.Logger.LogWarning(
                        $"Scout failed: {task.Exception?.GetBaseException().Message}");
                    return;
                }

                foreach (var pair in task.Result)
                {
                    if (!idToName.TryGetValue(pair.Key, out var location)) continue;
                    var info = pair.Value;
                    ScoutCache.Add(location, info.ItemDisplayName, info.Player?.Alias, info.Flags);
                }

                Plugin.Logger.LogInfo($"Scouted {ScoutCache.Count} revealed locations");
            });
    }

    // ---- inbound --------------------------------------------------------

    private static void OnItemReceived(IReceivedItemsHelper helper)
    {
        while (helper.Any()) ApState.ReceiveItem(helper.DequeueItem().ItemName);
    }

    private static void OnSocketClosed(string reason)
    {
        Plugin.Logger.LogWarning($"Archipelago connection closed: {reason}");
        ApState.ReleaseSession();
        ApState.RequestReload();
    }
}

/// Per-seed settings from the world's fill_slot_data.
public class SlotSettings
{
    public string GoalDifficulty;

    /// How many different witch forms must clear the goal difficulty's boss.
    public int GoalForms;

    /// How many shelves the hijacked shop stocks.
    public int ShopLocations;

    /// Whether each of a potion screen's three slots is its own check.
    public bool PotionSlotLocations;

    public static SlotSettings Defaults() => new SlotSettings
    {
        GoalDifficulty = "Nightmare",
        GoalForms = 6,
        ShopLocations = 30,
        PotionSlotLocations = false,
    };

    public static SlotSettings FromSlotData(Dictionary<string, object> slotData)
    {
        var settings = Defaults();
        if (slotData == null) return settings;

        if (slotData.TryGetValue("goal_difficulty", out var goal))
            settings.GoalDifficulty = Convert.ToString(goal);
        if (slotData.TryGetValue("goal_forms", out var forms))
            settings.GoalForms = Convert.ToInt32(forms);
        if (slotData.TryGetValue("shop_locations", out var shop))
            settings.ShopLocations = Convert.ToInt32(shop);
        if (slotData.TryGetValue("potion_slot_locations", out var slots))
            settings.PotionSlotLocations = Convert.ToBoolean(slots);

        return settings;
    }
}
