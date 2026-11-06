using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EESimulator.Core.Models;

public readonly record struct ComponentPinPosition(double X, double Y);

public sealed class AdaptiveComponentLayout
{
    private const double MinimumPinSpacing = 12.0;
    private const double CoincidentPinTolerance = 1.0;
    private static readonly ConditionalWeakTable<ComponentNode, LayoutCache> Cache = new();

    private readonly Dictionary<ComponentPin, ComponentPinPosition> _pinPositions;

    public double ScaleFactor { get; }
    public double HalfWidth { get; }
    public double HalfHeight { get; }

    private AdaptiveComponentLayout(
        ComponentNode component,
        ComponentPinPosition[] positions,
        double scaleFactor)
    {
        ScaleFactor = scaleFactor;
        _pinPositions = new Dictionary<ComponentPin, ComponentPinPosition>(component.Pins.Count);

        double maxX = 0;
        double maxY = 0;
        for (int index = 0; index < component.Pins.Count; index++)
        {
            ComponentPinPosition position = new(
                positions[index].X * scaleFactor,
                positions[index].Y * scaleFactor);
            _pinPositions[component.Pins[index]] = position;
            maxX = Math.Max(maxX, Math.Abs(position.X));
            maxY = Math.Max(maxY, Math.Abs(position.Y));
        }

        double scaleX = component.ScaleX > 0 ? component.ScaleX : 1.0;
        double scaleY = component.ScaleY > 0 ? component.ScaleY : 1.0;
        HalfWidth = Math.Max(26.0 * scaleX * scaleFactor, maxX + 6.0);
        HalfHeight = Math.Max(26.0 * scaleY * scaleFactor, maxY + 6.0);
    }

    public static AdaptiveComponentLayout Get(ComponentNode component)
    {
        ArgumentNullException.ThrowIfNull(component);
        LayoutCache entry = Cache.GetValue(component, static _ => new LayoutCache());
        lock (entry)
        {
            if (entry.Matches(component))
                return entry.Layout!;

            entry.Update(component, Create(component));
            return entry.Layout!;
        }
    }

    public ComponentPinPosition GetPinPosition(ComponentPin pin)
    {
        if (_pinPositions.TryGetValue(pin, out ComponentPinPosition position))
            return position;
        return new ComponentPinPosition(pin.RelativeX, pin.RelativeY);
    }

    private static AdaptiveComponentLayout Create(ComponentNode component)
    {
        double scaleX = component.ScaleX > 0 ? component.ScaleX : 1.0;
        double scaleY = component.ScaleY > 0 ? component.ScaleY : 1.0;
        var positions = new ComponentPinPosition[component.Pins.Count];

        for (int index = 0; index < positions.Length; index++)
        {
            ComponentPin pin = component.Pins[index];
            positions[index] = new ComponentPinPosition(pin.RelativeX * scaleX, pin.RelativeY * scaleY);
        }

        bool pinsWereOverlapping = SpreadOverlappingPins(positions);
        SeparateResidualOverlaps(positions);

        double minimumDistance = double.PositiveInfinity;
        for (int first = 0; first < positions.Length; first++)
        {
            for (int second = first + 1; second < positions.Length; second++)
            {
                double dx = positions[first].X - positions[second].X;
                double dy = positions[first].Y - positions[second].Y;
                minimumDistance = Math.Min(minimumDistance, Math.Sqrt(dx * dx + dy * dy));
            }
        }

        double scaleFactor = double.IsPositiveInfinity(minimumDistance) || minimumDistance >= MinimumPinSpacing
            ? 1.0
            : MinimumPinSpacing / minimumDistance;
        if (pinsWereOverlapping)
            scaleFactor = Math.Max(scaleFactor, 1.2);

        return new AdaptiveComponentLayout(component, positions, scaleFactor);
    }

