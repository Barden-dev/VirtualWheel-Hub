using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace SimRacingHub.Avalonia.Adapters;

/// <summary>Publishes a complete language as one resource change. Updating
/// hundreds of individual resources causes quadratic binding invalidation.</summary>
internal static class LocalizationResources
{
    private sealed class State
    {
        public ResourceDictionary? Dictionary;
        public IReadOnlyDictionary<string, string> Strings = new Dictionary<string, string>();
    }
    private static readonly ConditionalWeakTable<global::Avalonia.Application, State> States = new();
    public static void Apply(global::Avalonia.Application app, IReadOnlyDictionary<string, string> strings)
    {
        var state = States.GetValue(app, _ => new State());
        if (state.Strings.Count == strings.Count && strings.All(p => state.Strings.TryGetValue(p.Key, out var old) && old == p.Value)) return;
        var next = new ResourceDictionary();
        foreach (var pair in strings) next.Add(pair.Key, pair.Value);
        var dictionaries = app.Resources.MergedDictionaries;
        var index = state.Dictionary == null ? -1 : dictionaries.IndexOf(state.Dictionary);
        if (index >= 0) dictionaries[index] = next;
        else dictionaries.Add(next);
        state.Dictionary = next;
        state.Strings = new Dictionary<string, string>(strings);
    }
}
