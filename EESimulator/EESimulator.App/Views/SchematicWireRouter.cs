using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Graphics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

/// <summary>Creates short schematic paths that avoid component bodies.</summary>
internal static class SchematicWireRouter
{
    private const float GridStep = 10f;
    private const float ObstaclePadding = 2f;
    private const float PinLeadLength = 14f;
    private const float WireLaneClearance = 7f;

    private readonly record struct GridPoint(int X, int Y);
    private readonly record struct Obstacle(float Left, float Top, float Right, float Bottom);

    public static PointF GetPinWorldPosition(ComponentNode component, ComponentPin pin)
    {
        ComponentPinPosition position = AdaptiveComponentLayout.Get(component).GetPinPosition(pin);
        return TransformLocal(component, (float)position.X, (float)position.Y);
    }

    public static List<PointF> RouteConnection(
        ComponentNode fromComponent,
        ComponentPin fromPin,
        ComponentNode toComponent,
        ComponentPin toPin,
        IEnumerable<ComponentNode> allComponents,
        IReadOnlyList<Point2D>? routingPoints = null,
        IReadOnlyList<List<PointF>>? priorPaths = null)
    {
        var start = GetPinWorldPosition(fromComponent, fromPin);
        var end = GetPinWorldPosition(toComponent, toPin);
        priorPaths ??= Array.Empty<List<PointF>>();
        if (routingPoints is { Count: > 0 })
        {
            var points = new List<PointF> { start };
            points.AddRange(routingPoints.Select(point => new PointF((float)point.X, (float)point.Y)));
            points.Add(end);
            var routed = new List<List<PointF>>();
            for (int index = 0; index < points.Count - 1; index++)
            {
                var excluded = index == 0
                    ? new[] { fromComponent }
                    : index == points.Count - 2 ? new[] { toComponent } : Array.Empty<ComponentNode>();
                routed.Add(FindOrthogonalPath(
                    points[index], points[index + 1], GetObstacles(allComponents, excluded), priorPaths));
            }
            var pathWithPrior = CombinePaths(routed.ToArray());
            if (pathWithPrior.Count >= 2)
                return pathWithPrior;

            // Fallback 1: Route without prior paths to avoid congestion drops
            routed.Clear();
            for (int index = 0; index < points.Count - 1; index++)
            {
                var excluded = index == 0
                    ? new[] { fromComponent }
                    : index == points.Count - 2 ? new[] { toComponent } : Array.Empty<ComponentNode>();
                routed.Add(FindOrthogonalPath(
                    points[index], points[index + 1], GetObstacles(allComponents, excluded)));
            }
            var pathWithoutPrior = CombinePaths(routed.ToArray());
            if (pathWithoutPrior.Count >= 2)
                return pathWithoutPrior;

            // Fallback 2: Direct simplified points
            return Simplify(points);
        }
        var startDirection = GetPinOutwardDirection(fromComponent, fromPin, end);
        var endDirection = GetPinOutwardDirection(toComponent, toPin, start);
        var startLead = GetClearPinLead(fromComponent, start, startDirection);
        var endLead = GetClearPinLead(toComponent, end, endDirection);
        var sourceLeadObstacles = GetObstacles(allComponents, fromComponent);
        var routeObstacles = GetObstacles(allComponents, fromComponent, toComponent);
        var targetLeadObstacles = GetObstacles(allComponents, toComponent);
        if (IsRouteClear(new[] { start, end }, routeObstacles, priorPaths))
            return new List<PointF> { start, end };

        var path = CombinePaths(
            FindOrthogonalPath(start, startLead, sourceLeadObstacles),
            FindOrthogonalPath(startLead, endLead, routeObstacles, priorPaths),
            FindOrthogonalPath(endLead, end, targetLeadObstacles));

        if (path.Count >= 2)
            return ShortenRoute(path, routeObstacles, priorPaths);

        // Fallback 1: Allow lane-sharing with prior paths
        path = CombinePaths(
            FindOrthogonalPath(start, startLead, sourceLeadObstacles),
            FindOrthogonalPath(startLead, endLead, routeObstacles),
            FindOrthogonalPath(endLead, end, targetLeadObstacles));

        if (path.Count >= 2)
            return ShortenRoute(path, routeObstacles, Array.Empty<List<PointF>>());

        // Fallback 2: Direct connection without leads
        path = FindOrthogonalPath(start, end, routeObstacles);
        if (path.Count >= 2)
            return ShortenRoute(path, routeObstacles, Array.Empty<List<PointF>>());

        // Fallback 3: Guaranteed orthogonal elbow route so wire connections are never dropped or hidden
        float midX = (start.X + end.X) / 2f;
        return Simplify(new List<PointF>
        {
            start,
            startLead,
            new(midX, startLead.Y),
            new(midX, endLead.Y),
            endLead,
            end
        });
    }

