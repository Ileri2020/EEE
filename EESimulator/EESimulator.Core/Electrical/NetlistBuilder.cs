using System;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Models;

namespace EESimulator.Core.Electrical;

public class CircuitTopology
{
    public List<NetNode> Nets { get; } = new();
    public Dictionary<PinReference, NetNode> PinToNetMap { get; } = new();
}

/// <summary>
/// Analyzes connections, wires, and junctions to resolve unified electrical nodes/nets
/// using disjoint-set union (union-find) independent of visual wiring geometry.
/// </summary>
public class NetlistBuilder
{
    private class DisjointSet
    {
        private readonly Dictionary<PinReference, PinReference> _parent = new();

        public PinReference Find(PinReference pin)
        {
            if (!_parent.ContainsKey(pin))
                _parent[pin] = pin;

            if (!_parent[pin].Equals(pin))
                _parent[pin] = Find(_parent[pin]);

            return _parent[pin];
        }

        public void Union(PinReference a, PinReference b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (!rootA.Equals(rootB))
                _parent[rootB] = rootA;
        }

        public IEnumerable<PinReference> AllPins => _parent.Keys;
    }

    public static CircuitTopology BuildTopology(CircuitNode circuit)
    {
        var dsu = new DisjointSet();

        // Register all pins on all components
        foreach (var comp in circuit.Components)
        {
            foreach (var pin in comp.Pins)
            {
                var pinRef = new PinReference(comp.Id, pin.Id);
                dsu.Find(pinRef);
            }
        }

        // Union connected pins
        foreach (var conn in circuit.Connections)
        {
            var p1 = new PinReference(conn.FromComponentId, conn.FromPin);
            var p2 = new PinReference(conn.ToComponentId, conn.ToPin);
            dsu.Union(p1, p2);
        }

        // Union same-labeled Tunnel components across the schematic
        var tunnelGroups = circuit.Components
            .OfType<EESimulator.Core.Components.General.TunnelComponent>()
            .GroupBy(t => t.TunnelLabel, StringComparer.OrdinalIgnoreCase);

        foreach (var tGroup in tunnelGroups)
        {
            var tunnels = tGroup.ToList();
            if (tunnels.Count > 1 && tunnels[0].Pins.Count > 0)
            {
                var p0 = new PinReference(tunnels[0].Id, tunnels[0].Pins[0].Id);
                for (int i = 1; i < tunnels.Count; i++)
                {
                    if (tunnels[i].Pins.Count > 0)
                    {
                        var pi = new PinReference(tunnels[i].Id, tunnels[i].Pins[0].Id);
                        dsu.Union(p0, pi);
                    }
                }
            }
        }

        // Group pins by their root representative
        var groups = new Dictionary<PinReference, List<PinReference>>();
        foreach (var pin in dsu.AllPins)
        {
            var root = dsu.Find(pin);
            if (!groups.TryGetValue(root, out var list))
            {
                list = new List<PinReference>();
                groups[root] = list;
            }
            list.Add(pin);
        }

        var topology = new CircuitTopology();
        int netCounter = 1;

        // Identify ground components
        var groundCompIds = new HashSet<Guid>(circuit.Components.Where(c => c is GroundNode).Select(c => c.Id));

        foreach (var kvp in groups)
        {
            var pinList = kvp.Value;
            bool isGround = pinList.Any(p => groundCompIds.Contains(p.ComponentId));

            // Check if any connected component is a Tunnel or Label to give the net a descriptive name
            string? descriptiveName = null;
            if (!isGround)
            {
                var tunnelComp = circuit.Components
                    .OfType<EESimulator.Core.Components.General.TunnelComponent>()
                    .FirstOrDefault(t => pinList.Any(p => p.ComponentId == t.Id));
                if (tunnelComp != null && !string.IsNullOrWhiteSpace(tunnelComp.TunnelLabel))
                {
                    descriptiveName = tunnelComp.TunnelLabel;
                }
                else
                {
                    var labelComp = circuit.Components
                        .OfType<EESimulator.Core.Components.General.LabelComponent>()
                        .FirstOrDefault(l => pinList.Any(p => p.ComponentId == l.Id));
                    if (labelComp != null && !string.IsNullOrWhiteSpace(labelComp.Name))
                    {
                        descriptiveName = labelComp.Name;
                    }
                }
            }

            string netName = isGround ? "0" : (descriptiveName ?? $"Net_{netCounter++}");

            var netNode = new NetNode(netName, isGround);
            foreach (var p in pinList)
            {
                netNode.ConnectedPins.Add(p);
                topology.PinToNetMap[p] = netNode;
            }
            topology.Nets.Add(netNode);
        }

        return topology;
    }
}
