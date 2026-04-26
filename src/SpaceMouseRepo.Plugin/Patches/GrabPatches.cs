using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using BepInEx.Logging;
using HarmonyLib;
using SpaceMouseRepo.Core.Behavior;
using SpaceMouseRepo.Core.Input;
using SNVector3 = System.Numerics.Vector3;
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

    private static FieldInfo? _isLocalField;
    private static FieldInfo? _grabbedBoolField;       // PhysGrabber.grabbed: Boolean
    private static FieldInfo? _grabbedObjectRefField;  // PhysGrabber.grabbedPhysGrabObject: PhysGrabObject
    private static FieldInfo? _physRotationField;      // PhysGrabber.physRotation: Quaternion
    private static FieldInfo? _nextPhysRotationField;  // PhysGrabber.nextPhysRotation: Quaternion

    // One-shot diagnostic gates so we don't spam the log every frame.
    private static bool _diag_loggedFirstPostUpdate;
    private static bool _diag_loggedFirstIsLocalTrue;
    private static bool _diag_loggedFirstGrabbedTrue;
    private static bool _diag_loggedRotationWrite;
    private static int _diag_isLocalFalseCount;

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

        DumpTypeShape("PhysGrabber", grabberType);

        _isLocalField           = AccessTools.Field(grabberType, "isLocal");
        _grabbedBoolField       = AccessTools.Field(grabberType, "grabbed");
        _grabbedObjectRefField  = AccessTools.Field(grabberType, "grabbedPhysGrabObject");
        _physRotationField      = AccessTools.Field(grabberType, "physRotation");
        _nextPhysRotationField  = AccessTools.Field(grabberType, "nextPhysRotation");

        if (_isLocalField == null || _grabbedBoolField == null || _grabbedObjectRefField == null || _physRotationField == null)
        {
            _log.LogError($"PhysGrabber required-field discovery failed: " +
                $"isLocal={_isLocalField != null} grabbed={_grabbedBoolField != null} " +
                $"grabbedPhysGrabObject={_grabbedObjectRefField != null} physRotation={_physRotationField != null}. Plugin inactive.");
            _disabled = true;
            return;
        }

        // Patch every Update-flavored method that exists on PhysGrabber. Postfix runs after vanilla,
        // so writes to physRotation here are the last word before Unity's next physics tick.
        int patchedCount = 0;
        foreach (var name in new[] { "Update", "LateUpdate", "FixedUpdate" })
        {
            var m = AccessTools.Method(grabberType, name);
            if (m == null) continue;
            var postfix = new HarmonyMethod(typeof(GrabPatches).GetMethod(nameof(PostUpdate), BindingFlags.Static | BindingFlags.NonPublic));
            harmony.Patch(m, postfix: postfix);
            _log.LogInfo($"Patched {grabberType.FullName}.{name}");
            patchedCount++;
        }

        if (patchedCount == 0)
        {
            _log.LogError("No Update/LateUpdate/FixedUpdate found on PhysGrabber. Plugin inactive.");
            _disabled = true;
        }
    }

    private static void PostUpdate(object __instance)
    {
        if (_disabled) return;
        try
        {
            if (!_diag_loggedFirstPostUpdate)
            {
                _diag_loggedFirstPostUpdate = true;
                _log.LogInfo("[diag] PostUpdate first call");
            }

            if (_isLocalField!.GetValue(__instance) is not bool isLocal || !isLocal)
            {
                if (_diag_isLocalFalseCount < 3)
                {
                    _diag_isLocalFalseCount++;
                    _log.LogInfo($"[diag] isLocal=false on instance #{_diag_isLocalFalseCount} (count capped at 3)");
                }
                return;
            }

            if (!_diag_loggedFirstIsLocalTrue)
            {
                _diag_loggedFirstIsLocalTrue = true;
                _log.LogInfo("[diag] isLocal=true seen for first time");
            }

            // grabbed is a bool: are we currently holding something?
            if (_grabbedBoolField!.GetValue(__instance) is not bool grabbing || !grabbing)
            {
                _byHolder.GetValue(__instance, _ => new HeldObjectController(_cfg)).OnRelease();
                return;
            }

            if (!_diag_loggedFirstGrabbedTrue)
            {
                _diag_loggedFirstGrabbedTrue = true;
                var grabbedRef = _grabbedObjectRefField!.GetValue(__instance);
                if (grabbedRef != null) DumpTypeShape("grabbedPhysGrabObject runtime type", grabbedRef.GetType());
                else _log.LogInfo("[diag] grabbing=true but grabbedPhysGrabObject is null");
            }

            var ctrl = _byHolder.GetValue(__instance, _ => new HeldObjectController(_cfg));
            ctrl.Apply(_readState(), UnityEngine.Time.deltaTime);

            // Apply our accumulated rotation as a multiplicative delta on PhysGrabber.physRotation.
            // We compose: new = ourDelta * vanilla. PhysGrabber's downstream rotation steering reads
            // physRotation each frame, so this should propagate to the held object.
            var vanillaRot = (UQuaternion)_physRotationField!.GetValue(__instance)!;
            var ourDelta = ToUnity(ctrl.AccumulatedRotation);
            var combined = ourDelta * vanillaRot;
            _physRotationField.SetValue(__instance, combined);

            // Mirror to nextPhysRotation if present, in case vanilla derives from that.
            _nextPhysRotationField?.SetValue(__instance, combined);

            if (!_diag_loggedRotationWrite)
            {
                _diag_loggedRotationWrite = true;
                _log.LogInfo($"[diag] first rotation write: vanilla=({vanillaRot.x:F3},{vanillaRot.y:F3},{vanillaRot.z:F3},{vanillaRot.w:F3}) delta=({ourDelta.x:F3},{ourDelta.y:F3},{ourDelta.z:F3},{ourDelta.w:F3}) combined=({combined.x:F3},{combined.y:F3},{combined.z:F3},{combined.w:F3})");
            }
        }
        catch (Exception e) when (e is not ThreadAbortException)
        {
            _log.LogError($"GrabPatches.PostUpdate threw, disabling for session: {e}");
            _disabled = true;
        }
    }

    private static void DumpTypeShape(string label, Type t)
    {
        const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var fields = t.GetFields(bf);
        var props = t.GetProperties(bf);
        var methods = t.GetMethods(bf);
        _log.LogInfo($"[diag] {label} = {t.FullName} | {fields.Length} fields, {props.Length} properties, {methods.Length} methods");
        foreach (var f in fields) _log.LogInfo($"[diag]   field {f.FieldType.Name} {f.Name}");
        foreach (var p in props) _log.LogInfo($"[diag]   prop  {p.PropertyType.Name} {p.Name}");
        foreach (var m in methods)
        {
            // Skip inherited UnityEngine.Object/MonoBehaviour boilerplate to keep the dump readable.
            if (m.DeclaringType == typeof(object) || m.DeclaringType?.Namespace == "UnityEngine") continue;
            _log.LogInfo($"[diag]   method {m.ReturnType.Name} {m.Name}({m.GetParameters().Length} args)");
        }
    }

    private static UQuaternion ToUnity(SNQuaternion q) => new(q.X, q.Y, q.Z, q.W);
}
