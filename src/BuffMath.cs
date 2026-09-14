namespace TSKAtkInspector;

internal enum InspectorSection
{
    Attack,
    CriticalDamage,
    EnemyDebuff
}

internal static class BuffMath
{
    // GameAssembly TSKBattleCalculationManager.CriticalOffset starts a critical
    // hit at 1.5, then adds basis-point values from the active effect list.
    internal const long BaseCriticalDamage = 15000;

    internal static bool IsCriticalDamage(string typeName) =>
        typeName.Contains("CriticalDamageUp", StringComparison.Ordinal) &&
        !typeName.Contains("Down", StringComparison.Ordinal);

    internal static long CriticalDamageValue(string typeName, int effectiveValue,
        int value1, int value2, int value3, int rushCount)
    {
        if (typeName != "SisterCriticalDamageUpRush") return effectiveValue;
        // This is the one critical-damage type for which CriticalOffset ignores
        // SkillEffectValue and calculates the live value from the RUSH count.
        return Math.Min(value1 + (long)value2 * rushCount, value3);
    }
}
