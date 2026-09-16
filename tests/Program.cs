using Mono.Cecil;
using TSKAtkInspector;

int passed = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    passed++;
    Console.WriteLine("PASS " + description);
}
Check(Presentation.Lifetime(0, 40, 100) == "剩餘 40 / 100 CT", "remaining CT is not elapsed duration");
Check(Presentation.Lifetime(0, 0, 100).Contains("0 / 100"), "zero lifetime is not infinity");
Check(Presentation.Lifetime(0, -1, -1).Contains("特殊期限"), "unknown sentinel is not invented as infinity");
for (int i = 1; i <= 5; i++)
    Check(Presentation.Lifetime(i, 2, 3).Contains("2 / 3 次") && !Presentation.Lifetime(i, 2, 3).Contains(" CT"), "action counter unit " + i);
Check(Presentation.Lifetime(99, 3, 9).Contains("未知單位"), "future time types remain explicit");
Check(Presentation.CompactLifetime(0, 9992, 9999) == "9992 / 9999 CT", "compact lifetime removes the repeated label");
Check(Presentation.CompactEffectName("AtkUp") == "ATK", "compact ATK name");
Check(Presentation.CompactEffectName("AtkUpTypeOut") == "ATK · TypeOut", "compact conditional ATK name");
Check(Presentation.CompactEffectName("CriticalDamageUp") == "爆傷", "compact critical damage name");
Check(Presentation.CompactEffectName("CriticalDamageUpTypeRace") == "爆傷 · TypeRace", "compact conditional critical damage name");
Check(Presentation.DamageReceivedName("DmgUp", true) == "被傷增加", "compact received damage increase name");
Check(Presentation.DamageReceivedName("DamageDownTypeOut", false) == "被傷減輕 · TypeOut", "compact received damage reduction name");
Check(Presentation.ProductPercent(11760, 17000) == "199.92%", "E and F product keeps fractional percent");
Check(Presentation.Percent(12500) == "+125%", "basis points convert to percent");
Check(Presentation.Percent(-375) == "-3.75%", "debuff sign and fractional percent");
Check(Presentation.Percent(0) == "0%", "zero effective value stays zero");
Check(Presentation.Percent(10000000000L) == "+100000000%", "stack totals use 64-bit values");
Check(Presentation.RatePercent(15000) == "150%", "critical multiplier omits the buff sign");
Check(Presentation.EnemyDamageBadge(17000, 15000).Contains("E 170%") &&
      Presentation.EnemyDamageBadge(17000, 15000).Contains("F 150%"),
    "enemy timeline badge shows E above attacker-dependent F");
Check(BuffMath.BaseCriticalDamage == 15000, "critical damage base is the native 150 percent multiplier");
foreach (var name in new[] { "CriticalDamageUp", "CriticalDamageUpSister", "CriticalDamageUpAttributeOrAttackType", "SisterCriticalDamageUpRush" })
    Check(BuffMath.IsCriticalDamage(name), "critical damage category includes " + name);
foreach (var name in new[] { "CrtUp", "CrtUpType", "StunDamageUp", "AtkUp" })
    Check(!BuffMath.IsCriticalDamage(name), "critical chance and other damage categories exclude " + name);
Check(BuffMath.CriticalDamageValue("CriticalDamageUp", 8000, 1, 2, 3, 99) == 8000,
    "ordinary critical damage uses the game's effective value");
Check(BuffMath.CriticalDamageValue("SisterCriticalDamageUpRush", 0, 1000, 200, 5000, 10) == 3000,
    "Sister RUSH critical damage uses base plus current RUSH");
Check(BuffMath.CriticalDamageValue("SisterCriticalDamageUpRush", 0, 1000, 200, 5000, 99) == 5000,
    "Sister RUSH critical damage respects its cap");
Check(TimelineCtMath.ActualDistance(22, 2) == 24,
    "timeline CT converts vanilla overflow 2 at the screen edge to full distance 24");
