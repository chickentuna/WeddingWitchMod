using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using WeddingWitchArchipelago.Archipelago;
using WeddingWitchArchipelago.Patches;
using WeddingWitchArchipelago.Utils;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace WeddingWitchArchipelago;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class Plugin : BasePlugin
{
    public const string PluginGUID = "com.poulton.weddingwitcharchipelago";
    public const string PluginName = "WeddingWitchArchipelago";
    public const string PluginVersion = "1.0.0";

    public const string ModDisplayInfo = $"{PluginName} v{PluginVersion}";
    private const string APDisplayInfo = $"Archipelago v{ArchipelagoClient.APVersion}";
    public static ManualLogSource BepinLogger;
    public static ArchipelagoClient ArchipelagoClient;

    public override void Load()
    {
        // Plugin startup logic
        BepinLogger = Log;
        ArchipelagoClient = new ArchipelagoClient();
        ArchipelagoConsole.Awake();

        // apply Harmony patches (upgrade-shop hooks live in WeddingWitchArchipelago.Patches)
        new Harmony(PluginGUID).PatchAll();

        ArchipelagoConsole.LogMessage($"{ModDisplayInfo} loaded!");
    }

    private void OnGUI()
    {
        // show the mod is currently loaded in the corner
        GUI.Label(new Rect(16, 16, 300, 20), ModDisplayInfo);
        ArchipelagoConsole.OnGUI();

        string statusMessage;
        // show the Archipelago Version and whether we're connected or not
        if (ArchipelagoClient.Authenticated)
        {
            // if your game doesn't usually show the cursor this line may be necessary
            // Cursor.visible = false;

            statusMessage = " Status: Connected";
            GUI.Label(new Rect(16, 50, 300, 20), APDisplayInfo + statusMessage);
        }
        else
        {
            // if your game doesn't usually show the cursor this line may be necessary
            // Cursor.visible = true;

            statusMessage = " Status: Disconnected";
            GUI.Label(new Rect(16, 50, 300, 20), APDisplayInfo + statusMessage);
            GUI.Label(new Rect(16, 70, 150, 20), "Host: ");
            GUI.Label(new Rect(16, 90, 150, 20), "Player Name: ");
            GUI.Label(new Rect(16, 110, 150, 20), "Password: ");

            ArchipelagoClient.ServerData.Uri = GUI.TextField(new Rect(150, 70, 150, 20),
                ArchipelagoClient.ServerData.Uri);
            ArchipelagoClient.ServerData.SlotName = GUI.TextField(new Rect(150, 90, 150, 20),
                ArchipelagoClient.ServerData.SlotName);
            ArchipelagoClient.ServerData.Password = GUI.TextField(new Rect(150, 110, 150, 20),
                ArchipelagoClient.ServerData.Password);

            // requires that the player at least puts *something* in the slot name
            if (GUI.Button(new Rect(16, 130, 100, 20), "Connect") &&
                !ArchipelagoClient.ServerData.SlotName.IsNullOrWhiteSpace())
            {
                ArchipelagoClient.Connect();
            }
        }
        // Debug: one-time capture of the upgrade catalog. Open the in-game upgrade shop, then click
        // this to log every upgrade's loadKey/nameKey/maxLevel (fill UpgradeCatalog from the log).
        if (GUI.Button(new Rect(16, 160, 130, 20), "Dump Upgrades"))
            UpgradeShopPatches.DumpCatalog();
    }
}