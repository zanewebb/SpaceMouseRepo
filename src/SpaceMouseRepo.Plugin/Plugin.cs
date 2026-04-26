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
    public const string VERSION = "0.2.0";

    private SpaceMouseSdk? _sdk;
    private Harmony? _harmony;

    private void Awake()
    {
        Logger.LogInfo($"{NAME} v{VERSION} loading…");
        var harmonyAsm = typeof(Harmony).Assembly.GetName();
        Logger.LogInfo($"HarmonyX runtime version: {harmonyAsm.Version} ({harmonyAsm.FullName})");

        var pcfg = new PluginConfig(Config);

        _sdk = new SpaceMouseSdk(Logger, pcfg.TranslationDeadzone, pcfg.RotationDeadzone);

        _harmony = new Harmony(GUID);
        GrabPatches.Install(_harmony, Logger, pcfg.Manipulation, () => _sdk?.State ?? Core.Input.SpaceMouseState.Empty);

        Logger.LogInfo($"{NAME} ready. SDK active: {_sdk.IsActive}");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        _sdk?.Dispose();
    }
}