    public static List<PointF> RouteWireComponent(
        WireComponent wire,
        IEnumerable<ComponentNode> allComponents,
        IReadOnlyList<List<PointF>>? priorPaths = null)
    {
        var startPin = wire.Pins.FirstOrDefault();
        var endPin = wire.Pins.Skip(1).FirstOrDefault();
        if (startPin == null || endPin == null)
            return new List<PointF> { new((float)wire.X, (float)wire.Y), new((float)wire.X + 40, (float)wire.Y) };

        var start = GetPinWorldPosition(wire, startPin);
        var end = GetPinWorldPosition(wire, endPin);
        priorPaths ??= Array.Empty<List<PointF>>();
        var wireObstacles = GetObstacles(allComponents, wire);
        if (IsRouteClear(new[] { start, end }, wireObstacles, priorPaths))
            return new List<PointF> { start, end };

        var startDirection = GetPinOutwardDirection(wire, startPin, end);
        var endDirection = GetPinOutwardDirection(wire, endPin, start);
        var startLead = new PointF(start.X + startDirection.X * PinLeadLength, start.Y + startDirection.Y * PinLeadLength);
        var endLead = new PointF(end.X + endDirection.X * PinLeadLength, end.Y + endDirection.Y * PinLeadLength);
        var path = CombinePaths(
            FindOrthogonalPath(start, startLead, wireObstacles),
            FindOrthogonalPath(startLead, endLead, wireObstacles, priorPaths),
            FindOrthogonalPath(endLead, end, wireObstacles));

        if (path.Count >= 2)
            return ShortenRoute(path, wireObstacles, priorPaths);

        path = CombinePaths(
            FindOrthogonalPath(start, startLead, wireObstacles),
            FindOrthogonalPath(startLead, endLead, wireObstacles),
            FindOrthogonalPath(endLead, end, wireObstacles));

        if (path.Count >= 2)
            return ShortenRoute(path, wireObstacles, Array.Empty<List<PointF>>());

        path = FindOrthogonalPath(start, end, wireObstacles);
        if (path.Count >= 2)
            return ShortenRoute(path, wireObstacles, Array.Empty<List<PointF>>());

        float midX = (start.X + end.X) / 2f;
        return Simplify(new List<PointF> { start, new(midX, start.Y), new(midX, end.Y), end });
    }