Check(TimelineCtMath.ActualDistance(18, 0) == 18,
    "timeline CT remains visible inside the 22 CT lane");
Check(TimelineCtMath.ActualDistance(47, 0) == 47,
    "timeline CT preserves long off-screen distance instead of displaying 25");
Check(TimelineCtMath.ActualDistance(-1, 0) == 0,
    "timeline CT does not display a negative action-line distance");
Check(TimelineCtMath.EnemyDamageBadgeBelow(16),
    "enemy damage badge moves below at 16 CT");
Check(!TimelineCtMath.EnemyDamageBadgeBelow(12),
    "enemy damage badge threshold excludes STUN wait CT");
Check(!TimelineCtMath.EnemyDamageBadgeBelow(15),
    "enemy damage badge stays above below 16 CT");
Check(TimelineCtMath.EnemyDamageBadgeBelow(24),
    "enemy damage badge stays below for off-screen enemies");
Check(Presentation.InlineCriticalSummary(8000, 20000) == "Cri 80% CriDmg 200%",
    "standby critical summary shows chance before damage");
Check(ExGaugeMath.BattleRateValue("ExRateUp", 80, 999) == 80,
    "ordinary EX rate buff uses SkillValue1 like the native calculation");
Check(ExGaugeMath.BattleRateValue("ExRateDown", 30, 999) == -30,
    "EX rate reduction subtracts SkillValue1");
foreach (var name in new[] { "ExRateUpAllyCount", "ExRateUpAttrOut", "ExRateUpSkillCount", "ExRateUpAllyBit" })
    Check(ExGaugeMath.BattleRateValue(name, 1, 75) == 75,
        "conditional EX rate uses the live effective value: " + name);
Check(ExGaugeMath.BattleRateValue("ExRateUpSpecific", 60, 999) == 60,
    "specific EX rate buff uses SkillValue1");
Check(ExGaugeMath.BattleRateValue("AtkUp", 100, 100) == 0,
    "unrelated effects do not change EX rate");
Check(ExGaugeMath.AppliedBattleRate(450) == 300,
    "battle EX rate modifier respects the native 300 cap");
Check(ExGaugeMath.AppliedBattleRate(-40) == -40,
    "battle EX rate reduction is retained before the total floor");
Check(ExGaugeMath.CurrentRate(30, -80) == 0,
    "current EX rate cannot become negative");
Check(ExGaugeMath.Gain(0, false) == 27 && ExGaugeMath.Gain(0, true) == 34,
    "base normal and Charge EX gains round upward");
Check(ExGaugeMath.Gain(100, false) == 54 && ExGaugeMath.Gain(100, true) == 67,
    "boosted normal and Charge EX gains round upward");
Check(!Presentation.InlineExGaugeSummary(100, 0, 54, 67, false, false).Contains("/ 300"),
    "EX HUD omits the battle modifier when no effect is active");
Check(Presentation.InlineExGaugeSummary(100, 80, 75, 94, true, false).Contains("EX上昇 100 (80 / 300)") &&
      Presentation.InlineExGaugeSummary(100, 80, 75, 94, true, false).Contains("<b>75</b>") &&
      !Presentation.InlineExGaugeSummary(100, 80, 75, 94, true, false).Contains("<b>94</b>"),
    "normal EX gain is highlighted while the actor is not charging");
Check(Presentation.InlineExGaugeSummary(100, 80, 75, 94, true, true).Contains("<b>94</b>"),
    "Charge EX gain is highlighted while the actor is charging");
foreach (var name in new[] { "Target", "Purgatory", "Collapse", "Stigmata", "ExtremelyCold", "DemonicHindrance",
    "Electrification", "Crushing", "Laceration", "ReserveDark", "Domination", "Refereeing", "Chaos" })
    Check(EnemyDebuffMath.IsNamedDebuff(name), "named debuff category includes " + name);
