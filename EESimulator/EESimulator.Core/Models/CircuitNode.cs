using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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

    /// <summary>Simulation settings preserved from the project file (.eee).</summary>
    public object? SimulationSettings { get; set; }

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

    private long _structureVersion = 1;
    private long _configurationVersion = 1;
    private long _stateVersion = 1;
    private volatile List<ComponentNode> _components = new();

    public long StructureVersion => Interlocked.Read(ref _structureVersion);
    public long ConfigurationVersion => Interlocked.Read(ref _configurationVersion);
    public long StateVersion => Interlocked.Read(ref _stateVersion);
    public IReadOnlyList<ComponentNode> Components => _components;

    public void InvalidateStructure()
    {
        Interlocked.Increment(ref _structureVersion);
        Interlocked.Increment(ref _configurationVersion);
    }

    public CircuitNode()
    {
        Name = "CircuitRoot";
        OnChildAdded += (_, e) => OnHierarchyChanged(e.Child, added: true);
        OnChildRemoved += (_, e) => OnHierarchyChanged(e.Child, added: false);
        OnModified += (_, _) => Interlocked.Increment(ref _configurationVersion);
    }

    public void NotifySimulationStateChanged() => Interlocked.Increment(ref _stateVersion);

    private void OnHierarchyChanged(RootNode child, bool added)
    {
        Interlocked.Increment(ref _structureVersion);
        Interlocked.Increment(ref _configurationVersion);
        if (added)
            ObserveSubtree(child);
        else
            UnobserveSubtree(child);
        RefreshComponentCache();
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

    private void OnDescendantModified(object? sender, NodeModifiedEventArgs e)
    {
        if (IsRuntimeStateChange(sender, e.PropertyName))
        {
            Interlocked.Increment(ref _stateVersion);
            return;
        }

        Interlocked.Increment(ref _configurationVersion);
        if (AffectsTopology(sender, e.PropertyName))
            Interlocked.Increment(ref _structureVersion);
    }

    private void OnDescendantAdded(object? sender, NodeHierarchyChangedEventArgs e)
    {
        ObserveSubtree(e.Child);
        Interlocked.Increment(ref _structureVersion);
        Interlocked.Increment(ref _configurationVersion);
        RefreshComponentCache();
    }

    private void OnDescendantRemoved(object? sender, NodeHierarchyChangedEventArgs e)
    {
        UnobserveSubtree(e.Child);
        Interlocked.Increment(ref _structureVersion);
        Interlocked.Increment(ref _configurationVersion);
        RefreshComponentCache();
    }

    private void RefreshComponentCache() =>
        _components = FindChildren<ComponentNode>().ToList();

    private static bool IsRuntimeStateChange(object? sender, string propertyName) =>
        (propertyName is "Brightness" or "IsOn" or "IsPressed" or "ReadAddress" or "CarryOut") ||
        (sender is EESimulator.Core.Components.Base.ElectronicComponent &&
            (propertyName is "State" or "StateQ" or "Value" or "Data" or "DataReady" or "Pressed"));

    private static bool AffectsTopology(object? sender, string propertyName)
    {
        if (sender is ConnectionNode)
            return propertyName is "Resistance" or "FromComponentId" or "FromPin" or "ToComponentId" or "ToPin";
        if (sender is EESimulator.Core.Components.General.TunnelComponent)
            return propertyName == "TunnelLabel";
        if (sender is EESimulator.Core.Components.Electrical.GroundComponent)
            return propertyName == "GroundGroup";
        if (sender is EESimulator.Core.Components.General.ProbeComponent)
            return propertyName == "ReferenceGroundGroup";
        if (sender is EESimulator.Core.Components.General.LabelComponent)
            return propertyName is "Name" or "NetName";

        return propertyName is "Pins" or "PartNumber" or "ExposeSupplyPins" or
            "BitWidth" or "NumberOfInputs" or "InputCount" or "OutputCount" or
            "SelectBits" or "DataBusWidth" or "SelectWidth" or "GroupWidth" or "HasEnable" or
            "HasParallelLoad" or "HasOutputEnable" or "HasChipSelect" or
            "AddressBitWidth" or "DataBitWidth" or "PoleCount" or "LedCount" or
            "Rows" or "Columns" or "Length" or "Direction";
    }

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
