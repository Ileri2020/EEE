using System;
using System.Globalization;
using EESimulator.Core.Models;

namespace EESimulator.Commands.Commands;

public sealed class SetLogicalGridSpacingCommand : ICommand
{
    private readonly double _oldRowSpacing;
    private readonly double _oldColumnSpacing;

    public Guid Id { get; } = Guid.NewGuid();
    public string Description => $"Set logical grid spacing to {RowSpacing:G}/{ColumnSpacing:G}";
    public CommandOrigin Origin { get; set; } = CommandOrigin.Schematic;
    public CircuitNode Circuit { get; }
    public double RowSpacing { get; }
    public double ColumnSpacing { get; }

    public SetLogicalGridSpacingCommand(CircuitNode circuit, double rowSpacing, double columnSpacing)
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        if (!double.IsFinite(rowSpacing) || rowSpacing < CircuitNode.MinimumLogicalGridSpacing)
            throw new ArgumentOutOfRangeException(nameof(rowSpacing));
        if (!double.IsFinite(columnSpacing) || columnSpacing < CircuitNode.MinimumLogicalGridSpacing)
            throw new ArgumentOutOfRangeException(nameof(columnSpacing));

        _oldRowSpacing = circuit.LogicalGridRowSpacing;
        _oldColumnSpacing = circuit.LogicalGridColumnSpacing;
        RowSpacing = rowSpacing;
        ColumnSpacing = columnSpacing;
    }

    public bool Execute()
    {
        if (Circuit.LogicalGridRowSpacing == RowSpacing &&
            Circuit.LogicalGridColumnSpacing == ColumnSpacing)
            return false;

        ApplySpacing(RowSpacing, ColumnSpacing);
        return true;
    }

    public bool Undo()
    {
        ApplySpacing(_oldRowSpacing, _oldColumnSpacing);
        return true;
    }

    public string ToPythonScript() =>
        $"eesim.set_logical_grid_spacing({RowSpacing.ToString("G17", CultureInfo.InvariantCulture)}, " +
        $"{ColumnSpacing.ToString("G17", CultureInfo.InvariantCulture)})";

    private void ApplySpacing(double rowSpacing, double columnSpacing)
    {
        Circuit.LogicalGridRowSpacing = rowSpacing;
        Circuit.LogicalGridColumnSpacing = columnSpacing;

        foreach (LogicalGridPlacement placement in LogicalGridSchematicLayout.Calculate(Circuit).Placements)
        {
            placement.Component.X = placement.X;
            placement.Component.Y = placement.Y;
            placement.Component.NotifyModified(nameof(ComponentNode.X));
            placement.Component.NotifyModified(nameof(ComponentNode.Y));
        }
    }
}
