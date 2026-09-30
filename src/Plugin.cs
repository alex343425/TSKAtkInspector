using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace TSKAtkInspector;

[BepInPlugin("local.tsk.atkinspector", "TSK ATK Inspector", "1.8.3")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    public override void Load()
    {
        Logger = Log;
        AddComponent<InspectorBehaviour>();
        new Harmony("local.tsk.atkinspector").PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo("ATK Inspector 1.8.3 loaded. Standby shows Cri/CriDmg and auto-sized live EX gain with Atrophy; enemy E/F badges flip below at 16 note CT; timeline icons show full CT. Read-only battle data.");
    }
}

[HarmonyPatch(typeof(TSKBattleLeaderParameter), nameof(TSKBattleLeaderParameter.Initialize))]
internal static class LeaderPatch
{
    static void Postfix(TSKBattleLeaderParameter __instance) => InspectorBehaviour.Observe(__instance);
}

[HarmonyPatch(typeof(TSKBattleLeaderParameter), nameof(TSKBattleLeaderParameter.AttackPowText))]
internal static class LeaderRefreshPatch
{
    static void Postfix(TSKBattleLeaderParameter __instance) => InspectorBehaviour.Observe(__instance);
}

[HarmonyPatch(typeof(TSKBattleNote), nameof(TSKBattleNote.Initialize))]
internal static class BaselinePatch
{
    static void Prefix(TSKBattleNote __instance, TSKBattleUnit __0)
    {
        try { if (__0 != null) BattleReader.Capture(__instance.Pointer, __0.Attack); }
        catch (Exception e) { Plugin.Logger.LogWarning("Cannot capture entry ATK: " + e.Message); }
    }
}

[HarmonyPatch(typeof(TSKBattleMain), nameof(TSKBattleMain.Initialize))]
internal static class BattleStartPatch
{
    static void Prefix() { BattleReader.Reset(); InspectorBehaviour.ResetBattle(); }
}

[HarmonyPatch(typeof(TSKBattleMain), nameof(TSKBattleMain.End))]
internal static class BattleEndPatch
{
    static void Postfix() => InspectorBehaviour.ResetBattle();
}
