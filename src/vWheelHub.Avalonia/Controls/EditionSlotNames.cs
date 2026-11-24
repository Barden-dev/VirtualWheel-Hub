using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace SimRacingHub.Avalonia.Controls;

/// <summary>
/// Rebuilds a host view's NameScope so FindControl&lt;T&gt; also discovers named controls inside edition slot panels.
/// </summary>
internal static class EditionSlotNames
{
    private static readonly ConditionalWeakTable<Control, List<Control>> AttachedSlots = new();

    public static void Attach(Control host, params Control?[] slots)
    {
        var tracked = AttachedSlots.GetOrCreateValue(host);
        var added = false;
        foreach (var slot in slots)
        {
            if (slot == null || tracked.Contains(slot)) continue;
            tracked.Add(slot);
            added = true;
        }

        if (!added && tracked.Count == 0) return;

        var merged = new NameScope();
        RegisterTree(host, merged, enterRootAsScope: true);
        foreach (var slot in tracked)
        {
            RegisterTree(slot, merged, enterRootAsScope: true);
        }

        merged.Complete();
        NameScope.SetNameScope(host, merged);
    }

    private static void RegisterTree(ILogical node, NameScope target, bool enterRootAsScope)
    {
        if (node is StyledElement styled && !string.IsNullOrEmpty(styled.Name) && target.Find(styled.Name) == null)
        {
            target.Register(styled.Name, styled);
        }

        if (!enterRootAsScope && node is UserControl)
        {
            return;
        }

        foreach (var child in node.GetLogicalChildren())
        {
            RegisterTree(child, target, enterRootAsScope: false);
        }
    }
}