Check(!EnemyDebuffMath.IsNamedDebuff("DmgUp"), "ordinary received damage increase stays in E");
Check(EnemyDebuffMath.Applies(NamedDebuffRequirement.Fire, 1, 2, false, false, false), "melt applies to fire attribute");
Check(!EnemyDebuffMath.Applies(NamedDebuffRequirement.Fire, 2, 1, true, true, true), "melt excludes non-fire attribute");
Check(EnemyDebuffMath.Applies(NamedDebuffRequirement.Slash, 1, 2, false, false, false), "laceration applies to slash attacks");
Check(EnemyDebuffMath.Applies(NamedDebuffRequirement.Deity, 1, 1, false, true, false), "judgment applies to deity race");
Check(EnemyDebuffMath.Value("Collapse", 1000, 1, 1000, 3, 0) == 1000, "collapse level 1 rate");
Check(EnemyDebuffMath.Value("Collapse", 1000, 1, 1000, 3, 1) == 2000, "collapse level 2 rate");
Check(EnemyDebuffMath.Value("Collapse", 1000, 1, 1000, 3, 99) == 3000, "collapse rate respects level cap");
Check(EnemyDebuffMath.Value("Domination", 1000, 500, 0, 0, 3) == 2000, "domination rate follows current level");
Check(Presentation.Safe("<color=red>A</color> <unknown>B</unknown>") == "A B", "game names cannot inject rich text");
var sisterEffect = new EffectIdentity(325, 2000, 0, 0, 0, 0, 0, 9999, 0, 0, 0, 0);
var sharedIdCandidates = new[] {
    new SourceCandidate("fiona/ex", "フィオナ ／ EX", new[] { sisterEffect }),
    new SourceCandidate("fiona/passive", "フィオナ ／ 裝備／被動", new[] { sisterEffect })
};
Check(SourceIdentity.Match(sisterEffect, sharedIdCandidates, false) == null,
    "ambiguous shared effects are not attributed to the target's skills");
var apolloTeam = new[] { new SourceCandidate("apollo/team", "シスター：アポロ ／ チームスキル · シャドウムーン Lv.10", new[] { sisterEffect }) };
Check(SourceIdentity.Match(sisterEffect, apolloTeam, true) == "シスター：アポロ ／ チームスキル · シャドウムーン Lv.10",
    "witnessed Sister team execution identifies Shadow Moon");
var sachiTeam = new[] { new SourceCandidate("sachi/team", "シスター：サチ ／ チームスキル · フォーチュンドロップ Lv.10", new[] { sisterEffect }) };
Check(SourceIdentity.Match(sisterEffect, sachiTeam, true)!.Contains("サチ"),
    "two equipped Sisters remain distinct");
var scope = new SourceScope<string>();
using (scope.Enter("outer"))
{
    Check(scope.Current == "outer", "source scope enters execution");
    using (scope.Enter(null)) Check(scope.Current == null, "unknown nested source shadows parent");
    Check(scope.Current == "outer", "nested source scope restores parent");
}
Check(scope.Current == null, "source scope clears after execution");

string root = args.Length == 0 ? Directory.GetCurrentDirectory() : args[0];
using var game = ModuleDefinition.ReadModule(Path.Combine(root, "BepInEx/interop/Assembly-CSharp.dll"));
using var plugin = ModuleDefinition.ReadModule(Path.Combine(root, "mods/AtkInspector/bin/Release/net6.0/TSKAtkInspector.dll"));
var seenCalls = new HashSet<string>();
foreach (var type in plugin.GetTypes())
foreach (var method in type.Methods.Where(m => m.HasBody))
foreach (var instruction in method.Body.Instructions)
{
    if (instruction.Operand is not MethodReference called || called.DeclaringType.Scope.Name != "Assembly-CSharp") continue;
    if (!seenCalls.Add(called.FullName)) continue;
    var allowed = called.Name.StartsWith("get_") || new[] { "GetBaseAttack", "GetBaseExGaugeRate", "GetAttack", "GetAtkUpSkillEffect", "GetModeChangeEffect", "GetFieldEffect", "GetSkillEffect", "GetCritical", "CheckRace" }.Contains(called.Name);
    Check(allowed, "game data read-only: " + called.DeclaringType.Name + "." + called.Name);
    var targetType = game.GetType(called.DeclaringType.GetElementType().FullName);
    Check(targetType != null && targetType.Methods.Any(m => m.Name == called.Name && m.Parameters.Count == called.Parameters.Count), "installed game contains " + called.DeclaringType.Name + "." + called.Name);
}
var sisterType = game.GetType("TSKBattleSisterUnit");
foreach (var name in new[] { "ExecuteActiveSkill", "ExecuteSupportSkill", "ExecuteSupportSkillTargetEnemy" })
    Check(sisterType != null && sisterType.Methods.Count(m => m.Name == name) == 1,
        "Sister source hook is unambiguous: TSKBattleSisterUnit." + name);
