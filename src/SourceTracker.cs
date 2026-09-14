using System.Reflection;
using HarmonyLib;
using TKS.Network.Domain;
using Skill = TSKBattleSkillData;
using NativeSkills = Il2CppSystem.Collections.Generic.List<TKS.Network.Domain.BattleStartSkillDatumEntity>;

namespace TSKAtkInspector;

internal sealed record SisterSource(TSKBattleUnit Unit, bool Active);
internal sealed record RecordedSource(string Label, int Type);

internal static class SourceTracker
{
    // Scope state is per thread because a nested passive/Sister effect can execute
    // while another skill is being resolved.
    [ThreadStatic] private static SourceScope<SisterSource>? _sister;
    [ThreadStatic] private static SourceScope<string>? _execution;
    private static readonly Dictionary<IntPtr, RecordedSource> Applied = new();
    private static readonly HashSet<string> ReportedErrors = new();
    private static SourceScope<SisterSource> Sister => _sister ??= new();
    private static SourceScope<string> Execution => _execution ??= new();

    internal static void Reset()
    {
        Applied.Clear(); ReportedErrors.Clear(); _sister = null; _execution = null;
    }

    internal static IDisposable EnterSister(TSKBattleSisterUnit sister, bool active) =>
        Sister.Enter(new SisterSource(sister.UnitData, active));

    internal static IDisposable EnterExecution(Skill data, TSKBattleNote? attacker, bool isSister,
        BattleStartSkillDatumEntity? entity, TSKBattleUnit? explicitUnit)
    {
        string? label = null;
        try
        {
            if (isSister)
            {
                var context = Sister.Current;
                // The native Execute unitData argument identifies the Sister. The
                // effect.Note/attacker may instead be an ordinary team member.
                // The enclosing TSKBattleSisterUnit is authoritative. In some paths
                // Execute's unitData is reused for another participant.
                var unit = context?.Unit ?? explicitUnit;
                if (unit != null)
                {
                    var family = context != null && context.Unit.Pointer == unit.Pointer ?
                        (context.Active ? "アクティブスキル" : "チームスキル") : "技能";
                    var owner = $"シスター：{Presentation.Safe(unit.CharacterName)}";
                    if (entity != null) label = SkillLabel(owner, family, entity);
                    else
                    {
                        var candidates = new List<SourceCandidate>();
                        bool witnessed = context != null && context.Unit.Pointer == unit.Pointer;
                        if (!witnessed || context!.Active) AddCandidates(candidates, unit.SisterActiveSkillData, owner, "アクティブスキル");
                        if (!witnessed || !context!.Active) AddCandidates(candidates, unit.SisterSupportSkillData, owner, "チームスキル");
                        label = SourceIdentity.Match(Identity(data), candidates, witnessed) ??
                            $"{owner} ／ {family}（技能名稱未能唯一辨識）";
                    }
                }
                else label = "シスター技能（未提供シスター角色）";
            }
            else if (entity != null)
            {
                var owner = attacker != null && attacker.UnitData != null ? Presentation.Safe(attacker.UnitData.CharacterName) : "來源角色未提供";
                // This is the actual executing skill entity, not a search by effect ID.
                label = SkillLabel(owner, "技能", entity);
            }
        }
        catch (Exception e) { Report(e); }
        // An unrecognized nested execution must shadow, not inherit, its parent's label.
        return Execution.Enter(label);
    }

    internal static void Record(Skill effect)
    {
        var key = effect.Pointer;
        // Remove stale records even if this invocation could not identify a source.
        Applied.Remove(key);
        if (Execution.Current is { } label) Applied[key] = new RecordedSource(label, (int)effect.Type);
    }

    internal static string Resolve(TSKBattleNote target, Skill effect)
    {
        if (Applied.TryGetValue(effect.Pointer, out var origin) && origin.Type == (int)effect.Type)
            return origin.Label;
        // For effects outside the observed execution path, a strict full-parameter
        // match is only a candidate; never present the target as a confirmed caster.
        var source = effect.Note;
        if (source == null || source.UnitData == null) return "未捕捉套用來源（效果 ID 不能單獨識別技能）";
        var unit = source.UnitData;
        var candidates = new List<SourceCandidate>();
        var owner = Presentation.Safe(unit.CharacterName);
        AddCandidates(candidates, unit.ExSkillData, owner, "EX");
        AddCandidates(candidates, unit.EquipSkillData, owner, "裝備／被動");
        AddCandidates(candidates, unit.SpecificExSkillData, owner, "特殊 EX");
        AddCandidates(candidates, unit.EnemySkillData, owner, "敵方技能");
        var match = SourceIdentity.Match(Identity(effect), candidates, false);
        return match != null ? "未捕捉施放；參數相符候選：" + match :
            $"未捕捉套用來源（資料關聯角色：{owner}；效果 ID {effect.ID}）";
    }

