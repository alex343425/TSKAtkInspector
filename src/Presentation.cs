using System.Globalization;
using System.Text.RegularExpressions;

namespace TSKAtkInspector;

public static class Presentation
{
    public static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    public static string Percent(long basisPoints) => (basisPoints / 100d).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture) + "%";
    public static string RatePercent(long basisPoints) => (basisPoints / 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    public static string InlineCriticalSummary(long criticalRate, long criticalDamageRate) =>
        $"Cri {RatePercent(criticalRate)} CriDmg {RatePercent(criticalDamageRate)}";
    public static string ProductPercent(long firstRate, long secondRate) =>
        (firstRate * (double)secondRate / 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    public static string EnemyDamageBadge(long eRate, long fRate) =>
        $"<color=#84E0D7><b>E {RatePercent(eRate)}</b></color>\n<color=#FFD579><b>F {RatePercent(fRate)}</b></color>";
    public static string Safe(string? value) => Regex.Replace(value ?? "", "<[^>]*>", "").Replace("<", "＜").Replace(">", "＞");

    // Time and TimeMax are remaining/original counters. Duration is elapsed, NOT remaining.
    public static string Lifetime(int timeType, int remaining, int maximum)
    {
        string unit = timeType switch
        {
            0 => "CT", 1 => "自身行動", 2 => "對象行動", 3 => "自身行動（時間軸 0 除外）",
            4 => "選擇蓄力", 5 => "發動", _ => $"未知單位 {timeType}"
        };
        // Do not invent an infinite-duration sentinel; expose unknown negative values.
        if (remaining < 0 || maximum < 0) return $"特殊期限（{remaining} / {maximum}；{unit}）";
        var suffix = timeType == 0 ? "" : " 次";
        return timeType == 0 ? $"剩餘 {remaining} / {maximum} CT" : $"剩餘 {remaining} / {maximum}{suffix} {unit}";
    }

    public static string CompactLifetime(int timeType, int remaining, int maximum)
    {
        string full = Lifetime(timeType, remaining, maximum);
        return full.StartsWith("剩餘 ", StringComparison.Ordinal) ? full[3..] : full;
    }

    public static string CompactEffectName(string name)
    {
        if (name == "AtkUp") return "ATK";
        if (name.StartsWith("AtkUp", StringComparison.Ordinal)) return "ATK · " + name[5..];
        if (name == "AtkDown") return "ATK DOWN";
        if (name == "CriticalDamageUp") return "爆傷";
        if (name.StartsWith("CriticalDamageUp", StringComparison.Ordinal)) return "爆傷 · " + name[16..];
        if (name == "SisterCriticalDamageUpRush") return "爆傷 · SisterRush";
        return EffectName(name);
    }

    public static string DamageReceivedName(string name, bool increase)
    {
        string prefix = increase ? "DmgUp" : "DamageDown";
        string label = increase ? "被傷增加" : "被傷減輕";
        return name == prefix ? label : name.StartsWith(prefix, StringComparison.Ordinal) ?
            label + " · " + name[prefix.Length..] : label + " · " + name;
    }

    public static string AttributeName(int value) => value switch
    {
        1 => "炎", 2 => "水", 3 => "雷", 4 => "光", 5 => "闇", 9 => "無", _ => $"未知({value})"
    };

    public static string AttackKindName(int value) => value switch
    {
        1 => "魔法", 2 => "斬擊", 3 => "打擊", _ => $"未知({value})"
    };

    public static string EffectName(string name) => name switch
    {
        "AtkUp" => "ATK 提升", "AtkDown" => "ATK 降低", "AtkUpAtkRate" => "參照施放者 ATK",
        "AtkUpHpRate" => "依 HP 比例提升 ATK", "AtkUpOverHealRate" => "依超量治療提升 ATK",
        "AtkUpRushRate" => "依 RUSH 提升 ATK", "AtkUpStepUp" => "階段式 ATK 提升",
        "AtkUpUseCount" => "依使用次數提升 ATK", "AtkUpEffectCount" => "依效果數量提升 ATK",
        "AtkUpType" => "屬性條件 ATK 提升", "AtkUpRace" => "種族條件 ATK 提升",
        "AtkUpTypeRace" => "屬性／種族條件 ATK 提升", "AtkUpRole" => "職業條件 ATK 提升",
        "PassiveHpDownAtkUp" => "HP 減少時 ATK 提升", "SuperCharge" => "超蓄力",
        "Charge" => "蓄力", "AttackBuffEffectTimeUp" => "ATK BUFF 強化／延長",
        "ModeChange" => "形態變化", "ModeChangeB" => "形態變化 B",
        "AttackBuffEffectTimeUp2" => "ATK BUFF 強化／延長 2",
        "CriticalDamageUp" => "爆擊傷害倍率提升",
        "CriticalDamageUpDesignateSkill" => "指定技能爆擊傷害倍率提升",
        "CriticalDamageUpUnisonRate" => "依 UNISON 提升爆擊傷害倍率",
        "CriticalDamageUpAllyRate" => "依隊伍人數提升爆擊傷害倍率",
        "CriticalDamageUpUseCount" => "依使用次數提升爆擊傷害倍率",
        "CriticalDamageUpTransform" => "變身時提升爆擊傷害倍率",
        "CriticalDamageUpEnemyStun" => "敵方 STUN 時提升爆擊傷害倍率",
        "CriticalDamageUpSister" => "シスター爆擊傷害倍率提升",
        "SisterCriticalDamageUpRush" => "シスター依 RUSH 提升爆擊傷害倍率",
        "CriticalDamageUpTypeRace" => "屬性／種族條件爆擊傷害倍率提升",
        "CriticalDamageUpRushRate" => "依 RUSH 提升爆擊傷害倍率",
        "CriticalDamageUpAllyRateBit" => "依符合條件隊友數提升爆擊傷害倍率",
        "CriticalDamageUpAttributeOut" => "指定屬性以外爆擊傷害倍率提升",
        "CriticalDamageUpSpecificCount" => "依指定條件數量提升爆擊傷害倍率",
        "CriticalDamageUpTypeOutCount" => "依指定屬性外人數提升爆擊傷害倍率",
        "CriticalDamageUpAttributeOrAttackType" => "屬性／攻擊類型條件爆擊傷害倍率提升",
        "CriticalDamageUpRaceOrAffiriate" => "種族／所屬條件爆擊傷害倍率提升",
        _ => name.StartsWith("AtkUp") ? $"ATK 提升 · {name[5..]}" :
            name.StartsWith("CriticalDamageUp") ? $"爆擊傷害倍率提升 · {name[16..]}" : name
    };

    public static string? CriticalDamageCondition(string name) => name switch
    {
        "CriticalDamageUpDesignateSkill" => "適用條件：指定技能。",
        "CriticalDamageUpUnisonRate" => "有效量依 UNISON 相關條件計算。",
        "CriticalDamageUpAllyRate" => "有效量依隊伍人數計算。",
        "CriticalDamageUpUseCount" => "有效量依技能使用次數計算。",
        "CriticalDamageUpTransform" => "適用條件：變身狀態。",
        "CriticalDamageUpEnemyStun" => "適用條件：敵方處於 STUN。",
        "CriticalDamageUpTypeRace" => "適用條件：指定屬性／種族。",
        "CriticalDamageUpRushRate" => "有效量依目前 RUSH 計算。",
        "CriticalDamageUpAllyRateBit" => "有效量依符合條件的隊友數計算。",
        "CriticalDamageUpAttributeOut" => "適用條件：指定屬性以外。",
        "CriticalDamageUpSpecificCount" => "有效量依指定條件的數量計算。",
        "CriticalDamageUpTypeOutCount" => "有效量依指定屬性以外的隊友數計算。",
        "CriticalDamageUpAttributeOrAttackType" => "適用條件：指定屬性或攻擊類型。",
        "CriticalDamageUpRaceOrAffiriate" => "適用條件：指定種族或所屬。",
        _ => null
    };
}
