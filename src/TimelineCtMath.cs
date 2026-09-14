namespace TSKAtkInspector;

internal static class TimelineCtMath
{
    internal const int VisibleLaneCt = 22;

    // Vanilla shows (notePosition + stunWait - 22) only while notePosition > 22.
    // The requested value is the complete distance from the action line.
    internal static int ActualDistance(int notePosition, int stunWait)
    {
        long total = (long)notePosition + stunWait;
        if (total <= 0) return 0;
        return total >= int.MaxValue ? int.MaxValue : (int)total;
    }

    internal static bool EnemyDamageBadgeBelow(int notePosition) => notePosition >= 16;
}
