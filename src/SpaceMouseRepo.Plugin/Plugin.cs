using BepInEx;
using HarmonyLib;
using SpaceMouseRepo.Config;
using SpaceMouseRepo.Input;
using SpaceMouseRepo.Patches;

namespace SpaceMouseRepo;

[BepInPlugin(GUID, NAME, VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string GUID = "com.zanewebb.spacemouse_repo";
    public const string NAME = "SpaceMouse for R.E.P.O.";
    public const string VERSION = "0.1.2";

    private SpaceMouseHid? _hid;
    private Harmony? _harmony;

    private void Awake()
    {
        Logger.LogInfo($"{NAME} v{VERSION} loading…");
        var pcfg = new PluginConfig(Config);

        _hid = new SpaceMouseHid(Logger, pcfg.ExtraProductIds, pcfg.TranslationDeadzone, pcfg.RotationDeadzone);

        _harmony = new Harmony(GUID);
        GrabPatches.Install(_harmony, Logger, pcfg.Manipulation, () => _hid?.State ?? Core.Input.SpaceMouseState.Empty);

        Logger.LogInfo($"{NAME} ready. HID active: {_hid.IsActive}");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        _hid?.Dispose();
    }
}