    public static bool NeedsJunctionBreak(
        ComponentNode fromComponent,
        ComponentPin fromPin,
        ComponentNode toComponent,
        ComponentPin toPin,
        IEnumerable<ComponentNode> allComponents,
        out PointF junctionPoint)
    {
        var components = allComponents as IReadOnlyCollection<ComponentNode>
            ?? allComponents.ToList();
        var start = GetPinWorldPosition(fromComponent, fromPin);
        var end = GetPinWorldPosition(toComponent, toPin);

        var obstacles = GetObstacles(components, fromComponent, toComponent);
        var directPath = RouteConnection(
            fromComponent, fromPin, toComponent, toPin, components);
        if (directPath.Count > 0 && !directPath.Zip(directPath.Skip(1), (a, b) => SegmentBlocked(a, b, obstacles)).Any(b => b))
        {
            junctionPoint = default;
            return false;
        }

        var candidates = new List<PointF>();
        float midpointX = (float)(Math.Round(((start.X + end.X) / 2f) / GridStep) * GridStep);
        float midpointY = (float)(Math.Round(((start.Y + end.Y) / 2f) / GridStep) * GridStep);
        candidates.Add(new PointF(midpointX, start.Y));
        candidates.Add(new PointF(midpointX, end.Y));
        candidates.Add(new PointF(start.X, midpointY));
        candidates.Add(new PointF(end.X, midpointY));

        foreach (var component in components)
        {
            if (component == fromComponent || component == toComponent ||
                component is WireComponent ||
                component.ComponentType.Equals("Wire", StringComparison.OrdinalIgnoreCase))
                continue;

            var bounds = LogicalGridSchematicLayout.GetVisualHalfExtents(component);
            float left = (float)component.X - (float)bounds.HalfWidth - ObstaclePadding;
            float right = (float)component.X + (float)bounds.HalfWidth + ObstaclePadding;
            float top = (float)component.Y - (float)bounds.HalfHeight - ObstaclePadding;
            float bottom = (float)component.Y + (float)bounds.HalfHeight + ObstaclePadding;
            candidates.Add(new PointF(left - GridStep, (float)Math.Round(start.Y / GridStep) * GridStep));
            candidates.Add(new PointF(right + GridStep, (float)Math.Round(end.Y / GridStep) * GridStep));
            candidates.Add(new PointF((float)Math.Round(start.X / GridStep) * GridStep, top - GridStep));
            candidates.Add(new PointF((float)Math.Round(end.X / GridStep) * GridStep, bottom + GridStep));
        }

        foreach (var candidate in candidates)
        {
            if (IsBlocked(candidate, GetObstacles(components)))
                continue;

            var firstLeg = FindOrthogonalPath(
                start,
                candidate,
                GetObstacles(components, fromComponent));
            var secondLeg = FindOrthogonalPath(
                candidate,
                end,
                GetObstacles(components, toComponent));
            if (firstLeg.Count > 0 && secondLeg.Count > 0 &&
                !firstLeg.Zip(firstLeg.Skip(1), (a, b) => SegmentBlocked(a, b, obstacles)).Any(b => b) &&
                !secondLeg.Zip(secondLeg.Skip(1), (a, b) => SegmentBlocked(a, b, obstacles)).Any(b => b))
            {
                junctionPoint = new PointF(
                    (float)(Math.Round(candidate.X / GridStep) * GridStep),
                    (float)(Math.Round(candidate.Y / GridStep) * GridStep));
                return true;
            }
        }

        junctionPoint = new PointF(
            (float)(Math.Round(midpointX / GridStep) * GridStep),
            (float)(Math.Round(midpointY / GridStep) * GridStep));
        return true;
    }

    private static PointF GetClearPinLead(ComponentNode component, PointF pinPosition, PointF direction)
    {
        var halfExtents = LogicalGridSchematicLayout.GetVisualHalfExtents(component);
        bool horizontal = MathF.Abs(direction.X) >= MathF.Abs(direction.Y);
        float projectedPinOffset = horizontal
            ? MathF.Abs(pinPosition.X - (float)component.X)
            : MathF.Abs(pinPosition.Y - (float)component.Y);
        float halfExtent = horizontal ? (float)halfExtents.HalfWidth : (float)halfExtents.HalfHeight;
        float leadLength = Math.Max(PinLeadLength, halfExtent - projectedPinOffset + ObstaclePadding + 2f);
        return new PointF(
            pinPosition.X + direction.X * leadLength,
            pinPosition.Y + direction.Y * leadLength);
    }

    private static PointF TransformLocal(ComponentNode component, float x, float y)
    {
        float radians = (float)(component.Rotation * Math.PI / 180.0);
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new PointF((float)component.X + x * cos - y * sin, (float)component.Y + x * sin + y * cos);
    }

    private static PointF GetPinOutwardDirection(ComponentNode component, ComponentPin pin, PointF toward)
    {
        ComponentPinPosition position = AdaptiveComponentLayout.Get(component).GetPinPosition(pin);
        float localX = (float)position.X;
        float localY = (float)position.Y;
        float length = MathF.Sqrt(localX * localX + localY * localY);
        if (length < 0.1f)
        {
            var center = new PointF((float)component.X, (float)component.Y);
            localX = toward.X - center.X;
            localY = toward.Y - center.Y;
            length = MathF.Sqrt(localX * localX + localY * localY);
            if (length < 0.1f) return new PointF(1, 0);
        }

        float radians = (float)(component.Rotation * Math.PI / 180.0);
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        float worldX = (localX * cos - localY * sin) / length;
        float worldY = (localX * sin + localY * cos) / length;
        return MathF.Abs(worldX) >= MathF.Abs(worldY)
            ? new PointF(MathF.Sign(worldX), 0)
            : new PointF(0, MathF.Sign(worldY));
    }

