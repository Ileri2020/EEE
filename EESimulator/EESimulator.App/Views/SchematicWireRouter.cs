using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Graphics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

/// <summary>Creates orthogonal schematic paths that avoid component bodies.</summary>
internal static class SchematicWireRouter
{
    private const float GridStep = 10f;
    private const float ObstaclePadding = 9f;
    private const float PinLeadLength = 14f;

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
        IReadOnlyList<Point2D>? routingPoints = null)
    {
        var start = GetPinWorldPosition(fromComponent, fromPin);
        var end = GetPinWorldPosition(toComponent, toPin);
        if (routingPoints is { Count: > 0 })
        {
            var routedPathObstacles = GetObstacles(allComponents, fromComponent, toComponent);
            var points = new List<PointF> { start };
            points.AddRange(routingPoints.Select(point => new PointF((float)point.X, (float)point.Y)));
            points.Add(end);
            var routed = new List<List<PointF>>();
            for (int index = 0; index < points.Count - 1; index++)
                routed.Add(FindOrthogonalPath(points[index], points[index + 1], routedPathObstacles));
            return CombinePaths(routed.ToArray());
        }
        var startDirection = GetPinOutwardDirection(fromComponent, fromPin, end);
        var endDirection = GetPinOutwardDirection(toComponent, toPin, start);
        var startLead = new PointF(start.X + startDirection.X * PinLeadLength, start.Y + startDirection.Y * PinLeadLength);
        var endLead = new PointF(end.X + endDirection.X * PinLeadLength, end.Y + endDirection.Y * PinLeadLength);
        var connectionObstacles = GetObstacles(allComponents, fromComponent, toComponent);
        return CombinePaths(
            FindOrthogonalPath(start, startLead, connectionObstacles),
            FindOrthogonalPath(startLead, endLead, connectionObstacles),
            FindOrthogonalPath(endLead, end, connectionObstacles));
    }

    public static List<PointF> RouteWireComponent(WireComponent wire, IEnumerable<ComponentNode> allComponents)
    {
        var startPin = wire.Pins.FirstOrDefault();
        var endPin = wire.Pins.Skip(1).FirstOrDefault();
        if (startPin == null || endPin == null)
            return new List<PointF> { new((float)wire.X, (float)wire.Y), new((float)wire.X + 40, (float)wire.Y) };

        var start = GetPinWorldPosition(wire, startPin);
        var end = GetPinWorldPosition(wire, endPin);
        var startDirection = GetPinOutwardDirection(wire, startPin, end);
        var endDirection = GetPinOutwardDirection(wire, endPin, start);
        var startLead = new PointF(start.X + startDirection.X * PinLeadLength, start.Y + startDirection.Y * PinLeadLength);
        var endLead = new PointF(end.X + endDirection.X * PinLeadLength, end.Y + endDirection.Y * PinLeadLength);
        var wireObstacles = GetObstacles(allComponents, wire);
        return CombinePaths(
            FindOrthogonalPath(start, startLead, wireObstacles),
            FindOrthogonalPath(startLead, endLead, wireObstacles),
            FindOrthogonalPath(endLead, end, wireObstacles));
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

            AdaptiveComponentLayout layout = AdaptiveComponentLayout.Get(component);
            float halfX = (float)layout.HalfWidth;
            float halfY = (float)layout.HalfHeight;
            float radians = (float)(component.Rotation * Math.PI / 180.0);
            float cos = MathF.Abs(MathF.Cos(radians));
            float sin = MathF.Abs(MathF.Sin(radians));
            float extentX = halfX * cos + halfY * sin + ObstaclePadding;
            float extentY = halfX * sin + halfY * cos + ObstaclePadding;
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
        if (paths.Any(segment => segment.Count == 0))
            return new List<PointF>();

        var path = new List<PointF>();
        foreach (var segment in paths)
        {
            foreach (var point in segment)
            {
                if (path.Count == 0 || MathF.Abs(path[^1].X - point.X) > 0.01f || MathF.Abs(path[^1].Y - point.Y) > 0.01f)
                    path.Add(point);
            }
        }
        return Simplify(path);
    }

    private static List<PointF> FindOrthogonalPath(PointF start, PointF end, List<Obstacle> obstacles)
    {
        var startGrid = ToGrid(start);
        var endGrid = ToGrid(end);
        if (startGrid == endGrid)
        {
            if ((MathF.Abs(start.X - end.X) < 0.01f || MathF.Abs(start.Y - end.Y) < 0.01f) &&
                !SegmentBlocked(start, end, obstacles))
                return new List<PointF> { start, end };
            var firstElbow = new PointF(end.X, start.Y);
            if (!SegmentBlocked(start, firstElbow, obstacles) && !SegmentBlocked(firstElbow, end, obstacles))
                return new List<PointF> { start, firstElbow, end };
            var secondElbow = new PointF(start.X, end.Y);
            if (!SegmentBlocked(start, secondElbow, obstacles) && !SegmentBlocked(secondElbow, end, obstacles))
                return new List<PointF> { start, secondElbow, end };
            return new List<PointF>();
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
                return EnsureOrthogonal(Simplify(completePath), obstacles);
            }

            foreach (var direction in directions)
            {
                var next = new GridPoint(current.X + direction.X, current.Y + direction.Y);
                if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY || visited.Contains(next)) continue;
                if (IsBlocked(ToWorld(next), obstacles)) continue;

                int nextCost = cost[current] + 1;
                if (cost.TryGetValue(next, out var knownCost) && knownCost <= nextCost) continue;
                cost[next] = nextCost;
                cameFrom[next] = current;
                frontier.Enqueue(next, nextCost + Manhattan(next, endGrid));
            }
        }

        // Do not draw a crossing line if no collision-free route could be found.
        return new List<PointF>();
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
        return true;
    }

    private static List<PointF> EnsureOrthogonal(List<PointF> points, List<Obstacle> obstacles)
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
                    if (!SegmentBlocked(previous, firstElbow, obstacles) && !SegmentBlocked(firstElbow, point, obstacles))
                        result.Add(firstElbow);
                    else
                    {
                        var secondElbow = new PointF(previous.X, point.Y);
                        if (SegmentBlocked(previous, secondElbow, obstacles) || SegmentBlocked(secondElbow, point, obstacles))
                            return new List<PointF>();
                        result.Add(secondElbow);
                    }
                }
                else if (SegmentBlocked(previous, point, obstacles))
                {
                    return new List<PointF>();
                }
            }
            result.Add(point);
        }
        return Simplify(result);
    }

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
