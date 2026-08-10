using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;
using WeddingWitchArchipelago.Archipelago;

namespace WeddingWitchArchipelago;

[BepInPlugin(GUID, NAME, VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string GUID = "com.jpn.weddingwitch.archipelago";
    public const string NAME = "Wedding Witch Archipelago";
    public const string VERSION = "0.1.0";

    public static Plugin Instance { get; private set; }

    public static new BepInEx.Logging.ManualLogSource Logger { get; private set; }

    public static ArchipelagoClient Client { get; private set; }

    public static ConfigEntry<string> Host { get; private set; }
    public static ConfigEntry<int> Port { get; private set; }
    public static ConfigEntry<string> SlotName { get; private set; }
    public static ConfigEntry<string> Password { get; private set; }
    public static ConfigEntry<bool> ConnectOnStart { get; private set; }
    public static ConfigEntry<KeyCode> ConnectionUIKey { get; private set; }

    public static ConfigEntry<bool> EnableCheats { get; private set; }
    public static ConfigEntry<KeyCode> WinMissionKey { get; private set; }

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;

        Host = Config.Bind("Connection", "Host", "archipelago.gg", "Server address.");
        Port = Config.Bind("Connection", "Port", 38281, "Server port.");
        SlotName = Config.Bind("Connection", "SlotName", "", "Your slot name in the room.");
        Password = Config.Bind("Connection", "Password", "", "Room password, if the room has one.");
        ConnectOnStart = Config.Bind("Connection", "ConnectOnStart", false,
            "Connect automatically on launch using the details below, skipping the panel.");
        ConnectionUIKey = Config.Bind("Connection", "ConnectionUIKey", KeyCode.F1,
            "Opens and closes the Archipelago connection panel.");

        EnableCheats = Config.Bind("Cheats", "EnableCheats", false,
            "Enables the testing keys below. Leave this off for a seed you mean to "
            + "play — they can empty a multiworld's locations in a minute.");
        WinMissionKey = Config.Bind("Cheats", "WinMissionKey", KeyCode.F9,
            "Wins the mission you are in, sending its checks as a real clear would.");

        Logger.LogInfo($"{NAME} {VERSION} loading");
        Logger.LogInfo($"Unity {Application.unityVersion}, game {Application.productName} {Application.version}");

        Client = new ArchipelagoClient();
        gameObject.AddComponent<ConnectionUI>();
        gameObject.AddComponent<DebugCheats>();

        var harmony = new Harmony(GUID);
        harmony.PatchAll(typeof(Plugin).Assembly);
        // ES3's readers and writers are generic, so they cannot be reached by
        // attribute and are closed over their types by hand.
        SaveDataPatch.Apply(harmony);
        Logger.LogInfo($"Harmony applied {harmony.GetPatchedMethods().Count()} patches");

        if (ConnectOnStart.Value) Connect();
    }

    public static void Connect()
    {
        if (string.IsNullOrWhiteSpace(SlotName.Value))
        {
            Logger.LogError("Cannot connect: SlotName is empty in the config.");
            return;
        }

        var error = Client.Connect(Host.Value, Port.Value, SlotName.Value, Password.Value);
        if (error != null) Logger.LogError($"Archipelago connection failed: {error}");
    }

    /// Items and socket closures land on a network thread; re-reading the save and
    /// redrawing the shop has to happen here instead.
    private void Update()
    {
        if (ApState.TakeReloadRequest()) SaveDataPatch.ReloadGameData();
    }

    private void OnDestroy() => Client?.Disconnect();
}
