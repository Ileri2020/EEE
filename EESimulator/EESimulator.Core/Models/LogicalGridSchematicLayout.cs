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
    private const double MinimumTrackSize = 72;
    private const double MinimumWireCorridor = 84;
    private const double WireLaneSpacing = 14;
    private const double SameCellGap = 48;
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
        double wireCorridor = CalculateWireCorridor(circuit, logicalComponents);

        double columnCursor = Math.Max(OriginX, GetManualRightEdge(circuit, footprints) + wireCorridor);
        var columnCenters = new Dictionary<int, double>();
        int previousColumn = 0;
        foreach (int column in componentsByColumn.Keys.OrderBy(value => value))
        {
            int emptyColumns = previousColumn == 0 ? column - 1 : column - previousColumn - 1;
            columnCursor += (double)emptyColumns * (MinimumTrackSize + wireCorridor);
            double componentWidth = componentsByColumn[column]
                .Max(component => footprints[component].HalfWidth * 2);
            double columnWidth = Math.Max(MinimumTrackSize, componentWidth);
            columnCenters[column] = columnCursor + columnWidth / 2;
            columnCursor += columnWidth + wireCorridor;
            previousColumn = column;
        }

        var rowHeights = new Dictionary<int, double>();
        var rowCenters = new Dictionary<int, double>();
        foreach (var row in cellsByRow)
        {
            double tallestCell = row.Value.Max(cell =>
                cell.Value.Sum(component => footprints[component].HalfHeight * 2) +
                Math.Max(0, cell.Value.Count - 1) * SameCellGap);
            rowHeights[row.Key] = Math.Max(MinimumTrackSize, tallestCell);
        }

        double rowCursor = OriginY;
        int previousRow = 0;
        foreach (int row in rowHeights.Keys.OrderBy(value => value))
        {
            int emptyRows = previousRow == 0 ? row - 1 : row - previousRow - 1;
            rowCursor += (double)emptyRows * (MinimumTrackSize + wireCorridor);
            rowCenters[row] = rowCursor + rowHeights[row] / 2;
            rowCursor += rowHeights[row] + wireCorridor;
            previousRow = row;
        }

        var placements = new List<LogicalGridPlacement>(logicalComponents.Count);
        foreach (var cell in cells.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
        {
            var stack = cell.Value.OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase).ToList();
            double stackHeight = stack.Sum(component => footprints[component].HalfHeight * 2) +
                Math.Max(0, stack.Count - 1) * SameCellGap;
            double cursorY = rowCenters[cell.Key.Row] - stackHeight / 2;
            foreach (var component in stack)
            {
                Footprint footprint = footprints[component];
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

    private static double CalculateWireCorridor(CircuitNode circuit, IReadOnlyCollection<ComponentNode> logicalComponents)
    {
        var logicalById = logicalComponents.ToDictionary(component => component.Id);
        var columnEvents = new Dictionary<int, int>();
        var rowEvents = new Dictionary<int, int>();
        var pinFanOut = new Dictionary<(Guid ComponentId, string PinId), int>();
        foreach (ConnectionNode connection in circuit.FindChildren<ConnectionNode>())
        {
            Increment(pinFanOut, (connection.FromComponentId, connection.FromPin));
            Increment(pinFanOut, (connection.ToComponentId, connection.ToPin));
            if (!logicalById.TryGetValue(connection.FromComponentId, out ComponentNode? from) ||
                !logicalById.TryGetValue(connection.ToComponentId, out ComponentNode? to))
                continue;

            AddCrossing(columnEvents, from.LogicalColumn!.Value, to.LogicalColumn!.Value);
            AddCrossing(rowEvents, from.LogicalRow!.Value, to.LogicalRow!.Value);
        }

        int maxTraffic = Math.Max(MaximumCrossings(columnEvents), MaximumCrossings(rowEvents));
        int maxFanOut = pinFanOut.Values.DefaultIfEmpty(0).Max();
        int laneCount = Math.Max(maxTraffic, maxFanOut);
        return Math.Max(MinimumWireCorridor, 32 + laneCount * WireLaneSpacing);
    }

    private static void Increment(Dictionary<(Guid ComponentId, string PinId), int> counts,
        (Guid ComponentId, string PinId) pin)
    {
        counts[pin] = counts.GetValueOrDefault(pin) + 1;
    }

    private static void AddCrossing(Dictionary<int, int> events, int firstTrack, int secondTrack)
    {
        if (firstTrack == secondTrack) return;
        int lower = Math.Min(firstTrack, secondTrack);
        int upper = Math.Max(firstTrack, secondTrack);
        events[lower] = events.GetValueOrDefault(lower) + 1;
        events[upper] = events.GetValueOrDefault(upper) - 1;
    }

    private static int MaximumCrossings(Dictionary<int, int> events)
    {
        int active = 0;
        int maximum = 0;
        foreach (var item in events.OrderBy(pair => pair.Key))
        {
            active += item.Value;
            maximum = Math.Max(maximum, active);
        }
        return maximum;
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
        return double.IsNegativeInfinity(right) ? OriginX - MinimumWireCorridor : right;
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
