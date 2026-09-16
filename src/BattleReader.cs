using System.Text;
using Skill = TSKBattleSkillData;
using FieldType = TSKBattleSkillFieldEffect.EffectType;

namespace TSKAtkInspector;

internal sealed record BattleSnapshot(string Title, string Summary, string Details);
internal readonly record struct EnemyDamageRates(
    long IncreaseRate, long ReductionRate, long ERate, long NamedRate, long FRate,
    int Attribute, int AttackKind, bool Human, bool Deity, bool Demon,
    int CollapseCount, int DominationLevel, int NamedCount, int ApplicableNamedCount);
internal readonly record struct ExGaugeStatus(
    int BaseRate, int BattleRate, int CurrentRate, int NormalGain, int ChargeGain,
    bool HasBattleRateEffect, bool IsCharge);

internal static class BattleReader
{
    private static readonly Dictionary<IntPtr, int> EntryAttack = new();
    internal static void Reset() { EntryAttack.Clear(); SourceTracker.Reset(); }
    internal static void Capture(IntPtr note, int attack) => EntryAttack[note] = attack;

    internal static BattleSnapshot Read(TSKBattleNote note, InspectorSection section, bool allEffects, int enemyIndex = 0) =>
        section switch
        {
            InspectorSection.CriticalDamage => ReadCriticalDamage(note, allEffects),
            InspectorSection.EnemyDebuff => ReadEnemyDebuff(note, enemyIndex, allEffects),
            _ => ReadAttack(note, allEffects)
        };

    internal static int EnemyCount(TSKBattleNote note)
    {
        var list = note.EnemyTeam?.NoteList;
        return list == null ? 0 : Math.Min(5, list.Count);
    }

