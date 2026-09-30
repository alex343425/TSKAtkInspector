namespace TSKAtkInspector;

internal static class ExGaugeMath
{
    internal const int BattleRateCap = 300;

    internal static bool IsBattleRateEffect(string typeName) => typeName is
        "ExRateUp" or "ExRateDown" or "ExRateUpAllyCount" or "ExRateUpAttrOut" or
        "ExRateUpSkillCount" or "ExRateUpSpecific" or "ExRateUpAllyBit";

    // Mirrors TSKBattleCalculationManager.CaluculationSkillGauge.
    // Ordinary Up/Down and Specific read SkillValue1; the conditional variants
    // read the game's live SkillEffectValue.
    internal static long BattleRateValue(string typeName, int skillValue1, int effectiveValue) => typeName switch
    {
        "ExRateUp" => skillValue1,
        "ExRateDown" => -(long)skillValue1,
        "ExRateUpSpecific" => skillValue1,
        "ExRateUpAllyCount" or "ExRateUpAttrOut" or "ExRateUpSkillCount" or "ExRateUpAllyBit" => effectiveValue,
        _ => 0
    };

    internal static int AppliedBattleRate(long rate) => rate switch
    {
        > BattleRateCap => BattleRateCap,
        < int.MinValue => int.MinValue,
        _ => (int)rate
    };

    internal static int CurrentRate(int baseRate, int battleRate)
    {
        long total = (long)baseRate + battleRate;
        if (total <= 0) return 0;
        return total >= int.MaxValue ? int.MaxValue : (int)total;
    }

    internal static int Gain(int currentRate, bool charge)
    {
        long baseValue = 100L + Math.Max(0, currentRate);
        long numerator = charge ? baseValue : baseValue * 4;
        long denominator = charge ? 3 : 15;
        long result = (numerator + denominator - 1) / denominator;
        return result >= int.MaxValue ? int.MaxValue : (int)result;
    }

    // SendAddTeamExGauge deducts a separately rounded loss from the already
    // rounded gain. Match its single-precision rate and product as well.
    internal static int ApplyAtrophy(int gain, int reductionRate, int rateFraction)
    {
        if (rateFraction <= 0) throw new ArgumentOutOfRangeException(nameof(rateFraction));
        float rate = (float)((double)reductionRate / rateFraction);
        float loss = MathF.Ceiling(rate * gain);
        return (int)Math.Clamp((double)gain - loss, 0, int.MaxValue);
    }
}
