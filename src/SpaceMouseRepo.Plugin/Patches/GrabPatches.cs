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
    internal static ManualLogSource Log = null!;
    internal static ManipulationConfig Cfg = null!;
    internal static Func<SpaceMouseState> ReadState = () => SpaceMouseState.Empty;
    internal static bool Disabled;
    internal static readonly ConditionalWeakTable<object, HeldObjectController> ByHolder = new();

    public static void Install(Harmony harmony, ManualLogSource log, ManipulationConfig cfg, Func<SpaceMouseState> readState)
    {
        Log = log;
        Cfg = cfg;
        ReadState = readState;

        var grabberType = AccessTools.TypeByName("PhysGrabber");
        if (grabberType == null)
        {
            Log.LogError("Type PhysGrabber not found. SpaceMouse mod inactive.");
            Disabled = true;
            return;
        }

        DumpTypeShape("PhysGrabber", grabberType);

        // Switch to attribute-based PatchAll. Manual harmony.Patch() reported success in v0.1.0-0.1.3
        // but the postfix never actually fired in-game. PatchAll uses a different internal code path
        // and is the canonical BepInEx approach.
        try
        {
            harmony.PatchAll(typeof(GetRotationInputPatch));
            harmony.PatchAll(typeof(UpdatePatch));
            Log.LogInfo("PatchAll completed for GetRotationInputPatch + UpdatePatch");
        }
        catch (Exception e)
        {
            Log.LogError($"PatchAll threw: {e}");
            Disabled = true;
        }
    }

    private static void DumpTypeShape(string label, Type t)
    {
        const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var fields = t.GetFields(bf);
        var methods = t.GetMethods(bf);
        Log.LogInfo($"[diag] {label} = {t.FullName} | {fields.Length} fields, {methods.Length} methods");
        foreach (var m in methods)
        {
            if (m.DeclaringType == typeof(object) || m.DeclaringType?.Namespace == "UnityEngine") continue;
            Log.LogInfo($"[diag]   method {m.ReturnType.Name} {m.Name}({m.GetParameters().Length} args)");
        }
    }

    internal static UQuaternion ToUnity(SNQuaternion q) => new(q.X, q.Y, q.Z, q.W);
}

[HarmonyPatch]
internal static class GetRotationInputPatch
{
    private static bool _diag_prefixRan;
    private static bool _diag_postfixRan;
    private static bool _diag_postfixModified;

    static MethodBase TargetMethod() => AccessTools.Method("PhysGrabber:GetRotationInput");

    [HarmonyPrefix]
    static void Prefix()
    {
        if (!_diag_prefixRan)
        {
            _diag_prefixRan = true;
            GrabPatches.Log.LogInfo("[diag] GetRotationInput PREFIX ran");
        }
    }

    [HarmonyPostfix]
    static void Postfix(object __instance, ref UQuaternion __result)
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
            GrabPatches.Log.LogError($"GetRotationInputPatch.Postfix threw, disabling: {e}");
            GrabPatches.Disabled = true;
        }
    }
}

// Belt-and-suspenders: if GetRotationInput isn't being called by vanilla on your input,
// Update certainly is — this proves at least *some* postfix fires every frame.
[HarmonyPatch]
internal static class UpdatePatch
{
    private static bool _diag_ran;

    static MethodBase TargetMethod() => AccessTools.Method("PhysGrabber:Update");

    [HarmonyPostfix]
    static void Postfix()
    {
        if (!_diag_ran)
        {
            _diag_ran = true;
            GrabPatches.Log.LogInfo("[diag] PhysGrabber.Update POSTFIX ran (any postfix at all is firing)");
        }
    }
}
