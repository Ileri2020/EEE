using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly Dictionary<PinReference, byte> _rank = new();

        public PinReference Find(PinReference pin)
        {
            if (!_parent.ContainsKey(pin))
            {
                _parent[pin] = pin;
                _rank[pin] = 0;
            }

            if (!_parent[pin].Equals(pin))
                _parent[pin] = Find(_parent[pin]);

            return _parent[pin];
        }

        public void Union(PinReference a, PinReference b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA.Equals(rootB))
                return;

            byte rankA = _rank[rootA];
            byte rankB = _rank[rootB];
            if (rankA < rankB)
                _parent[rootA] = rootB;
            else
            {
                _parent[rootB] = rootA;
                if (rankA == rankB)
                    _rank[rootA]++;
            }
        }

        public IEnumerable<PinReference> AllPins => _parent.Keys;
    }

    public static CircuitTopology BuildTopology(CircuitNode circuit)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        var components = circuit.Components.ToList();
        var connections = circuit.Connections.ToList();
        var dsu = new DisjointSet();

        // Register all pins on all components
        foreach (var comp in components)
        {
            foreach (var pin in comp.Pins)
            {
                var pinRef = new PinReference(comp.Id, pin.Id);
                dsu.Find(pinRef);
            }
        }

        // Union connected pins
        foreach (var conn in connections)
        {
            var p1 = new PinReference(conn.FromComponentId, conn.FromPin);
            var p2 = new PinReference(conn.ToComponentId, conn.ToPin);
            dsu.Union(p1, p2);
        }

        // Union same-labeled Tunnel components across the schematic
        var tunnelsByLabel = components
            .OfType<EESimulator.Core.Components.General.TunnelComponent>()
            .Where(tunnel => !string.IsNullOrWhiteSpace(tunnel.TunnelLabel) && tunnel.Pins.Count > 0)
            .GroupBy(tunnel => tunnel.TunnelLabel, StringComparer.OrdinalIgnoreCase);
        var tunnelByComponent = new Dictionary<Guid, EESimulator.Core.Components.General.TunnelComponent>();
        foreach (var tunnelGroup in tunnelsByLabel)
        {
            var tunnels = tunnelGroup.ToList();
            var first = tunnels[0];
            var firstPin = new PinReference(first.Id, first.Pins[0].Id);
            tunnelByComponent[first.Id] = first;
            for (int i = 1; i < tunnels.Count; i++)
            {
                var tunnel = tunnels[i];
                dsu.Union(firstPin, new PinReference(tunnel.Id, tunnel.Pins[0].Id));
                tunnelByComponent[tunnel.Id] = tunnel;
            }
        }
        var labelsByComponent = components
            .OfType<EESimulator.Core.Components.General.LabelComponent>()
            .ToDictionary(label => label.Id);

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
        var groundCompIds = new HashSet<Guid>(components.Where(component => component is GroundNode).Select(component => component.Id));
        var groupEntries = groups.Values.ToList();
        var netNames = new string[groupEntries.Count];
        var groundNets = new bool[groupEntries.Count];
        int netCounter = 1;
        for (int i = 0; i < groupEntries.Count; i++)
        {
            var pinList = groupEntries[i];
            bool isGround = pinList.Any(p => groundCompIds.Contains(p.ComponentId));
            groundNets[i] = isGround;
            string? descriptiveName = null;
            if (!isGround)
            {
                foreach (var pin in pinList)
                {
                    if (tunnelByComponent.TryGetValue(pin.ComponentId, out var tunnel) &&
                        !string.IsNullOrWhiteSpace(tunnel.TunnelLabel))
                    {
                        descriptiveName = tunnel.TunnelLabel;
                        break;
                    }
                }

                if (descriptiveName == null)
                {
                    foreach (var pin in pinList)
                    {
                        if (labelsByComponent.TryGetValue(pin.ComponentId, out var label) &&
                            !string.IsNullOrWhiteSpace(label.Name))
                        {
                            descriptiveName = label.Name;
                            break;
                        }
                    }
                }
            }

            netNames[i] = isGround ? "0" : descriptiveName ?? $"Net_{netCounter++}";
        }

        var resolvedNets = new NetNode[groupEntries.Count];
        void CreateNet(int index)
        {
            var pinList = groupEntries[index];
            var netNode = new NetNode(netNames[index], groundNets[index]);
            foreach (var p in pinList)
                netNode.ConnectedPins.Add(p);
            resolvedNets[index] = netNode;
        }

        if (groupEntries.Count >= 128)
            Parallel.For(0, groupEntries.Count, CreateNet);
        else
            for (int i = 0; i < groupEntries.Count; i++)
                CreateNet(i);

        foreach (var net in resolvedNets)
        {
            topology.Nets.Add(net);
            foreach (var pin in net.ConnectedPins)
                topology.PinToNetMap[pin] = net;
        }

        return topology;
    }
}
