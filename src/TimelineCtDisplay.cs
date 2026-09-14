using System.Globalization;
using HarmonyLib;

namespace TSKAtkInspector;

internal static class TimelineCtDisplay
{
    private static string _lastError = "";

    internal static void Apply(TSKBattleNoteView view, int notePosition, int stunWait)
    {
        try
        {
            var root = view.outObject;
            var text = view.outCount;
            if (root == null || text == null) return;
            text.text = TimelineCtMath.ActualDistance(notePosition, stunWait).ToString(CultureInfo.InvariantCulture);
            root.SetActive(true);
        }
        catch (Exception e) { LogOnce(e); }
    }

    internal static void Apply(TSKBattleUnitIcon view, int notePosition, int stunWait)
    {
        try
        {
            var root = view.outObject;
            var text = view.outText;
            if (root == null || text == null) return;
            text.text = TimelineCtMath.ActualDistance(notePosition, stunWait).ToString(CultureInfo.InvariantCulture);
            root.SetActive(true);
        }
        catch (Exception e) { LogOnce(e); }
    }

    private static void LogOnce(Exception e)
    {
        string value = e.ToString();
        if (value == _lastError) return;
        _lastError = value;
        Plugin.Logger.LogWarning("Cannot update persistent timeline CT: " + e.Message);
    }
}

[HarmonyPatch(typeof(TSKBattleNoteView), nameof(TSKBattleNoteView.SetNoteCountText))]
internal static class TimelineNoteCtPatch
{
    static void Postfix(TSKBattleNoteView __instance, int __0, int __1) =>
        TimelineCtDisplay.Apply(__instance, __0, __1);
}

[HarmonyPatch(typeof(TSKBattleUnitIcon), nameof(TSKBattleUnitIcon.SetOutCountObject))]
internal static class TimelineUnitIconCtPatch
{
    static void Postfix(TSKBattleUnitIcon __instance, int __0, int __1) =>
        TimelineCtDisplay.Apply(__instance, __0, __1);
}