    private static EffectIdentity Identity(Skill data) => new((int)data.Type,
        data.SkillValue1, data.SkillValue2, data.SkillValue3, data.SkillValue4, data.SkillValue5,
        (int)data.TimeType, data.TimeMax, (int)data.SkillTarget, data.SkillTargetValue, data.SkillTargetValue2, data.SkillTargetValue3);

    private static EffectIdentity Identity(BattleStartSkillEffectEntity data) => new(data.effect_type,
        data.effect_value_1, data.effect_value_2, data.effect_value_3, data.effect_value_4, data.effect_value_5,
        data.effect_time_type, data.effect_time, data.effect_target_type, data.effect_target_value, data.effect_target_value_2, data.effect_target_value_3);

    private static string SkillLabel(string owner, string family, BattleStartSkillDatumEntity data) =>
        $"{owner} ／ {family} · {Presentation.Safe(data.skill_name)}" + (data.lv > 0 ? $" Lv.{data.lv}" : "");

    private static void AddCandidates(List<SourceCandidate> candidates, NativeSkills? list, string owner, string family)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            var skill = list[i];
            var effects = new List<EffectIdentity>();
            var definitions = skill.skill_effect;
            if (definitions != null) for (int j = 0; j < definitions.Length; j++) effects.Add(Identity(definitions[j]));
            candidates.Add(new SourceCandidate($"{owner}/{family}/{skill.Pointer}", SkillLabel(owner, family, skill), effects));
        }
    }

    internal static void Report(Exception e)
    {
        if (ReportedErrors.Add(e.Message)) Plugin.Logger.LogWarning("Buff source tracking: " + e);
    }
}

[HarmonyPatch]
internal static class SisterSourcePatch
{
    internal static readonly string[] Methods = { nameof(TSKBattleSisterUnit.ExecuteActiveSkill),
        nameof(TSKBattleSisterUnit.ExecuteSupportSkill), nameof(TSKBattleSisterUnit.ExecuteSupportSkillTargetEnemy) };

    [HarmonyTargetMethods]
    static IEnumerable<MethodBase> TargetMethods() => Methods.Select(name => AccessTools.Method(typeof(TSKBattleSisterUnit), name));

    static void Prefix(TSKBattleSisterUnit __instance, MethodBase __originalMethod, out IDisposable? __state)
    {
        __state = null;
        try { __state = SourceTracker.EnterSister(__instance, __originalMethod.Name == nameof(TSKBattleSisterUnit.ExecuteActiveSkill)); }
        catch (Exception e) { SourceTracker.Report(e); }
    }
    static void Finalizer(IDisposable? __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(TSKBattleSkillManager), nameof(TSKBattleSkillManager.Execute))]
internal static class ExecutingSourcePatch
{
    static void Prefix(Skill __0, TSKBattleNote __1, bool __13, BattleStartSkillDatumEntity __14, TSKBattleUnit __17, out IDisposable? __state)
    {
        __state = null;
        try { __state = SourceTracker.EnterExecution(__0, __1, __13, __14, __17); }
        catch (Exception e) { SourceTracker.Report(e); }
    }
    static void Finalizer(IDisposable? __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(TSKBattleNote), nameof(TSKBattleNote.SetSkillEffect))]
internal static class AppliedSourcePatch
{
    // SetSkillEffect stores the supplied effect instance in the target's list. Capture
    // here, after Execute has made any per-target copies, while its source scope is live.
    static void Prefix(TSKBattleNote __instance, Skill __0)
    {
        try { SourceTracker.Record(__0); }
        catch (Exception e) { SourceTracker.Report(e); }
    }
}

[HarmonyPatch(typeof(TSKBattleSkillData), nameof(TSKBattleSkillData.OverWrite), new[] { typeof(TSKBattleSkillData) })]
internal static class OverwrittenSourcePatch
{
    // SetSkillEffect can refresh an existing list object instead of inserting the
    // incoming instance. Transfer the active execution source to that stored object.
    static void Prefix(TSKBattleSkillData __instance)
    {
        try { SourceTracker.Record(__instance); }
        catch (Exception e) { SourceTracker.Report(e); }
    }
}
