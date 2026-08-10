using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace WeddingWitchArchipelago;

/// Routes GameData.es3 through the room's own store while a session is live — see
/// ApProfile for why that file and not the others.
///
/// Everything that reads an upgrade level does it the same way: GlobalStat
/// .ApplyUpgrade, PlayerMagnet, EnchantManager, RoadMapCanvas and UpgradeItem all
/// call ES3.Load&lt;int&gt;(loadKey, "GameData.es3"). Intercepting there is what turns a
/// received Archipelago item into a level, with no per-consumer patching.
///
/// The overloads taking a file name are the ones patched, because every call site in
/// the game names its file explicitly. They are generic, so they have to be closed
/// over the type actually used and patched by hand rather than by attribute.
public static class SaveDataPatch
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(typeof(ES3), nameof(ES3.KeyExists),
                               new[] { typeof(string), typeof(string) }),
            prefix: Hook(nameof(KeyExistsPrefix)));

        harmony.Patch(LoadMethod(typeof(int)), prefix: Hook(nameof(LoadIntPrefix)));
        harmony.Patch(SaveMethod(typeof(int)), prefix: Hook(nameof(SaveIntPrefix)));

        harmony.Patch(AccessTools.Method(typeof(GameDataManager.GameData), "Load"),
                      prefix: Hook(nameof(GameDataLoadPrefix)));
    }

    /// Re-reads everything that cached a value from the file we just swapped out
    /// from under it.
    public static void ReloadGameData()
    {
        GameDataManager.ReloadAll();
        ShopPatch.RefreshOpenShop();
        DifficultyPatch.Apply();
        MenuSplit.InstallNow();
    }

    // ---- ES3 ------------------------------------------------------------

    private static bool KeyExistsPrefix(string key, string filePath, ref bool __result)
    {
        if (!Diverted(filePath)) return true;
        __result = ApProfile.TryGet(filePath, key, out _);
        return false;
    }

    private static bool LoadIntPrefix(string key, string filePath, ref int __result)
    {
        if (!Diverted(filePath)) return true;
        __result = ApProfile.TryGet(filePath, key, out var stored)
                   && int.TryParse(stored, out var value)
            ? value
            : 0;
        return false;
    }

    private static bool SaveIntPrefix(string key, int value, string filePath)
    {
        if (!Diverted(filePath)) return true;
        ApProfile.Set(filePath, key, value.ToString());
        return false;
    }

    private static bool Diverted(string filePath) =>
        ApProfile.Active && ApProfile.Owns(filePath);

    // ---- lifetime counters ----------------------------------------------

    /// GameDataManager.GameData.Load leaves its count untouched when the key is
    /// missing, so a room that has never seen a counter would keep whatever the
    /// player's own save had and then write that into the room's store.
    private static bool GameDataLoadPrefix(GameDataManager.GameData __instance)
    {
        if (!ApProfile.Active) return true;

        var fields = Traverse.Create(__instance);
        var name = fields.Field<string>("_name").Value;
        fields.Field<int>("_count").Value =
            ApProfile.TryGet(ApProfile.GameDataFile, name, out var stored)
            && int.TryParse(stored, out var value)
                ? value
                : 0;
        return false;
    }

    // ---- reflection helpers ---------------------------------------------

    private static HarmonyMethod Hook(string name) =>
        new HarmonyMethod(AccessTools.Method(typeof(SaveDataPatch), name));

    /// ES3.Load&lt;T&gt;(string key, string filePath). Told apart from
    /// Load&lt;T&gt;(string key, T defaultValue) by its second parameter.
    private static MethodInfo LoadMethod(Type type) =>
        Generic(nameof(ES3.Load), type,
                parameters => parameters.Length == 2
                              && parameters[1].ParameterType == typeof(string));

    /// ES3.Save&lt;T&gt;(string key, T value, string filePath). Told apart from
    /// Save&lt;T&gt;(string key, T value, ES3Settings settings) by its third parameter.
    private static MethodInfo SaveMethod(Type type) =>
        Generic(nameof(ES3.Save), type,
                parameters => parameters.Length == 3
                              && parameters[2].ParameterType == typeof(string));

    /// AccessTools.Method matches parameters against the open definition, where the
    /// value parameter is still T — so the overload has to be picked by shape and
    /// closed afterwards.
    private static MethodInfo Generic(string name, Type type,
                                      Func<ParameterInfo[], bool> matches)
    {
        var definition = typeof(ES3)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method => method.Name == name
                                      && method.IsGenericMethodDefinition
                                      && method.GetParameters()[0].ParameterType == typeof(string)
                                      && matches(method.GetParameters()));

        if (definition == null)
            throw new MissingMethodException($"ES3.{name}<T> with a file-name overload is gone");

        return definition.MakeGenericMethod(type);
    }
}
