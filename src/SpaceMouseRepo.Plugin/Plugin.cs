using System;
using System.IO;
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
    public const string VERSION = "0.3.3";

    // Heartbeat diagnostic: writes both to BepInEx logger AND directly to a file in the user's
    // BepInEx config dir. If the file gets heartbeats but BepInEx's LogOutput.log doesn't, then
    // BepInEx's per-frame logging is being throttled/suppressed during gameplay. If the file is
    // empty too, the plugin's MonoBehaviour Update isn't running at all.
    private static StreamWriter? _diagFile;
    private float _heartbeatTimer;
    private int _heartbeatCount;

    private SpaceMouseSdk? _sdk;
    private Harmony? _harmony;

    private void Awake()
    {
        // Open a side-channel diag log next to BepInEx's LogOutput.log.
        try
        {
            var dir = Path.Combine(Paths.BepInExRootPath ?? Paths.ConfigPath, "..");
            var path = Path.Combine(dir, "LogOutput.SpaceMouseRepo.log");
            _diagFile = new StreamWriter(path, append: false) { AutoFlush = true };
            _diagFile.WriteLine($"=== SpaceMouseRepo v{VERSION} side-channel diag log opened at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            Logger.LogInfo($"Side-channel diag log: {Path.GetFullPath(path)}");
        }
        catch (Exception e)
        {
            Logger.LogWarning($"Could not open side-channel diag log: {e.Message}");
        }

        Logger.LogInfo($"{NAME} v{VERSION} loading…");
        var harmonyAsm = typeof(Harmony).Assembly.GetName();
        Logger.LogInfo($"HarmonyX runtime version: {harmonyAsm.Version} ({harmonyAsm.FullName})");

        var pcfg = new PluginConfig(Config);

        _sdk = new SpaceMouseSdk(Logger, pcfg.TranslationDeadzone, pcfg.RotationDeadzone);

        _harmony = new Harmony(GUID);
        GrabPatches.Install(_harmony, Logger, pcfg.Manipulation, () => _sdk?.State ?? Core.Input.SpaceMouseState.Empty);

        // Hand the diag file to GrabPatches so the patch can write through it directly,
        // bypassing BepInEx's logger if it turns out to be the suppressed channel.
        GrabPatches.SideChannel = _diagFile;

        Logger.LogInfo($"{NAME} ready. SDK active: {_sdk.IsActive}");
        WriteSideChannel($"Awake complete. SDK active: {_sdk.IsActive}");
    }

    private void Update()
    {
        _heartbeatTimer += UnityEngine.Time.unscaledDeltaTime;
        if (_heartbeatTimer < 5f) return;
        _heartbeatTimer = 0f;
        _heartbeatCount++;
        Logger.LogInfo($"[heartbeat] Plugin.Update tick #{_heartbeatCount} at t={UnityEngine.Time.unscaledTime:F1}s");
        WriteSideChannel($"[heartbeat] Plugin.Update tick #{_heartbeatCount} at t={UnityEngine.Time.unscaledTime:F1}s");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        _sdk?.Dispose();
        _diagFile?.Dispose();
    }

    internal static void WriteSideChannel(string line)
    {
        try { _diagFile?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {line}"); } catch { /* ignore */ }
    }
}