    private static bool SpreadOverlappingPins(ComponentPinPosition[] positions)
    {
        int count = positions.Length;
        if (count < 2)
            return false;

        int[] parents = new int[count];
        for (int index = 0; index < count; index++)
            parents[index] = index;

        for (int first = 0; first < count; first++)
        {
            for (int second = first + 1; second < count; second++)
            {
                double dx = positions[first].X - positions[second].X;
                double dy = positions[first].Y - positions[second].Y;
                if (dx * dx + dy * dy <= CoincidentPinTolerance * CoincidentPinTolerance)
                    Union(parents, first, second);
            }
        }

        var groups = new Dictionary<int, List<int>>();
        for (int index = 0; index < count; index++)
        {
            int root = Find(parents, index);
            if (!groups.TryGetValue(root, out List<int>? group))
            {
                group = new List<int>();
                groups.Add(root, group);
            }
            group.Add(index);
        }

        bool foundOverlappingPins = false;
        foreach (List<int> group in groups.Values)
        {
            if (group.Count < 2)
                continue;
            foundOverlappingPins = true;

            double centerX = 0;
            double centerY = 0;
            foreach (int index in group)
            {
                centerX += positions[index].X;
                centerY += positions[index].Y;
            }
            centerX /= group.Count;
            centerY /= group.Count;

            double radius = MinimumPinSpacing / (2.0 * Math.Sin(Math.PI / group.Count));
            for (int index = 0; index < group.Count; index++)
            {
                double angle = -Math.PI / 2.0 + 2.0 * Math.PI * index / group.Count;
                positions[group[index]] = new ComponentPinPosition(
                    centerX + radius * Math.Cos(angle),
                    centerY + radius * Math.Sin(angle));
            }
        }
        return foundOverlappingPins;
    }

    private static void SeparateResidualOverlaps(ComponentPinPosition[] positions)
    {
        const double minimumSourceDistance = 1.0;
        const double goldenAngle = 2.399963229728653;

        for (int index = 1; index < positions.Length; index++)
        {
            ComponentPinPosition origin = positions[index];
            bool overlaps = false;
            for (int other = 0; other < positions.Length; other++)
            {
                if (other == index) continue;
                double dx = origin.X - positions[other].X;
                double dy = origin.Y - positions[other].Y;
                if (dx * dx + dy * dy < minimumSourceDistance * minimumSourceDistance)
                {
                    overlaps = true;
                    break;
                }
            }
            if (!overlaps) continue;

            for (int attempt = 1; ; attempt++)
            {
                double radius = Math.Ceiling(Math.Sqrt(attempt));
                double angle = attempt * goldenAngle;
                var candidate = new ComponentPinPosition(
                    origin.X + radius * Math.Cos(angle),
                    origin.Y + radius * Math.Sin(angle));

                bool candidateIsClear = true;
                for (int other = 0; other < positions.Length; other++)
                {
                    if (other == index) continue;
                    double dx = candidate.X - positions[other].X;
                    double dy = candidate.Y - positions[other].Y;
                    if (dx * dx + dy * dy < minimumSourceDistance * minimumSourceDistance)
                    {
                        candidateIsClear = false;
                        break;
                    }
                }

                if (!candidateIsClear) continue;
                positions[index] = candidate;
                break;
            }
        }
    }

    private static int Find(int[] parents, int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }
        return index;
    }

    private static void Union(int[] parents, int first, int second)
    {
        int firstRoot = Find(parents, first);
        int secondRoot = Find(parents, second);
        if (firstRoot != secondRoot)
            parents[secondRoot] = firstRoot;
    }

    private sealed class LayoutCache
    {
        private ComponentPin[] _pins = Array.Empty<ComponentPin>();
        private double[] _coordinates = Array.Empty<double>();
        private double _scaleX;
        private double _scaleY;

        public AdaptiveComponentLayout? Layout { get; private set; }

        public bool Matches(ComponentNode component)
        {
            if (Layout == null
                || _scaleX != component.ScaleX
                || _scaleY != component.ScaleY
                || _pins.Length != component.Pins.Count)
                return false;

            for (int index = 0; index < _pins.Length; index++)
            {
                ComponentPin pin = component.Pins[index];
                if (!ReferenceEquals(_pins[index], pin)
                    || _coordinates[index * 2] != pin.RelativeX
                    || _coordinates[index * 2 + 1] != pin.RelativeY)
                    return false;
            }

            return true;
        }

        public void Update(ComponentNode component, AdaptiveComponentLayout layout)
        {
            _scaleX = component.ScaleX;
            _scaleY = component.ScaleY;
            _pins = component.Pins.ToArray();
            _coordinates = new double[_pins.Length * 2];
            for (int index = 0; index < _pins.Length; index++)
            {
                _coordinates[index * 2] = _pins[index].RelativeX;
                _coordinates[index * 2 + 1] = _pins[index].RelativeY;
            }
            Layout = layout;
        }
    }
}
