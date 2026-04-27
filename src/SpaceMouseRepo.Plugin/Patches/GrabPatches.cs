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

    public static void Install(Harmony harmony, ManualLogSource log, ManipulationConfig cfg, Func<SpaceMouseState> readState)
    {
        Log = log;
        Cfg = cfg;
        ReadState = readState;

        try
        {
            // PatchAll() with no args scans the entire calling assembly for [HarmonyPatch]-attributed
            // classes and applies them. This matches the pattern used by working R.E.P.O. mods like
            // Omniscye/DualGrab — typed typeof(PhysGrabber) attaches reliably where the previous
            // string-based AccessTools.Method approach didn't.
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
public static class UpdateSentinelPatch
{
    private static bool _diag_ran;

    [HarmonyPostfix]
    [HarmonyPatch("Update")]
    public static void Postfix(PhysGrabber __instance)
    {
        if (!_diag_ran)
        {
            _diag_ran = true;
            GrabPatches.Log.LogInfo("[diag] PhysGrabber.Update POSTFIX ran (Harmony is patching correctly)");
        }
    }
}

[HarmonyPatch(typeof(PhysGrabber))]
public static class GetRotationInputPatch
{
    private static bool _diag_postfixRan;
    private static bool _diag_postfixModified;

    [HarmonyPostfix]
    [HarmonyPatch("GetRotationInput")]
    public static void Postfix(PhysGrabber __instance, ref UQuaternion __result)
    {
        if (!_diag_postfixRan)
        {
            _diag_postfixRan = true;
            GrabPatches.Log.LogInfo($"[diag] GetRotationInput POSTFIX ran, vanilla=({__result.x:F3},{__result.y:F3},{__result.z:F3},{__result.w:F3})");
        }

        if (GrabPatches.Disabled) return;
        try
        {
            var ctrl = GrabPatches.ByHolder.GetValue(__instance, _ => new HeldObjectController(GrabPatches.Cfg));
            ctrl.Apply(GrabPatches.ReadState(), UnityEngine.Time.deltaTime);

            var ourDelta = GrabPatches.ToUnity(ctrl.AccumulatedRotation);
            if (ourDelta == UQuaternion.identity) return;

            __result = ourDelta * __result;

            if (!_diag_postfixModified)
            {
                _diag_postfixModified = true;
                GrabPatches.Log.LogInfo($"[diag] GetRotationInput POSTFIX modified result: delta=({ourDelta.x:F3},{ourDelta.y:F3},{ourDelta.z:F3},{ourDelta.w:F3})");
            }
        }
        catch (Exception e) when (e is not ThreadAbortException)
        {
            GrabPatches.Log.LogError($"GetRotationInput postfix threw, disabling: {e}");
            GrabPatches.Disabled = true;
        }
    }
}
