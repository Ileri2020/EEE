using System;
using System.Collections.Generic;

namespace EESimulator.Core.Models;

/// <summary>
/// Root document node representing a complete circuit schematic project.
/// </summary>
public class CircuitNode : RootNode
{
    public const double MinimumLogicalGridSpacing = 20;
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
        OnChildAdded += (_, _) => StructureVersion++;
        OnChildRemoved += (_, _) => StructureVersion++;
        OnModified += (_, _) => StructureVersion++;
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
