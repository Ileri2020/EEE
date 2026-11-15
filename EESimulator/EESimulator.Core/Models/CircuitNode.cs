using System;
using System.Collections.Generic;

namespace EESimulator.Core.Models;

/// <summary>
/// Root document node representing a complete circuit schematic project.
/// </summary>
public class CircuitNode : RootNode
{
    public const double MinimumLogicalGridSpacing = 2;
    public const double LogicalGridSpacingStep = 20;

    private double _logicalGridRowSpacing = MinimumLogicalGridSpacing;
    private double _logicalGridColumnSpacing = MinimumLogicalGridSpacing;

    public string Title { get; set; } = "Untitled Circuit";

    public string Description { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0.0";

    public double LogicalGridRowSpacing
    {
        get => _logicalGridRowSpacing;
        set => SetLogicalGridSpacing(ref _logicalGridRowSpacing, value, nameof(LogicalGridRowSpacing));
    }

    public double LogicalGridColumnSpacing
    {
        get => _logicalGridColumnSpacing;
        set => SetLogicalGridSpacing(ref _logicalGridColumnSpacing, value, nameof(LogicalGridColumnSpacing));
    }

    public long StructureVersion { get; private set; } = 1;

    public void InvalidateStructure()
    {
        StructureVersion++;
    }

    public CircuitNode()
    {
        Name = "CircuitRoot";
        OnChildAdded += (_, e) =>
        {
            StructureVersion++;
            ObserveSubtree(e.Child);
        };
        OnChildRemoved += (_, e) =>
        {
            StructureVersion++;
            UnobserveSubtree(e.Child);
        };
        OnModified += (_, _) => StructureVersion++;
    }

    private void ObserveSubtree(RootNode node)
    {
        node.OnModified += OnDescendantModified;
        node.OnChildAdded += OnDescendantAdded;
        node.OnChildRemoved += OnDescendantRemoved;
        foreach (var child in node.Children)
            ObserveSubtree(child);
    }

    private void UnobserveSubtree(RootNode node)
    {
        node.OnModified -= OnDescendantModified;
        node.OnChildAdded -= OnDescendantAdded;
        node.OnChildRemoved -= OnDescendantRemoved;
        foreach (var child in node.Children)
            UnobserveSubtree(child);
    }

    private void OnDescendantModified(object? sender, NodeModifiedEventArgs e) => StructureVersion++;

    private void OnDescendantAdded(object? sender, NodeHierarchyChangedEventArgs e)
    {
        StructureVersion++;
        ObserveSubtree(e.Child);
    }

    private void OnDescendantRemoved(object? sender, NodeHierarchyChangedEventArgs e)
    {
        StructureVersion++;
        UnobserveSubtree(e.Child);
    }

    public IEnumerable<ComponentNode> Components => FindChildren<ComponentNode>();

    public IEnumerable<ConnectionNode> Connections => FindChildren<ConnectionNode>();

    public IEnumerable<GroupNode> Groups => FindChildren<GroupNode>();

    private void SetLogicalGridSpacing(ref double field, double value, string propertyName)
    {
        if (!double.IsFinite(value) || value < MinimumLogicalGridSpacing)
            throw new ArgumentOutOfRangeException(propertyName, $"Logical grid spacing must be at least {MinimumLogicalGridSpacing}.");

        if (field == value)
            return;

        double oldValue = field;
        field = value;
        NotifyModified(propertyName, oldValue, value);
    }
}
