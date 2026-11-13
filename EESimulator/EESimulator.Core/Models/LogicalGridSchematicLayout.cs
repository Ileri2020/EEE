using System;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.General;

namespace EESimulator.Core.Models;

public readonly record struct LogicalGridPlacement(ComponentNode Component, double X, double Y);

public readonly record struct SchematicLayoutCollision(ComponentNode First, ComponentNode Second);

public sealed class LogicalGridLayoutResult
{
    public IReadOnlyList<LogicalGridPlacement> Placements { get; }
    public IReadOnlyList<SchematicLayoutCollision> UnresolvedCollisions { get; }

    internal LogicalGridLayoutResult(
        IReadOnlyList<LogicalGridPlacement> placements,
        IReadOnlyList<SchematicLayoutCollision> unresolvedCollisions)
    {
        Placements = placements;
        UnresolvedCollisions = unresolvedCollisions;
    }
}

/// <summary>Maps 1-based logical cells to physical centers using component and pin footprints.</summary>
public static class LogicalGridSchematicLayout
{
    private const double OriginX = 96;
    private const double OriginY = 80;
    private const double WireCorridor = 20;
    private const double SameCellGap = 20;
    private const double ReferenceLabelHeight = 18;

    private readonly record struct Footprint(double HalfWidth, double HalfHeight)
    {
        public Footprint Rotate(double degrees)
        {
            double radians = degrees * Math.PI / 180;
            double cosine = Math.Abs(Math.Cos(radians));
            double sine = Math.Abs(Math.Sin(radians));
            return new Footprint(
                HalfWidth * cosine + HalfHeight * sine,
                HalfWidth * sine + HalfHeight * cosine);
        }

        public double Left(double centerX) => centerX - HalfWidth;
        public double Right(double centerX) => centerX + HalfWidth;
        public double Top(double centerY) => centerY - HalfHeight;
        public double Bottom(double centerY) => centerY + HalfHeight;
    }

    private readonly record struct PositionedBounds(ComponentNode Component, Footprint Bounds, double X, double Y, bool Logical);

    public static LogicalGridLayoutResult Calculate(CircuitNode circuit)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        var logicalComponents = circuit.Components
            .Where(component => component.HasLogicalPlacement)
            .ToList();
        if (logicalComponents.Count == 0)
            return new LogicalGridLayoutResult(Array.Empty<LogicalGridPlacement>(), Array.Empty<SchematicLayoutCollision>());

        var footprints = circuit.Components.ToDictionary(component => component, GetFootprint);
        var cells = logicalComponents
            .GroupBy(component => (Row: component.LogicalRow!.Value, Column: component.LogicalColumn!.Value))
            .ToDictionary(group => group.Key, group => group.ToList());
        var cellsByRow = cells.GroupBy(pair => pair.Key.Row)
            .ToDictionary(group => group.Key, group => group.ToList());
        var componentsByColumn = logicalComponents.GroupBy(component => component.LogicalColumn!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        double columnCursor = Math.Max(OriginX, GetManualRightEdge(circuit, footprints) + WireCorridor);
        var columnCenters = new Dictionary<int, double>();
        int previousColumn = 0;
        foreach (int column in componentsByColumn.Keys.OrderBy(value => value))
        {
            int emptyColumns = previousColumn == 0 ? column - 1 : column - previousColumn - 1;
            columnCursor += (double)emptyColumns * WireCorridor;
            double componentWidth = componentsByColumn[column]
                .Max(component => footprints[component].Rotate(component.Rotation).HalfWidth * 2);
            double columnWidth = componentWidth;
            columnCenters[column] = columnCursor + columnWidth / 2;
            columnCursor += columnWidth + WireCorridor;
            previousColumn = column;
        }

        var rowHeights = new Dictionary<int, double>();
        var rowCenters = new Dictionary<int, double>();
        foreach (var row in cellsByRow)
        {
            double tallestCell = row.Value.Max(cell =>
                cell.Value.Sum(component => footprints[component].Rotate(component.Rotation).HalfHeight * 2) +
                Math.Max(0, cell.Value.Count - 1) * SameCellGap);
            rowHeights[row.Key] = tallestCell;
        }

        double rowCursor = OriginY;
        int previousRow = 0;
        foreach (int row in rowHeights.Keys.OrderBy(value => value))
        {
            int emptyRows = previousRow == 0 ? row - 1 : row - previousRow - 1;
            rowCursor += (double)emptyRows * WireCorridor;
            rowCenters[row] = rowCursor + rowHeights[row] / 2;
            rowCursor += rowHeights[row] + WireCorridor;
            previousRow = row;
        }

        var placements = new List<LogicalGridPlacement>(logicalComponents.Count);
        foreach (var cell in cells.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
        {
            var stack = cell.Value.OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase).ToList();
            double stackHeight = stack.Sum(component => footprints[component].Rotate(component.Rotation).HalfHeight * 2) +
                Math.Max(0, stack.Count - 1) * SameCellGap;
            double cursorY = rowCenters[cell.Key.Row] - stackHeight / 2;
            foreach (var component in stack)
            {
                Footprint footprint = footprints[component].Rotate(component.Rotation);
                double y = cursorY + footprint.HalfHeight;
                placements.Add(new LogicalGridPlacement(component, columnCenters[cell.Key.Column], y));
                cursorY += footprint.HalfHeight * 2 + SameCellGap;
            }
        }

        var placementsByComponent = placements.ToDictionary(placement => placement.Component);
        var positioned = circuit.Components
            .Where(component => component is not WireComponent && !component.ComponentType.Equals("Wire", StringComparison.OrdinalIgnoreCase))
            .Select(component =>
            {
                bool isLogical = placementsByComponent.TryGetValue(component, out LogicalGridPlacement placement);
                double x = isLogical ? placement.X : component.X;
                double y = isLogical ? placement.Y : component.Y;
                return new PositionedBounds(component, footprints[component].Rotate(component.Rotation), x, y, isLogical);
            })
            .OrderBy(item => item.Bounds.Left(item.X))
            .ToList();

        var collisions = FindCollisions(positioned);
        return new LogicalGridLayoutResult(placements, collisions);
    }