    private static List<Obstacle> GetObstacles(IEnumerable<ComponentNode> components, params ComponentNode[] excluded)
    {
        var excludedSet = new HashSet<ComponentNode>(excluded);
        var obstacles = new List<Obstacle>();
        foreach (var component in components)
        {
            if (excludedSet.Contains(component) || component is WireComponent || component.ComponentType.Equals("Wire", StringComparison.OrdinalIgnoreCase))
                continue;

            var visualBounds = LogicalGridSchematicLayout.GetVisualHalfExtents(component);
            float extentX = (float)visualBounds.HalfWidth + ObstaclePadding;
            float extentY = (float)visualBounds.HalfHeight + ObstaclePadding;
            obstacles.Add(new Obstacle(
                (float)component.X - extentX,
                (float)component.Y - extentY,
                (float)component.X + extentX,
                (float)component.Y + extentY));
        }
        return obstacles;
    }

    private static List<PointF> CombinePaths(params List<PointF>[] paths)
    {
        // Skip empty segments rather than discarding the whole wire
        var validPaths = paths.Where(p => p != null && p.Count > 0).ToList();
        if (validPaths.Count == 0)
            return new List<PointF>();

        var path = new List<PointF>();
        foreach (var segment in validPaths)
        {
            foreach (var point in segment)
            {
                if (path.Count == 0 || MathF.Abs(path[^1].X - point.X) > 0.01f || MathF.Abs(path[^1].Y - point.Y) > 0.01f)
                    path.Add(point);
            }
        }
        return Simplify(path);
    }

    private static List<PointF> FindOrthogonalPath(
        PointF start,
        PointF end,
        List<Obstacle> obstacles,
        IReadOnlyList<List<PointF>>? priorPaths = null)
    {
        priorPaths ??= Array.Empty<List<PointF>>();
        var directPath = new List<PointF> { start, end };
        if (IsRouteClear(directPath, obstacles, priorPaths))
            return directPath;

        var horizontalFirst = new List<PointF> { start, new(end.X, start.Y), end };
        var verticalFirst = new List<PointF> { start, new(start.X, end.Y), end };
        bool horizontalFirstClear = IsRouteClear(horizontalFirst, obstacles, priorPaths);
        bool verticalFirstClear = IsRouteClear(verticalFirst, obstacles, priorPaths);

        if (verticalFirstClear)
            return Simplify(verticalFirst);
        if (horizontalFirstClear)
            return Simplify(horizontalFirst);

        var startGrid = ToGrid(start);
        var endGrid = ToGrid(end);
        if (startGrid == endGrid)
        {
            if ((MathF.Abs(start.X - end.X) < 0.01f || MathF.Abs(start.Y - end.Y) < 0.01f) &&
                !SegmentBlocked(start, end, obstacles) && !SharesOccupiedWireLane(start, end, priorPaths))
                return new List<PointF> { start, end };
            var firstElbow = new PointF(end.X, start.Y);
            if (!SegmentBlocked(start, firstElbow, obstacles) && !SegmentBlocked(firstElbow, end, obstacles) &&
                !SharesOccupiedWireLane(start, firstElbow, priorPaths) && !SharesOccupiedWireLane(firstElbow, end, priorPaths))
                return new List<PointF> { start, firstElbow, end };
            var secondElbow = new PointF(start.X, end.Y);
            if (!SegmentBlocked(start, secondElbow, obstacles) && !SegmentBlocked(secondElbow, end, obstacles) &&
                !SharesOccupiedWireLane(start, secondElbow, priorPaths) && !SharesOccupiedWireLane(secondElbow, end, priorPaths))
                return new List<PointF> { start, secondElbow, end };
            // Fallback: always emit an elbow, never drop the wire
            return new List<PointF> { start, firstElbow, end };
        }

        int minX = Math.Min(startGrid.X, endGrid.X) - 40;
        int maxX = Math.Max(startGrid.X, endGrid.X) + 40;
        int minY = Math.Min(startGrid.Y, endGrid.Y) - 40;
        int maxY = Math.Max(startGrid.Y, endGrid.Y) + 40;
        foreach (var obstacle in obstacles)
        {
            minX = Math.Min(minX, (int)MathF.Floor(obstacle.Left / GridStep) - 2);
            maxX = Math.Max(maxX, (int)MathF.Ceiling(obstacle.Right / GridStep) + 2);
            minY = Math.Min(minY, (int)MathF.Floor(obstacle.Top / GridStep) - 2);
            maxY = Math.Max(maxY, (int)MathF.Ceiling(obstacle.Bottom / GridStep) + 2);
        }

        var frontier = new PriorityQueue<GridPoint, int>();
        var cameFrom = new Dictionary<GridPoint, GridPoint>();
        var cost = new Dictionary<GridPoint, int> { [startGrid] = 0 };
        var visited = new HashSet<GridPoint>();
        frontier.Enqueue(startGrid, Manhattan(startGrid, endGrid));
        GridPoint[] directions = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        while (frontier.Count > 0 && visited.Count < 30000)
        {
            var current = frontier.Dequeue();
            if (!visited.Add(current)) continue;
            if (current == endGrid)
            {
                var reversed = new List<PointF> { ToWorld(current) };
                while (cameFrom.TryGetValue(current, out var previous))
                {
                    current = previous;
                    reversed.Add(ToWorld(current));
                }
                reversed.Reverse();
                var completePath = new List<PointF> { start };
                completePath.AddRange(reversed);
                completePath.Add(end);
                return ShortenRoute(
                    EnsureOrthogonal(Simplify(completePath), obstacles, priorPaths),
                    obstacles,
                    priorPaths);
            }

            foreach (var direction in directions)
            {
                var next = new GridPoint(current.X + direction.X, current.Y + direction.Y);
                if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY || visited.Contains(next)) continue;
                PointF currentWorld = ToWorld(current);
                PointF nextWorld = ToWorld(next);
                if (IsBlocked(nextWorld, obstacles) || SegmentBlocked(currentWorld, nextWorld, obstacles) ||
                    SharesOccupiedWireLane(currentWorld, nextWorld, priorPaths)) continue;

                int nextCost = cost[current] + 1;
                if (cost.TryGetValue(next, out var knownCost) && knownCost <= nextCost) continue;
                cost[next] = nextCost;
                cameFrom[next] = current;
                frontier.Enqueue(next, nextCost + Manhattan(next, endGrid));
            }
        }

