using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Models;

namespace EESimulator.Core.Electrical;

public class CircuitTopology
{
    public List<NetNode> Nets { get; } = new();
    public Dictionary<PinReference, NetNode> PinToNetMap { get; } = new();
    public List<ConnectionNode> ResistiveConnections { get; } = new();
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

        var resistiveConnections = new List<ConnectionNode>();

        // Union ideal wires; finite-resistance wires stay as simulation branches.
        foreach (var conn in connections)
        {
            if (conn.Resistance > 0)
            {
                resistiveConnections.Add(conn);
                continue;
            }

            var p1 = new PinReference(conn.FromComponentId, conn.FromPin);
            var p2 = new PinReference(conn.ToComponentId, conn.ToPin);
            dsu.Union(p1, p2);
        }

        var groundsByGroup = components
            .OfType<GroundNode>()
            .Where(ground => ground.Pins.Count > 0)
            .GroupBy(ground => ground.GroundGroup, StringComparer.OrdinalIgnoreCase);
        foreach (var groundGroup in groundsByGroup)
        {
            var groupGrounds = groundGroup.ToList();
            var firstGroundPin = new PinReference(groupGrounds[0].Id, groupGrounds[0].Pins[0].Id);
            for (int i = 1; i < groupGrounds.Count; i++)
                dsu.Union(firstGroundPin, new PinReference(groupGrounds[i].Id, groupGrounds[i].Pins[0].Id));
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
        topology.ResistiveConnections.AddRange(resistiveConnections);
        var groundsByComponentId = components
            .OfType<GroundNode>()
            .ToDictionary(ground => ground.Id);
        var groupEntries = groups.Values.ToList();
        var netNames = new string[groupEntries.Count];
        var groundNets = new bool[groupEntries.Count];
        var netGroundGroups = new string?[groupEntries.Count];
        var usedNetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "0" };
        int netCounter = 1;
        int groundCounter = 1;
        for (int i = 0; i < groupEntries.Count; i++)
        {
            var pinList = groupEntries[i];
            string[] groundGroups = pinList
                .Where(pin => groundsByComponentId.ContainsKey(pin.ComponentId))
                .Select(pin => groundsByComponentId[pin.ComponentId].GroundGroup)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string? groundGroup = groundGroups.Contains(
                GroundComponent.DefaultGroundGroup, StringComparer.OrdinalIgnoreCase)
                ? GroundComponent.DefaultGroundGroup
                : groundGroups.FirstOrDefault();
            bool isDefaultGround = groundGroup?.Equals(
                GroundComponent.DefaultGroundGroup, StringComparison.OrdinalIgnoreCase) == true;
            groundNets[i] = isDefaultGround;
            netGroundGroups[i] = groundGroup;
            string? descriptiveName = null;
            if (groundGroup == null)
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

            if (isDefaultGround)
            {
                netNames[i] = "0";
            }
            else if (groundGroup != null)
            {
                string candidate;
                do
                {
                    candidate = $"GroundGroup_{groundCounter++}";
                } while (!usedNetNames.Add(candidate));
                netNames[i] = candidate;
            }
            else
            {
                string candidate = descriptiveName ?? $"Net_{netCounter++}";
                while (!usedNetNames.Add(candidate))
                    candidate = $"Net_{netCounter++}";
                netNames[i] = candidate;
            }
        }

        var resolvedNets = new NetNode[groupEntries.Count];
        void CreateNet(int index)
        {
            var pinList = groupEntries[index];
            var netNode = new NetNode(netNames[index], groundNets[index], netGroundGroups[index]);
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
