using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace WeddingWitchArchipelago;

/// A per-room stand-in for GameData.es3.
///
/// That file holds the upgrade levels, which in a seed are item state and belong to
/// the room rather than to the player: without this, every level the multiworld
/// granted would be written into the save they play unconnected, and their own
/// levels would show up in the room. Love Coins and the lifetime counters live in
/// the same file and come along with it.
///
/// AchivementState.es3 is diverted too: achievements pay out Love Coins, so a room
/// needs its own claimed/unclaimed states, counted against its own lifetime counters.
/// Steam is still told about every achievement the room completes.
///
/// Config.es3 — language, resolution, volume, key bindings — stays shared, since
/// none of it is progress.
public static class ApProfile
{
    public const string GameDataFile = "GameData.es3";
    public const string AchievementFile = "AchivementState.es3";

    /// The mod's own bookkeeping, kept in the same per-room store. Not a real ES3 file,
    /// so the interception in SaveDataPatch never sees it.
    public const string ModFile = "mod";

    private static readonly Dictionary<string, string> Values =
        new Dictionary<string, string>();

    private static string path;

    /// While false every ES3 call runs untouched, so playing unconnected leaves the
    /// ordinary save exactly as the game left it.
    public static bool Active { get; private set; }

    public static bool Owns(string file) => file == GameDataFile || file == AchievementFile;

    public static void Enter(string room, string slot)
    {
        var name = Sanitise(room) + "_" + Sanitise(slot) + ".txt";
        lock (Values)
        {
            path = Path.Combine(Paths.BepInExRootPath, "WeddingWitchArchipelago", name);
            Values.Clear();
            Active = true;
            Load();
            Plugin.Logger.LogInfo($"Save profile: {path} ({Values.Count} keys)");
        }
    }

    public static void Leave()
    {
        lock (Values)
        {
            Active = false;
            Values.Clear();
            path = null;
        }
    }

    // ---- the store ------------------------------------------------------
    //
    // Written from two threads: the game reads and writes it through ES3 on the main
    // one, while received items raise levels on the network one.

    public static bool TryGet(string file, string key, out string value)
    {
        lock (Values) return Values.TryGetValue(Compose(file, key), out value);
    }

    public static void Set(string file, string key, string value)
    {
        lock (Values)
        {
            Values[Compose(file, key)] = value;
            Save();
        }
    }

    public static void Remove(string file, string key)
    {
        lock (Values)
        {
            if (Values.Remove(Compose(file, key))) Save();
        }
    }

    private static string Compose(string file, string key) => file + "|" + key;

    // ---- persistence ----------------------------------------------------

    private static void Load()
    {
        if (!File.Exists(path)) return;
        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var split = line.IndexOf('=');
                if (split <= 0) continue;
                Values[line.Substring(0, split)] = line.Substring(split + 1);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Could not read save profile {path}: {ex.Message}");
        }
    }

    private static void Save()
    {
        if (path == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path,
                Values.OrderBy(pair => pair.Key)
                      .Select(pair => pair.Key + "=" + pair.Value)
                      .ToArray());
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Could not write save profile {path}: {ex.Message}");
        }
    }

    private static string Sanitise(string value)
    {
        if (string.IsNullOrEmpty(value)) return "unknown";
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
