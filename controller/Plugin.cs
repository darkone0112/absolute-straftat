using System;
using BepInEx;
using BepInEx.Configuration;

namespace AbsoluteStraftat.Controller;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "darkone.absolute-straftat.controller";
    public const string PluginName = "Absolute STRAFTAT Controller";
    public const string PluginVersion = "0.1.0";

    private ConfigEntry<bool>? controllerEnabled;
    private ConfigEntry<bool>? loudTestMode;
    private ConfigEntry<int>? uiScalePercent;
    private ConfigEntry<int>? spawnBudget;
    private ConfigEntry<ControlledMod>? focusMod;
    private ConfigEntry<string>? testNote;

    private void Awake()
    {
        controllerEnabled = Config.Bind(
            "Controller",
            "Enabled",
            true,
            "Master switch for the Absolute STRAFTAT in-game controller.");

        loudTestMode = Config.Bind(
            "Controller",
            "LoudTestMode",
            false,
            "Testing switch rendered by Mod Menu. Currently logs changes and proves live config writes.");

        uiScalePercent = Config.Bind(
            "Controller",
            "UiScalePercent",
            100,
            new ConfigDescription(
                "Test slider for UI-ish values. This is a safe controller proof, not a STRAFTAT engine patch.",
                new AcceptableValueRange<int>(75, 150)));

        spawnBudget = Config.Bind(
            "Controller",
            "SpawnBudget",
            8,
            new ConfigDescription(
                "Test slider for spawn-ish values so we can validate Mod Menu numeric control.",
                new AcceptableValueRange<int>(0, 64)));

        focusMod = Config.Bind(
            "Controller",
            "FocusMod",
            ControlledMod.MoreStrafts,
            "Which installed mod this controller is pretending to inspect while we wire up real adapters.");

        testNote = Config.Bind(
            "Controller",
            "TestNote",
            "committee approved",
            "Short writable note. If this changes from inside Mod Menu, the bridge is alive.");

        controllerEnabled.SettingChanged += OnSettingChanged;
        loudTestMode.SettingChanged += OnSettingChanged;
        uiScalePercent.SettingChanged += OnSettingChanged;
        spawnBudget.SettingChanged += OnSettingChanged;
        focusMod.SettingChanged += OnSettingChanged;
        testNote.SettingChanged += OnSettingChanged;

        Logger.LogInfo("Absolute STRAFTAT Controller loaded. Open Mod Menu to edit its test controls.");
    }

    private void OnSettingChanged(object sender, EventArgs eventArgs)
    {
        if (controllerEnabled?.Value != true)
        {
            return;
        }

        Logger.LogInfo(
            $"Controller changed: focus={focusMod?.Value}, loud={loudTestMode?.Value}, uiScale={uiScalePercent?.Value}, spawnBudget={spawnBudget?.Value}, note={testNote?.Value}");
    }
}

public enum ControlledMod
{
    MoreStrafts,
    GunGame,
    UiSpawnAddon,
    Fancy,
    CosmeticsBundle
}