    public static (double HalfWidth, double HalfHeight) GetVisualHalfExtents(ComponentNode component)
    {
        Footprint footprint = GetFootprint(component).Rotate(component.Rotation);
        return (footprint.HalfWidth, footprint.HalfHeight);
    }

    private static double GetManualRightEdge(CircuitNode circuit, IReadOnlyDictionary<ComponentNode, Footprint> footprints)
    {
        double right = double.NegativeInfinity;
        foreach (var component in circuit.Components)
        {
            if (component.HasLogicalPlacement || component is WireComponent ||
                component.ComponentType.Equals("Wire", StringComparison.OrdinalIgnoreCase))
                continue;
            Footprint footprint = footprints[component].Rotate(component.Rotation);
            right = Math.Max(right, footprint.Right(component.X));
        }
        return double.IsNegativeInfinity(right) ? OriginX - WireCorridor : right;
    }

    private static Footprint GetFootprint(ComponentNode component)
    {
        AdaptiveComponentLayout adaptive = AdaptiveComponentLayout.Get(component);
        double halfWidth = Math.Max(adaptive.HalfWidth, 30 * Math.Max(component.ScaleX, 1));
        double halfHeight = Math.Max(adaptive.HalfHeight, 28 * Math.Max(component.ScaleY, 1));
        string reference = string.IsNullOrWhiteSpace(component.Reference) ? component.Name : component.Reference;
        halfWidth = Math.Max(halfWidth, reference.Length * 3.75 + 6);
        halfHeight += ReferenceLabelHeight;

        foreach (ComponentPin pin in component.Pins)
        {
            ComponentPinPosition position = adaptive.GetPinPosition(pin);
            double pinLabelWidth = pin.Name.Length * 6.5 + 8;
            halfWidth = Math.Max(halfWidth, Math.Abs(position.X) + pinLabelWidth / 2);
            halfHeight = Math.Max(halfHeight, Math.Abs(position.Y) + ReferenceLabelHeight + 8);
        }

        return new Footprint(halfWidth, halfHeight);
    }

    private static List<SchematicLayoutCollision> FindCollisions(List<PositionedBounds> items)
    {
        var collisions = new List<SchematicLayoutCollision>();
        var active = new List<PositionedBounds>();
        foreach (PositionedBounds current in items)
        {
            active.RemoveAll(item => item.Bounds.Right(item.X) <= current.Bounds.Left(current.X));
            foreach (PositionedBounds candidate in active)
            {
                if (!current.Logical && !candidate.Logical) continue;
                bool overlapsVertically = candidate.Bounds.Bottom(candidate.Y) > current.Bounds.Top(current.Y) &&
                    current.Bounds.Bottom(current.Y) > candidate.Bounds.Top(candidate.Y);
                if (overlapsVertically)
                    collisions.Add(new SchematicLayoutCollision(candidate.Component, current.Component));
            }
            active.Add(current);
        }
        return collisions;
    }

}
