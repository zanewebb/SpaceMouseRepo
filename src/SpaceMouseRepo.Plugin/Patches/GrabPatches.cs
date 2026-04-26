using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using HarmonyLib;
using SpaceMouseRepo.Core.Behavior;
using SpaceMouseRepo.Core.Input;
using SNVector3 = System.Numerics.Vector3;
using SNQuaternion = System.Numerics.Quaternion;
using UVector3 = UnityEngine.Vector3;
using UQuaternion = UnityEngine.Quaternion;

namespace SpaceMouseRepo.Patches;

public static class GrabPatches
{
    private static ManualLogSource _log = null!;
    private static ManipulationConfig _cfg = null!;
    private static Func<SpaceMouseState> _readState = () => SpaceMouseState.Empty;
    private static bool _disabled;
    private static readonly ConditionalWeakTable<object, HeldObjectController> _byHolder = new();

    private static FieldInfo? _isLocalField;
    private static FieldInfo? _grabbedField;
    private static FieldInfo? _targetRotField;
    private static FieldInfo? _targetPosField;

    public static void Install(Harmony harmony, ManualLogSource log, ManipulationConfig cfg, Func<SpaceMouseState> readState)
    {
        _log = log;
        _cfg = cfg;
        _readState = readState;

        var grabberType = AccessTools.TypeByName("PhysGrabber");
        if (grabberType == null)
        {
            _log.LogError("Type PhysGrabber not found. SpaceMouse mod inactive. Report this with the BepInEx log.");
            _disabled = true;
            return;
        }

        var update = AccessTools.Method(grabberType, "Update")
                  ?? AccessTools.Method(grabberType, "LateUpdate");
        if (update == null)
        {
            _log.LogError("PhysGrabber.Update / LateUpdate not found. SpaceMouse mod inactive.");
            _disabled = true;
            return;
        }

        _isLocalField  = AccessTools.Field(grabberType, "isLocal");
        _grabbedField  = AccessTools.Field(grabberType, "grabbed");
        if (_isLocalField == null || _grabbedField == null)
        {
            _log.LogError($"PhysGrabber field discovery failed: isLocal={_isLocalField != null} grabbed={_grabbedField != null}. Plugin inactive.");
            _disabled = true;
            return;
        }

        var postfix = new HarmonyMethod(typeof(GrabPatches).GetMethod(nameof(PostUpdate), BindingFlags.Static | BindingFlags.NonPublic));
        harmony.Patch(update, postfix: postfix);
        _log.LogInfo($"Patched {grabberType.FullName}.{update.Name}");
    }

    private static void PostUpdate(object __instance)
    {
        if (_disabled) return;
        try
        {
            if (_isLocalField!.GetValue(__instance) is not bool isLocal || !isLocal) return;
            var grabbed = _grabbedField!.GetValue(__instance);
            var ctrl = _byHolder.GetValue(__instance, _ => new HeldObjectController(_cfg));

            if (grabbed == null)
            {
                ctrl.OnRelease();
                return;
            }

            EnsureTargetFields(grabbed);
            if (_targetRotField == null || _targetPosField == null) return;

            ctrl.Apply(_readState(), UnityEngine.Time.deltaTime);

            var rot = (UQuaternion)_targetRotField.GetValue(grabbed)!;
            var pos = (UVector3)_targetPosField.GetValue(grabbed)!;

            var addRot = ToUnity(ctrl.AccumulatedRotation);
            var addPos = ToUnity(ctrl.AccumulatedOffset);

            _targetRotField.SetValue(grabbed, addRot * rot);
            _targetPosField.SetValue(grabbed, pos + addPos);
        }
        catch (Exception e)
        {
            _log.LogError($"GrabPatches.PostUpdate threw, disabling for session: {e}");
            _disabled = true;
        }
    }

    private static void EnsureTargetFields(object grabbed)
    {
        if (_targetRotField != null && _targetPosField != null) return;
        var t = grabbed.GetType();
        _targetRotField ??= AccessTools.Field(t, "targetRotation");
        _targetPosField ??= AccessTools.Field(t, "targetPosition");
        if (_targetRotField == null || _targetPosField == null)
            _log.LogError($"PhysGrabObject target fields not found on {t.FullName}. Looked for: targetRotation, targetPosition.");
    }

    private static UQuaternion ToUnity(SNQuaternion q) => new(q.X, q.Y, q.Z, q.W);
    private static UVector3 ToUnity(SNVector3 v) => new(v.X, v.Y, v.Z);
}