    internal static long CurrentCriticalDamage(TSKBattleNote note)
    {
        var effects = note.SkillEffectList;
        var team = note.Team;
        int rush = team != null ? team.RushCount : 0;
        long rate = BuffMath.BaseCriticalDamage;
        if (effects == null) return rate;
        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            string name = effect.Type.ToString();
            if (!BuffMath.IsCriticalDamage(name)) continue;
            rate += BuffMath.CriticalDamageValue(name, effect.SkillEffectValue,
                effect.SkillValue1, effect.SkillValue2, effect.SkillValue3, rush);
        }
        return rate;
    }

    internal static ExGaugeStatus CurrentExGauge(TSKBattleNote note)
    {
        int baseRate = note.GetBaseExGaugeRate();
        long rawBattleRate = 0;
        bool hasBattleRateEffect = false;
        var effects = note.SkillEffectList;
        if (effects != null) for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            string typeName = effect.Type.ToString();
            if (!ExGaugeMath.IsBattleRateEffect(typeName)) continue;
            hasBattleRateEffect = true;
            rawBattleRate += ExGaugeMath.BattleRateValue(typeName,
                effect.SkillValue1, effect.SkillEffectValue);
        }

        int battleRate = ExGaugeMath.AppliedBattleRate(rawBattleRate);
        int currentRate = ExGaugeMath.CurrentRate(baseRate, battleRate);
        return new ExGaugeStatus(baseRate, battleRate, currentRate,
            ExGaugeMath.Gain(currentRate, false), ExGaugeMath.Gain(currentRate, true),
            hasBattleRateEffect, note.isCharge || note.isSuperCharge);
    }

    internal static EnemyDamageRates CurrentEnemyDamageRates(TSKBattleNote attacker, TSKBattleNote target)
    {
        var attackerUnit = attacker.UnitData;
        if (attackerUnit == null) throw new InvalidOperationException("攻擊角色資料尚未準備好");

        long increaseRate = 0, reductionRate = 0;
        var increases = target.GetSkillEffect("DmgUp");
        if (increases != null) for (int i = 0; i < increases.Count; i++)
            increaseRate += increases[i].SkillEffectValue;
        var reductions = target.GetSkillEffect("DamageDown");
        if (reductions != null) for (int i = 0; i < reductions.Count; i++)
            reductionRate += reductions[i].SkillEffectValue;

        int attribute = attackerUnit.AttrType;
        int attackKind = attackerUnit.AttackKind;
        bool human = attacker.CheckRace(1);
        bool deity = attacker.CheckRace(2);
        bool demon = attacker.CheckRace(3);
        int collapseCount = target.collapseData != null ? target.collapseData.Count : 0;
        int dominationLevel = target.dominationLevel;
        long namedRate = 0;
        int namedCount = 0, applicableNamedCount = 0;
        var effects = target.SkillEffectList;
        if (effects != null) for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            string typeName = effect.Type.ToString();
            if (!EnemyDebuffMath.IsNamedDebuff(typeName)) continue;
            namedCount++;
            var requirement = EnemyDebuffMath.Requirement(typeName);
            if (!EnemyDebuffMath.Applies(requirement, attribute, attackKind, human, deity, demon)) continue;
            int state = typeName == "Collapse" ? collapseCount : typeName == "Domination" ? dominationLevel : 0;
            namedRate += EnemyDebuffMath.Value(typeName, effect.SkillValue1, effect.SkillValue2,
                effect.SkillValue3, effect.SkillValue5, state);
            applicableNamedCount++;
        }

        return new EnemyDamageRates(increaseRate, reductionRate,
            EnemyDebuffMath.BaseRate + increaseRate - reductionRate,
            namedRate, EnemyDebuffMath.BaseRate + namedRate,
            attribute, attackKind, human, deity, demon,
            collapseCount, dominationLevel, namedCount, applicableNamedCount);
    }

    private static TSKBattleNote? EnemyAt(TSKBattleNote note, int index)
    {
        var list = note.EnemyTeam?.NoteList;
        return list != null && index >= 0 && index < list.Count && index < 5 ? list[index] : null;
    }

    private static BattleSnapshot ReadAttack(TSKBattleNote note, bool allEffects)
    {
        var unit = note.UnitData;
        if (unit == null) throw new InvalidOperationException("角色資料尚未準備好");
        var entryKnown = EntryAttack.TryGetValue(note.Pointer, out var entry);
        if (!entryKnown) entry = unit.Attack;
        int basis = note.GetBaseAttack();
        int current = note.GetAttack(false); // Same call as the game's AttackPowText.
        var buffs = note.GetAtkUpSkillEffect();
        var buffPointers = new HashSet<IntPtr>();
        long rate = 0, down = 0;
        if (buffs != null) for (int i = 0; i < buffs.Count; i++)
        {
            var effect = buffs[i];
            buffPointers.Add(effect.Pointer);
            rate += effect.SkillEffectValue;
        }
        var effects = note.SkillEffectList;
        if (effects != null) for (int i = 0; i < effects.Count; i++)
            if (effects[i].Type == Skill.SkillType.AtkDown) down += effects[i].SkillValue1;

        var sb = new StringBuilder();
        sb.AppendLine("<b>基礎與戰鬥狀態</b>");
        sb.AppendLine($"入場基礎 ATK：{Presentation.Number(entry)}" + (entryKnown ? "（初始化時記錄）" : "（未捕捉入場；以下為角色資料值）"));
        sb.AppendLine("入場數值已包含遊戲交付的養成／裝備等；未提供的戰前細項無法拆分。");
        sb.AppendLine($"當前計算基礎：{Presentation.Number(basis)}　與入場差值：{basis - (long)entry:+#,0;-#,0;0}");
        sb.AppendLine("入場基礎為參考值，沒有 BUFF 倒數；場地／變身修正另列於下方。");
        sb.AppendLine($"ATK BUFF：{Presentation.Percent(rate)}　DEBUFF：{Presentation.Percent(-down)}");
        sb.AppendLine($"百分比合計：{Presentation.Percent(Math.Max(-8000, rate - down))}（遊戲下限 -80%；蓄力與特殊加算另計）");
        if (note.isCharge || note.isSuperCharge)
        {
            var chargeRate = 5000;
            if (note.isSuperCharge && effects != null)
                for (int i = 0; i < effects.Count; i++)
                    if (effects[i].Type == Skill.SkillType.SuperCharge) { chargeRate = effects[i].SkillValue1; break; }
            sb.AppendLine($"<color=#FFD579>{(note.isSuperCharge ? "超蓄力" : "蓄力")}中：計算基礎 × {Presentation.Percent(chargeRate)}；場地加成另列，持續至狀態解除。</color>");
        }

        var modes = note.GetModeChangeEffect();
        var modePointers = new HashSet<IntPtr>();
        if (modes != null) for (int i = 0; i < modes.Count; i++) modePointers.Add(modes[i].Pointer);
        sb.AppendLine();
        sb.AppendLine("<b>目前附加效果　｜　有效量　｜　剩餘 / 最大期限</b>");
        int shown = 0;
        if (effects != null) for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            string name = effect.Type.ToString();
            bool isBuff = buffPointers.Contains(effect.Pointer);
            bool isMode = modePointers.Contains(effect.Pointer);
            bool related = isBuff || isMode || name.StartsWith("Atk") || name.Contains("AttackBuff") ||
                name is "PassiveHpDownAtkUp" or "SuperCharge" or "Charge";
            if (!related && !allEffects) continue;
            shown++;
            string amount;
            if (isBuff) amount = Presentation.Percent(effect.SkillEffectValue);
            else if (effect.Type == Skill.SkillType.AtkDown) amount = Presentation.Percent(-(long)effect.SkillValue1);
            else if (effect.Type == Skill.SkillType.AtkUpAtkRate)
            {
                var source = effect.Note;
                amount = $"施放者基礎 ATK × {Presentation.Percent(effect.SkillEffectValue)}";
                if (source != null && source.UnitData != null)
                {
                    // Native GetAttack truncates this term, unlike the rounded main BUFF group.
                    var added = (long)(source.GetBaseAttack() * (float)(effect.SkillEffectValue / 10000d));
                    amount += $" = +{Presentation.Number(added)} ATK";
                }
            }
            else if (isMode) amount = $"基礎 ATK 修正 {Presentation.Percent(effect.SkillValue1)}（多筆時遊戲採最後一筆）";
            else if (name == "SuperCharge") amount = $"蓄力加成 {Presentation.Percent(effect.SkillValue1)}（取代普通蓄力）";
            else amount = $"特殊效果；原始有效值 {effect.SkillEffectValue}（未換算）";
            var color = name == "AtkDown" ? "FFACB9" : related ? "84E0D7" : "C6CBDD";
            sb.AppendLine($"<color=#{color}><b>{shown}. {Presentation.CompactEffectName(name)}</b></color> {amount} | {Presentation.CompactLifetime((int)effect.TimeType, effect.Time, effect.TimeMax)}");
            sb.AppendLine($"    來源：{SourceTracker.Resolve(note, effect)}");
            if (effect.IsCopy) sb.AppendLine("    複製效果；名稱依目前保存的來源辨識。");
            if (effect.IsProhibited) sb.AppendLine("    遊戲標記：IsProhibited；保留顯示供核對。");
            if (effect.IsApplyAttackBuffUp) sb.AppendLine("    已套用 ATK BUFF 強化（上方為強化後有效量）。");
            if (allEffects || (!isBuff && effect.Type != Skill.SkillType.AtkDown && !isMode))
                sb.AppendLine($"    參數：{effect.SkillValue1}, {effect.SkillValue2}, {effect.SkillValue3}, {effect.SkillValue4}, {effect.SkillValue5}　效果 ID {effect.ID}");
        }
        if (shown == 0) sb.AppendLine("目前效果清單中沒有可辨識的 ATK 加成。\n");

        sb.AppendLine("<b>適用於此角色的場地修正</b>");
        var field = TSKBattleSkillFieldEffect.Instance;
        int fieldCount = 0;
        if (field != null) foreach (var kind in new[] { FieldType.NebulaRate, FieldType.BaseAtkDown, FieldType.UnleashStarPower, FieldType.ChargeDamageUp })
        {
            var list = field.GetFieldEffect(kind, note);
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                long value = kind == FieldType.UnleashStarPower ? item.effect_value_2 : item.effect_value_1;
                if (kind == FieldType.BaseAtkDown) value = -value;
                fieldCount++;
                sb.AppendLine($"<color=#FFD579>{Presentation.Safe(item.field_effect_name)}</color>　{Presentation.Percent(value)}" +
                    (kind == FieldType.ChargeDamageUp && !note.isCharge && !note.isSuperCharge ? "（蓄力時適用）" : ""));
                sb.AppendLine($"    {Presentation.Safe(item.detail)}");
                sb.AppendLine("    期限：場地適用期間（資料未提供個別倒數）");
            }
        }
        if (fieldCount == 0) sb.AppendLine("無適用的 ATK 場地修正。");
        sb.AppendLine("\n數值每 0.25 秒更新；CT／行動次數由遊戲扣除。\n各效果可能共用基礎、受下限及取整影響，請以目前 ATK 為準。\n開啟明細不會暫停戰鬥；可先使用遊戲暫停或 TSKHook 的 F5。");
        return new BattleSnapshot($"ATK 加成明細 · {Presentation.Safe(unit.CharacterName)}",
            $"目前 ATK  <b>{Presentation.Number(current)}</b>     {(entryKnown ? "入場基礎" : "角色資料值")}  {Presentation.Number(entry)}     計算基礎  {Presentation.Number(basis)}", sb.ToString());
    }

    private static BattleSnapshot ReadCriticalDamage(TSKBattleNote note, bool allEffects)
    {
        var unit = note.UnitData;
        if (unit == null) throw new InvalidOperationException("角色資料尚未準備好");
        var effects = note.SkillEffectList;
        var team = note.Team;
        int rush = team != null ? team.RushCount : 0;
        long currentRate = CurrentCriticalDamage(note);
        long buffRate = currentRate - BuffMath.BaseCriticalDamage;

        var sb = new StringBuilder();
        sb.AppendLine("<b>爆擊傷害倍率重點</b>");
        sb.AppendLine($"基礎爆擊傷害倍率：{Presentation.RatePercent(BuffMath.BaseCriticalDamage)}");
        sb.AppendLine($"目前效果清單加成：{Presentation.Percent(buffRate)}");
        sb.AppendLine($"目前可檢測倍率：<b>{Presentation.RatePercent(currentRate)}</b>　目前 RUSH：{rush}");
        sb.AppendLine("只有攻擊判定為爆擊時才套用此倍率；爆擊率（CrtUp）不屬於本分類。");
        sb.AppendLine("傷害結算另有依當次技能／目標傳入的單次爆傷值；未選定攻擊前不併入上方倍率。");
        sb.AppendLine();
        sb.AppendLine("<b>目前附加效果　｜　有效量　｜　剩餘 / 最大期限</b>");
        int shown = 0;
        if (effects != null) for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            string name = effect.Type.ToString();
            bool related = BuffMath.IsCriticalDamage(name);
            if (!related && !allEffects) continue;
            shown++;
            string amount;
            if (related)
            {
                long value = BuffMath.CriticalDamageValue(name, effect.SkillEffectValue,
                    effect.SkillValue1, effect.SkillValue2, effect.SkillValue3, rush);
                amount = Presentation.Percent(value);
                if (name == "SisterCriticalDamageUpRush") amount +=
                    $"（基礎 {Presentation.Percent(effect.SkillValue1)}＋每 RUSH {Presentation.Percent(effect.SkillValue2)} × {rush}；上限 {Presentation.Percent(effect.SkillValue3)}）";
            }
            else amount = $"其他效果；原始有效值 {effect.SkillEffectValue}（未換算）";
            var color = related ? "84E0D7" : "C6CBDD";
            sb.AppendLine($"<color=#{color}><b>{shown}. {Presentation.CompactEffectName(name)}</b></color> {amount} | {Presentation.CompactLifetime((int)effect.TimeType, effect.Time, effect.TimeMax)}");
            sb.AppendLine($"    來源：{SourceTracker.Resolve(note, effect)}");
            var condition = Presentation.CriticalDamageCondition(name);
            if (condition != null) sb.AppendLine("    " + condition);
            if (effect.IsCopy) sb.AppendLine("    複製效果；名稱依目前保存的來源辨識。");
            if (effect.IsProhibited) sb.AppendLine("    遊戲標記：IsProhibited；保留顯示供核對。");
            if (allEffects || !related || name == "SisterCriticalDamageUpRush")
                sb.AppendLine($"    參數：{effect.SkillValue1}, {effect.SkillValue2}, {effect.SkillValue3}, {effect.SkillValue4}, {effect.SkillValue5}　效果 ID {effect.ID}");
        }
        if (shown == 0) sb.AppendLine("目前效果清單中沒有可辨識的爆擊傷害倍率加成。\n");

        sb.AppendLine("數值每 0.25 秒更新；CT／行動次數由遊戲扣除。\nシスター RUSH 型依遊戲原生公式：基礎值＋每 RUSH 增量，並受設定上限限制。\n開啟明細不會暫停戰鬥；可先使用遊戲暫停或 TSKHook 的 F5。");
        return new BattleSnapshot($"爆擊傷害倍率 UP 明細 · {Presentation.Safe(unit.CharacterName)}",
            $"目前可檢測倍率  <b>{Presentation.RatePercent(currentRate)}</b>     基礎  {Presentation.RatePercent(BuffMath.BaseCriticalDamage)}     BUFF  {Presentation.Percent(buffRate)}     RUSH  {rush}", sb.ToString());
    }

    private static BattleSnapshot ReadEnemyDebuff(TSKBattleNote attacker, int enemyIndex, bool allEffects)
    {
        var attackerUnit = attacker.UnitData;
        if (attackerUnit == null) throw new InvalidOperationException("攻擊角色資料尚未準備好");
        var target = EnemyAt(attacker, enemyIndex);
        if (target == null || target.UnitData == null)
            return new BattleSnapshot("敵方 DEBUFF 明細", "目前沒有可讀取的敵方目標", "敵方隊伍資料尚未準備好，或該目標欄位不存在。");

        var targetUnit = target.UnitData;
        var effects = target.SkillEffectList;
        var rates = CurrentEnemyDamageRates(attacker, target);
        var increases = target.GetSkillEffect("DmgUp");
        var reductions = target.GetSkillEffect("DamageDown");
        var increasePointers = new HashSet<IntPtr>();
        var reductionPointers = new HashSet<IntPtr>();
        if (increases != null) for (int i = 0; i < increases.Count; i++)
        {
            var effect = increases[i];
            increasePointers.Add(effect.Pointer);
        }
        if (reductions != null) for (int i = 0; i < reductions.Count; i++)
        {
            var effect = reductions[i];
            reductionPointers.Add(effect.Pointer);
        }
        long increaseRate = rates.IncreaseRate, reductionRate = rates.ReductionRate;
        long eRate = rates.ERate, namedRate = rates.NamedRate, fRate = rates.FRate;
        int attribute = rates.Attribute, attackKind = rates.AttackKind;
        bool human = rates.Human, deity = rates.Deity, demon = rates.Demon;
        int collapseCount = rates.CollapseCount, dominationLevel = rates.DominationLevel;
        int namedCount = rates.NamedCount, applicableNamedCount = rates.ApplicableNamedCount;
        string attackerName = Presentation.Safe(attackerUnit.CharacterName);
        string targetName = Presentation.Safe(string.IsNullOrWhiteSpace(targetUnit.CharacterName) ? targetUnit.UnitName : targetUnit.CharacterName);

        var sb = new StringBuilder();
        sb.AppendLine($"<b>敵方 {enemyIndex + 1} · {targetName}</b>　HP {Presentation.Number(target.CurrentHP)} / {Presentation.Number(target.MaxHP)}" +
            (target.isDefeat ? "　<color=#FFACB9>已擊破</color>" : ""));
        sb.AppendLine($"條件判定攻擊者：{attackerName}　屬性 {Presentation.AttributeName(attribute)}／攻擊類型 {Presentation.AttackKindName(attackKind)}");
        sb.AppendLine($"<color=#84E0D7><b>E 被傷增減倍率（共通）：{Presentation.RatePercent(eRate)}</b></color>　100% ＋增加 {Presentation.Percent(increaseRate)} ＋減輕 {Presentation.Percent(-reductionRate)}");
        sb.AppendLine($"<color=#FFD579><b>F 命名 DEBUFF 倍率：{Presentation.RatePercent(fRate)}</b></color>　100% ＋目前適用 {Presentation.Percent(namedRate)}");
        sb.AppendLine($"E × F：<b>{Presentation.ProductPercent(eRate, fRate)}</b>（只合併這兩個乘區）");
        sb.AppendLine();

        sb.AppendLine("<b>E｜被傷增加／減輕　｜　有效量　｜　剩餘 / 最大期限</b>");
        int eShown = 0;
        if (effects != null) for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            bool up = increasePointers.Contains(effect.Pointer);
            bool down = reductionPointers.Contains(effect.Pointer);
            if (!up && !down) continue;
            eShown++;
            string typeName = effect.Type.ToString();
            string name = Presentation.DamageReceivedName(typeName, up);
            long value = up ? effect.SkillEffectValue : -(long)effect.SkillEffectValue;
            sb.AppendLine($"<color=#{(up ? "84E0D7" : "FFD579")}><b>{eShown}. {name}</b></color> {Presentation.Percent(value)} | {Presentation.CompactLifetime((int)effect.TimeType, effect.Time, effect.TimeMax)}");
            sb.AppendLine($"    來源：{SourceTracker.Resolve(target, effect)}");
            if (effect.IsCopy) sb.AppendLine("    複製效果；名稱依目前保存的來源辨識。");
            if (effect.IsProhibited) sb.AppendLine("    遊戲標記：IsProhibited；保留顯示供核對。");
            if (allEffects) sb.AppendLine($"    參數：{effect.SkillValue1}, {effect.SkillValue2}, {effect.SkillValue3}, {effect.SkillValue4}, {effect.SkillValue5}　效果 ID {effect.ID}");
        }
        if (eShown == 0) sb.AppendLine("目前沒有被傷增加／減輕效果。");

        sb.AppendLine();
        sb.AppendLine($"<b>F｜命名 DEBUFF（{applicableNamedCount} / {namedCount} 筆目前適用）</b>");
        int fShown = 0;
        if (effects != null) for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            string typeName = effect.Type.ToString();
            if (!EnemyDebuffMath.IsNamedDebuff(typeName)) continue;
            fShown++;
            var requirement = EnemyDebuffMath.Requirement(typeName);
            bool applies = EnemyDebuffMath.Applies(requirement, attribute, attackKind, human, deity, demon);
            int state = typeName == "Collapse" ? collapseCount : typeName == "Domination" ? dominationLevel : 0;
            long value = EnemyDebuffMath.Value(typeName, effect.SkillValue1, effect.SkillValue2,
                effect.SkillValue3, effect.SkillValue5, state);
            string stateText = typeName == "Collapse" ? $"；崩壞累積 {collapseCount}" :
                typeName == "Domination" ? $"；支配 Lv.{Math.Max(1, dominationLevel)}" : "";
            sb.AppendLine($"<color=#{(applies ? "84E0D7" : "7E879E")}><b>{fShown}. {EnemyDebuffMath.Name(typeName)}</b></color> {Presentation.Percent(value)} | {Presentation.CompactLifetime((int)effect.TimeType, effect.Time, effect.TimeMax)}" +
                (applies ? "" : "　<color=#9AA1B4>目前不適用</color>"));
            sb.AppendLine($"    來源：{SourceTracker.Resolve(target, effect)}");
            sb.AppendLine($"    條件：{EnemyDebuffMath.RequirementName(requirement)}{stateText}　→　{(applies ? "計入 F" : "不計入 F")}");
            if (effect.IsCopy) sb.AppendLine("    複製效果；名稱依目前保存的來源辨識。");
            if (effect.IsProhibited) sb.AppendLine("    遊戲標記：IsProhibited；保留顯示供核對。");
            if (allEffects || typeName is "Collapse" or "Domination")
                sb.AppendLine($"    參數：{effect.SkillValue1}, {effect.SkillValue2}, {effect.SkillValue3}, {effect.SkillValue4}, {effect.SkillValue5}　效果 ID {effect.ID}");
        }
        if (fShown == 0) sb.AppendLine("目前沒有標的、融解等命名 DEBUFF。");

        int conditionalPassive = 0, damageCut = 0;
        if (effects != null) for (int i = 0; i < effects.Count; i++)
        {
            string typeName = effects[i].Type.ToString();
            if (typeName.StartsWith("PassiveDamageDown", StringComparison.Ordinal) &&
                !reductionPointers.Contains(effects[i].Pointer)) conditionalPassive++;
            if (typeName.Contains("DamageCut", StringComparison.Ordinal)) damageCut++;
        }
        if (conditionalPassive > 0) sb.AppendLine($"\n<color=#FFD579>另偵測到 {conditionalPassive} 筆依攻擊狀態判定的敵方被動減傷；不提前混入共通 E。</color>");
        if (damageCut > 0) sb.AppendLine($"<color=#FFD579>另偵測到 {damageCut} 筆 DamageCut；這是不同乘區，不列入 E 或 F。</color>");

        if (allEffects && effects != null)
        {
            sb.AppendLine("\n<b>其他效果／原始參數</b>");
            int other = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (increasePointers.Contains(effect.Pointer) || reductionPointers.Contains(effect.Pointer) ||
                    EnemyDebuffMath.IsNamedDebuff(effect.Type.ToString())) continue;
                other++;
                sb.AppendLine($"{other}. {Presentation.CompactEffectName(effect.Type.ToString())} | {Presentation.CompactLifetime((int)effect.TimeType, effect.Time, effect.TimeMax)}");
                sb.AppendLine($"    來源：{SourceTracker.Resolve(target, effect)}　參數：{effect.SkillValue1}, {effect.SkillValue2}, {effect.SkillValue3}, {effect.SkillValue4}, {effect.SkillValue5}　ID {effect.ID}");
            }
            if (other == 0) sb.AppendLine("無其他效果。");
        }

        sb.AppendLine("\n數值每 0.25 秒更新。F 依開窗時固定的我方角色判斷屬性、種族與攻擊類型。\n同名命名 DEBUFF 由遊戲管理覆蓋／延長；DamageCut 保持在其他乘區。");
        return new BattleSnapshot($"敵方 DEBUFF 明細 · 敵 {enemyIndex + 1} {targetName}",
            $"敵 {enemyIndex + 1}  <b>{targetName}</b>     E共通  <b>{Presentation.RatePercent(eRate)}</b>     F  <b>{Presentation.RatePercent(fRate)}</b>     攻擊者  {attackerName}", sb.ToString());
    }

}
