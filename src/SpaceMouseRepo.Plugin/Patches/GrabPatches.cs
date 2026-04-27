using System;
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
    internal static ManualLogSource Log = null!;
    internal static ManipulationConfig Cfg = null!;
    internal static Func<SpaceMouseState> ReadState = () => SpaceMouseState.Empty;
    internal static bool Disabled;
    internal static readonly ConditionalWeakTable<PhysGrabber, HeldObjectController> ByHolder = new();
    internal static System.IO.StreamWriter? SideChannel;

    internal static void Tee(string line)
    {
        Log.LogInfo(line);
        try { SideChannel?.WriteLine($"[{System.DateTime.Now:HH:mm:ss.fff}] {line}"); } catch { /* ignore */ }
    }

    public static void Install(Harmony harmony, ManualLogSource log, ManipulationConfig cfg, Func<SpaceMouseState> readState)
    {
        Log = log;
        Cfg = cfg;
        ReadState = readState;

        try
        {
            harmony.PatchAll();
            Log.LogInfo("Harmony.PatchAll() completed.");
        }
        catch (Exception e)
        {
            Log.LogError($"Harmony.PatchAll() threw: {e}");
            Disabled = true;
        }
    }

    internal static UQuaternion ToUnity(SNQuaternion q) => new(q.X, q.Y, q.Z, q.W);
}

[HarmonyPatch(typeof(PhysGrabber))]
public static class UpdatePostfix
{
    private static bool _diag_loggedFirstFire;
    private static bool _diag_loggedFirstGrab;
    private static bool _diag_loggedFirstWrite;

    [HarmonyPostfix]
    [HarmonyPatch("Update")]
    public static void Postfix(PhysGrabber __instance)
    {
        if (!_diag_loggedFirstFire)
        {
            _diag_loggedFirstFire = true;
            GrabPatches.Tee("[diag] PhysGrabber.Update POSTFIX firing.");
        }
        if (GrabPatches.Disabled) return;
        try
        {
            // Real PhysGrabber fields are non-public in R.E.P.O.'s Assembly-CSharp; our stub had them
            // public so the IL compiled fine, but runtime accessibility checks reject direct ld/stfld
            // (FieldAccessException, every frame, ~60 Hz). Traverse goes through reflection and
            // ignores access modifiers. Cached internally so it's still fast.
            var trav = Traverse.Create(__instance);
            if (!trav.Field<bool>("isLocal").Value) return;

            var ctrl = GrabPatches.ByHolder.GetValue(__instance, _ => new HeldObjectController(GrabPatches.Cfg));

            if (!trav.Field<bool>("grabbed").Value)
            {
                ctrl.OnRelease();
                return;
            }

            if (!_diag_loggedFirstGrab)
            {
                _diag_loggedFirstGrab = true;
                GrabPatches.Tee("[diag] First time observing grabbed=true on local PhysGrabber.");
            }

            ctrl.Apply(GrabPatches.ReadState(), UnityEngine.Time.deltaTime);

            var ourDelta = GrabPatches.ToUnity(ctrl.AccumulatedRotation);
            if (ourDelta == UQuaternion.identity) return;

            var physRotField = trav.Field<UQuaternion>("physRotation");
            var nextPhysRotField = trav.Field<UQuaternion>("nextPhysRotation");

            var beforePhys = physRotField.Value;
            var combined = ourDelta * beforePhys;
            physRotField.Value = combined;
            nextPhysRotField.Value = combined;

            if (!_diag_loggedFirstWrite)
            {
                _diag_loggedFirstWrite = true;
                GrabPatches.Tee($"[diag] First rotation write: before=({beforePhys.x:F3},{beforePhys.y:F3},{beforePhys.z:F3},{beforePhys.w:F3}) delta=({ourDelta.x:F3},{ourDelta.y:F3},{ourDelta.z:F3},{ourDelta.w:F3})");
            }
        }
        catch (Exception e) when (e is not ThreadAbortException)
        {
            GrabPatches.Log.LogError($"UpdatePostfix threw, disabling: {e}");
            GrabPatches.Disabled = true;
        }
    }
}
