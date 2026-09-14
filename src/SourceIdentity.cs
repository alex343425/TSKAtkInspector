namespace TSKAtkInspector;

// effect_id is NOT a globally unique skill identity. Match original effect parameters
// only within the unit and skill family that are actually executing.
internal sealed record EffectIdentity(int Type, int Value1, int Value2, int Value3, int Value4, int Value5,
    int TimeType, int TimeMax, int Target, int Target1, int Target2, int Target3);

internal sealed record SourceCandidate(string Key, string Label, IReadOnlyList<EffectIdentity> Effects);

internal static class SourceIdentity
{
    internal static string? Match(EffectIdentity effect, IReadOnlyList<SourceCandidate> candidates, bool executingFamily)
    {
        // A singleton list in a witnessed Sister execution identifies the skill even
        // when CheckSkillApply has already overwritten its values/type for the target.
        if (executingFamily && candidates.Count == 1) return candidates[0].Label;
        var matches = candidates.Where(c => c.Effects.Contains(effect)).GroupBy(c => c.Key).ToArray();
        return matches.Length == 1 ? matches[0].First().Label : null;
    }
}

// Used by synchronous native calls. IDisposable restores nested scopes on both
// successful returns and exceptions through Harmony finalizers.
internal sealed class SourceScope<T> where T : class
{
    internal T? Current { get; private set; }
    internal IDisposable Enter(T? value)
    {
        var previous = Current;
        Current = value;
        return new Restore(() => Current = previous);
    }
    private sealed class Restore : IDisposable
    {
        private Action? _action;
        internal Restore(Action action) => _action = action;
        public void Dispose() { var action = _action; _action = null; action?.Invoke(); }
    }
}