var skillDataType = game.GetType("TSKBattleSkillData");
var skillType = game.GetType("TSKBattleSkillData/SkillType");
foreach (var name in new[] { "CriticalDamageUp", "CriticalDamageUpSister", "SisterCriticalDamageUpRush",
    "CriticalDamageUpDesignateSkill", "CriticalDamageUpRushRate", "CriticalDamageUpAttributeOrAttackType" })
    Check(skillType != null && skillType.Fields.Any(f => f.Name == name && f.HasConstant),
        "installed game contains critical damage effect " + name);
foreach (var name in new[] { "ExRateUp", "ExRateDown", "ExRateUpAllyCount", "ExRateUpAttrOut",
    "ExRateUpSkillCount", "ExRateUpSpecific", "ExRateUpAllyBit" })
    Check(skillType != null && skillType.Fields.Any(f => f.Name == name && f.HasConstant) && ExGaugeMath.IsBattleRateEffect(name),
        "installed battle EX rate effect is covered: " + name);
var nativeCriticalNames = skillType!.Fields.Where(f => f.HasConstant && f.Name.Contains("CriticalDamageUp") && !f.Name.Contains("Down"))
    .Select(f => f.Name).ToArray();
Check(nativeCriticalNames.Length == 17, "all 17 current native critical damage effect types were enumerated");
foreach (var name in nativeCriticalNames)
    Check(BuffMath.IsCriticalDamage(name), "critical damage category covers installed effect " + name);
var nativeNamedDebuffs = new[] { "Target", "Purgatory", "Collapse", "Stigmata", "ExtremelyCold", "DemonicHindrance",
    "Electrification", "Crushing", "Laceration", "ReserveDark", "Domination", "Refereeing", "Chaos" };
foreach (var name in nativeNamedDebuffs)
    Check(skillType.Fields.Any(f => f.Name == name && f.HasConstant) && EnemyDebuffMath.IsNamedDebuff(name),
        "installed named damage debuff is covered: " + name);
Check(skillDataType != null && skillDataType.Methods.Count(m => m.Name == "OverWrite" &&
    m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "TSKBattleSkillData") == 1,
    "overwritten effect source hook selects the TSKBattleSkillData overload");
foreach (var type in plugin.GetTypes())
{
    foreach (var patch in type.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch" && a.ConstructorArguments.Count == 2))
    {
        var target = (TypeReference)patch.ConstructorArguments[0].Value;
        string name = (string)patch.ConstructorArguments[1].Value;
        var targetType = game.GetType(target.FullName);
        Check(targetType != null && targetType.Methods.Count(m => m.Name == name) == 1, "Harmony target is unambiguous: " + target.FullName + "." + name);
    }
}
Check(plugin.AssemblyReferences.Any(a => a.Name == "BepInEx.Unity.IL2CPP"), "plugin targets installed IL2CPP loader");
Check(!plugin.AssemblyReferences.Any(a => a.Name == "TSKHook"), "plugin can coexist without replacing TSKHook");
Console.WriteLine($"{passed} checks passed. These are offline checks; in-game interaction still requires a live battle.");