        // A* exhausted - fall back to a guaranteed mid-point elbow so the wire is never dropped.
        float fallbackMidX = (start.X + end.X) / 2f;
        return Simplify(new List<PointF> { start, new(fallbackMidX, start.Y), new(fallbackMidX, end.Y), end });
    }

    private static bool IsBlocked(PointF point, List<Obstacle> obstacles) =>
        obstacles.Any(o => point.X > o.Left && point.X < o.Right && point.Y > o.Top && point.Y < o.Bottom);

    private static bool SegmentBlocked(PointF start, PointF end, List<Obstacle> obstacles)
    {
        if (MathF.Abs(start.Y - end.Y) < 0.01f)
        {
            float left = Math.Min(start.X, end.X);
            float right = Math.Max(start.X, end.X);
            return obstacles.Any(o => start.Y > o.Top && start.Y < o.Bottom && right > o.Left && left < o.Right);
        }
        if (MathF.Abs(start.X - end.X) < 0.01f)
        {
            float top = Math.Min(start.Y, end.Y);
            float bottom = Math.Max(start.Y, end.Y);
            return obstacles.Any(o => start.X > o.Left && start.X < o.Right && bottom > o.Top && top < o.Bottom);
        }
        return SegmentIntersectsObstacle(start, end, obstacles);
    }

    private static bool SegmentIntersectsObstacle(PointF start, PointF end, List<Obstacle> obstacles)
    {
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        foreach (Obstacle obstacle in obstacles)
        {
            float tMinimum = 0;
            float tMaximum = 1;
            if (!Clip(-dx, start.X - obstacle.Left, ref tMinimum, ref tMaximum) ||
                !Clip(dx, obstacle.Right - start.X, ref tMinimum, ref tMaximum) ||
                !Clip(-dy, start.Y - obstacle.Top, ref tMinimum, ref tMaximum) ||
                !Clip(dy, obstacle.Bottom - start.Y, ref tMinimum, ref tMaximum))
                continue;

            if (tMinimum < tMaximum && tMaximum > 0.001f && tMinimum < 0.999f)
                return true;
        }
        return false;
    }

    private static bool Clip(float denominator, float numerator, ref float minimum, ref float maximum)
    {
        if (MathF.Abs(denominator) < 0.0001f)
            return numerator >= 0;

        float amount = numerator / denominator;
        if (denominator < 0)
        {
            if (amount > maximum) return false;
            minimum = Math.Max(minimum, amount);
        }
        else
        {
            if (amount < minimum) return false;
            maximum = Math.Min(maximum, amount);
        }
        return true;
    }

    private static List<PointF> ShortenRoute(
        IReadOnlyList<PointF> points,
        List<Obstacle> obstacles,
        IReadOnlyList<List<PointF>> priorPaths)
    {
        if (points.Count <= 2)
            return points.ToList();

        var shortened = new List<PointF> { points[0] };
        int current = 0;
        while (current < points.Count - 1)
        {
            int next = points.Count - 1;
            while (next > current + 1 &&
                   !IsRouteClear(new[] { points[current], points[next] }, obstacles, priorPaths))
                next--;
            shortened.Add(points[next]);
            current = next;
        }
        return Simplify(shortened);
    }

    private static List<PointF> EnsureOrthogonal(
        List<PointF> points,
        List<Obstacle> obstacles,
        IReadOnlyList<List<PointF>> priorPaths)
    {
        var result = new List<PointF>();
        foreach (var point in points)
        {
            if (result.Count > 0)
            {
                var previous = result[^1];
                if (MathF.Abs(previous.X - point.X) > 0.01f && MathF.Abs(previous.Y - point.Y) > 0.01f)
                {
                    var firstElbow = new PointF(point.X, previous.Y);
                    if (!SegmentBlocked(previous, firstElbow, obstacles) && !SegmentBlocked(firstElbow, point, obstacles) &&
                        !SharesOccupiedWireLane(previous, firstElbow, priorPaths) &&
                        !SharesOccupiedWireLane(firstElbow, point, priorPaths))
                        result.Add(firstElbow);
                    else
                    {
                        var secondElbow = new PointF(previous.X, point.Y);
                        // Always add an elbow — never return empty and drop the wire
                        result.Add(!SegmentBlocked(previous, secondElbow, obstacles) &&
                                   !SegmentBlocked(secondElbow, point, obstacles) &&
                                   !SharesOccupiedWireLane(previous, secondElbow, priorPaths) &&
                                   !SharesOccupiedWireLane(secondElbow, point, priorPaths)
                            ? secondElbow
                            : firstElbow);
                    }
                }
                // If the direct segment is blocked, skip the check — we still draw it
                // so the wire is visible (it may overlap an obstacle but won't vanish).
            }
            result.Add(point);
        }
        return Simplify(result);
    }

    private static bool IsRouteClear(
        IReadOnlyList<PointF> points,
        List<Obstacle> obstacles,
        IReadOnlyList<List<PointF>> priorPaths)
    {
        for (int index = 0; index < points.Count - 1; index++)
        {
            if (SegmentBlocked(points[index], points[index + 1], obstacles) ||
                SharesOccupiedWireLane(points[index], points[index + 1], priorPaths))
                return false;
        }
        return true;
    }

    private static bool SharesOccupiedWireLane(
        PointF start,
        PointF end,
        IReadOnlyList<List<PointF>> priorPaths)
    {
        bool horizontal = MathF.Abs(start.Y - end.Y) < 0.01f;
        bool vertical = MathF.Abs(start.X - end.X) < 0.01f;

        foreach (List<PointF> priorPath in priorPaths)
        {
            for (int index = 0; index < priorPath.Count - 1; index++)
            {
                PointF priorStart = priorPath[index];
                PointF priorEnd = priorPath[index + 1];
                bool priorHorizontal = MathF.Abs(priorStart.Y - priorEnd.Y) < 0.01f;
                bool priorVertical = MathF.Abs(priorStart.X - priorEnd.X) < 0.01f;
                if (!SharesEndpoint(start, end, priorStart, priorEnd) &&
                    SegmentsIntersect(start, end, priorStart, priorEnd))
                    return true;

                if (!horizontal && !vertical)
                    continue;

                float currentMin = horizontal ? Math.Min(start.X, end.X) : Math.Min(start.Y, end.Y);
                float currentMax = horizontal ? Math.Max(start.X, end.X) : Math.Max(start.Y, end.Y);
                float lane = horizontal ? start.Y : start.X;
                if (horizontal && priorHorizontal && MathF.Abs(lane - priorStart.Y) < WireLaneClearance)
                {
                    float priorMin = Math.Min(priorStart.X, priorEnd.X);
                    float priorMax = Math.Max(priorStart.X, priorEnd.X);
                    if (Math.Min(currentMax, priorMax) - Math.Max(currentMin, priorMin) > PinLeadLength + 1)
                        return true;
                }
                else if (vertical && priorVertical && MathF.Abs(lane - priorStart.X) < WireLaneClearance)
                {
                    float priorMin = Math.Min(priorStart.Y, priorEnd.Y);
                    float priorMax = Math.Max(priorStart.Y, priorEnd.Y);
                    if (Math.Min(currentMax, priorMax) - Math.Max(currentMin, priorMin) > PinLeadLength + 1)
                        return true;
                }
            }
        }
        return false;
    }

    private static bool SharesEndpoint(PointF firstStart, PointF firstEnd, PointF secondStart, PointF secondEnd) =>
        PointsEqual(firstStart, secondStart) || PointsEqual(firstStart, secondEnd) ||
        PointsEqual(firstEnd, secondStart) || PointsEqual(firstEnd, secondEnd);

    private static bool PointsEqual(PointF first, PointF second) =>
        MathF.Abs(first.X - second.X) < 0.01f && MathF.Abs(first.Y - second.Y) < 0.01f;

    private static bool SegmentsIntersect(PointF firstStart, PointF firstEnd, PointF secondStart, PointF secondEnd)
    {
        float firstOrientation = Cross(firstStart, firstEnd, secondStart);
        float secondOrientation = Cross(firstStart, firstEnd, secondEnd);
        float thirdOrientation = Cross(secondStart, secondEnd, firstStart);
        float fourthOrientation = Cross(secondStart, secondEnd, firstEnd);

        if (((firstOrientation > 0 && secondOrientation < 0) || (firstOrientation < 0 && secondOrientation > 0)) &&
            ((thirdOrientation > 0 && fourthOrientation < 0) || (thirdOrientation < 0 && fourthOrientation > 0)))
            return true;

        return (MathF.Abs(firstOrientation) < 0.01f && PointOnSegment(secondStart, firstStart, firstEnd)) ||
            (MathF.Abs(secondOrientation) < 0.01f && PointOnSegment(secondEnd, firstStart, firstEnd)) ||
            (MathF.Abs(thirdOrientation) < 0.01f && PointOnSegment(firstStart, secondStart, secondEnd)) ||
            (MathF.Abs(fourthOrientation) < 0.01f && PointOnSegment(firstEnd, secondStart, secondEnd));
    }

    private static float Cross(PointF start, PointF end, PointF point) =>
        (end.X - start.X) * (point.Y - start.Y) - (end.Y - start.Y) * (point.X - start.X);

    private static bool PointOnSegment(PointF point, PointF start, PointF end) =>
        point.X >= Math.Min(start.X, end.X) - 0.01f &&
        point.X <= Math.Max(start.X, end.X) + 0.01f &&
        point.Y >= Math.Min(start.Y, end.Y) - 0.01f &&
        point.Y <= Math.Max(start.Y, end.Y) + 0.01f;

    private static int Manhattan(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    private static GridPoint ToGrid(PointF point) => new((int)MathF.Round(point.X / GridStep), (int)MathF.Round(point.Y / GridStep));
    private static PointF ToWorld(GridPoint point) => new(point.X * GridStep, point.Y * GridStep);

    private static List<PointF> Simplify(List<PointF> points)
    {
        var compact = new List<PointF>();
        foreach (var point in points)
        {
            if (compact.Count == 0 || MathF.Abs(compact[^1].X - point.X) > 0.01f || MathF.Abs(compact[^1].Y - point.Y) > 0.01f)
                compact.Add(point);
        }

        for (int i = compact.Count - 2; i > 0; i--)
        {
            var a = compact[i - 1];
            var b = compact[i];
            var c = compact[i + 1];
            if ((MathF.Abs(a.X - b.X) < 0.01f && MathF.Abs(b.X - c.X) < 0.01f) ||
                (MathF.Abs(a.Y - b.Y) < 0.01f && MathF.Abs(b.Y - c.Y) < 0.01f))
                compact.RemoveAt(i);
        }
        return compact;
    }
}
