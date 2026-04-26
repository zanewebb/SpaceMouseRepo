using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using BepInEx.Logging;
using HarmonyLib;
using SpaceMouseRepo.Core.Behavior;
using SpaceMouseRepo.Core.Input;
using SNQuaternion = System.Numerics.Quaternion;
using UQuaternion = UnityEngine.Quaternion;

namespace SpaceMouseRepo.Patches;

public static class GrabPatches
{
    private static ManualLogSource _log = null!;
    private static ManipulationConfig _cfg = null!;
    private static Func<SpaceMouseState> _readState = () => SpaceMouseState.Empty;
    private static bool _disabled;
    private static readonly ConditionalWeakTable<object, HeldObjectController> _byHolder = new();

    private static bool _diag_loggedFirstCall;
    private static bool _diag_loggedFirstNonIdentity;

    public static void Install(Harmony harmony, ManualLogSource log, ManipulationConfig cfg, Func<SpaceMouseState> readState)
    {
        _log = log;
        _cfg = cfg;
        _readState = readState;

        var grabberType = AccessTools.TypeByName("PhysGrabber");
        if (grabberType == null)
        {
            _log.LogError("Type PhysGrabber not found. SpaceMouse mod inactive.");
            _disabled = true;
            return;
        }

        DumpTypeShape("PhysGrabber", grabberType);

        // GetRotationInput() returns the per-frame rotation that vanilla applies to the held
        // object. Postfixing it with a Quaternion ref __result lets us multiply our SpaceMouse
        // delta directly into the rotation pipeline, without fighting any other system.
        var getRot = AccessTools.Method(grabberType, "GetRotationInput");
        if (getRot == null)
        {
            _log.LogError("PhysGrabber.GetRotationInput not found. SpaceMouse mod inactive.");
            _disabled = true;
            return;
        }

        var postfix = new HarmonyMethod(typeof(GrabPatches).GetMethod(nameof(PostGetRotationInput), BindingFlags.Static | BindingFlags.NonPublic));
        harmony.Patch(getRot, postfix: postfix);
        _log.LogInfo($"Patched {grabberType.FullName}.GetRotationInput");
    }

    private static void PostGetRotationInput(object __instance, ref UQuaternion __result)
    {
        // Diagnostic must come BEFORE _disabled check so we always know if the patch even fires.
        if (!_diag_loggedFirstCall)
        {
            _diag_loggedFirstCall = true;
            _log.LogInfo($"[diag] PostGetRotationInput first call: vanilla=({__result.x:F3},{__result.y:F3},{__result.z:F3},{__result.w:F3})");
        }

        if (_disabled) return;
        try
        {
            var ctrl = _byHolder.GetValue(__instance, _ => new HeldObjectController(_cfg));
            ctrl.Apply(_readState(), UnityEngine.Time.deltaTime);

            var ourDelta = ToUnity(ctrl.AccumulatedRotation);
            // Identity ⇒ no puck input; leave vanilla untouched.
            if (ourDelta == UQuaternion.identity) return;

            var combined = ourDelta * __result;

            if (!_diag_loggedFirstNonIdentity)
            {
                _diag_loggedFirstNonIdentity = true;
                _log.LogInfo($"[diag] first non-identity rotation injection: delta=({ourDelta.x:F3},{ourDelta.y:F3},{ourDelta.z:F3},{ourDelta.w:F3})");
            }

            __result = combined;
        }
        catch (Exception e) when (e is not ThreadAbortException)
        {
            _log.LogError($"GrabPatches.PostGetRotationInput threw, disabling for session: {e}");
            _disabled = true;
        }
    }

    private static void DumpTypeShape(string label, Type t)
    {
        const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var fields = t.GetFields(bf);
        var methods = t.GetMethods(bf);
        _log.LogInfo($"[diag] {label} = {t.FullName} | {fields.Length} fields, {methods.Length} methods");
        foreach (var m in methods)
        {
            if (m.DeclaringType == typeof(object) || m.DeclaringType?.Namespace == "UnityEngine") continue;
            _log.LogInfo($"[diag]   method {m.ReturnType.Name} {m.Name}({m.GetParameters().Length} args)");
        }
    }

    private static UQuaternion ToUnity(SNQuaternion q) => new(q.X, q.Y, q.Z, q.W);
}
