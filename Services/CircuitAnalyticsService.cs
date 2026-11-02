using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Electrical;
using EESimulator.Core.Models;
using EESimulator.Engine;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;

namespace EEE.Services;

/// <summary>Builds engineering reports from operating-point results and source-isolated solves.</summary>
public static class CircuitAnalyticsService
{
    private sealed record Branch(ComponentNode Component, string From, string To, double Voltage, double Current);
    private sealed record WalkEdge(string To, Branch Branch, bool Forward);

    public static string Analyze(CircuitNode circuit, SimulationResultsData results, string mode, ComponentNode? selectedComponent)
    {
        if (!results.Success)
            return $"Analysis could not run: {results.Message}";

        return mode switch
        {
            "Nodal analysis" => BuildNodalReport(circuit, results),
            "KCL" => BuildKclReport(circuit, results),
            "KVL" => BuildKvlReport(circuit),
            "Mesh analysis" => BuildMeshReport(circuit),
            "Thevenin voltage and current" => BuildTheveninReport(circuit, selectedComponent),
            "Superposition" => BuildSuperpositionReport(circuit),
            _ => "Select an analysis type."
        };
    }

    private static string BuildNodalReport(CircuitNode circuit, SimulationResultsData results)
    {
        var topology = NetlistBuilder.BuildTopology(circuit);
        var lines = new List<string> { "NODAL VOLTAGES (referenced to ground)" };
        foreach (var net in topology.Nets.OrderBy(net => net.NetName, StringComparer.Ordinal))
        {
            double voltage = net.IsGround ? 0 : results.OperatingPointVoltages.GetValueOrDefault(net.NetName);
            lines.Add($"{net.NetName,-18} {voltage,12:G6} V{(net.IsGround ? "  (reference)" : string.Empty)}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildKclReport(CircuitNode circuit, SimulationResultsData results)
    {
        var topology = NetlistBuilder.BuildTopology(circuit);
        var sums = topology.Nets.ToDictionary(net => net.NetName, _ => 0.0, StringComparer.Ordinal);
        foreach (var component in circuit.Components.Where(component => component.Pins.Count == 2))
        {
            var pin0 = new PinReference(component.Id, component.Pins[0].Id);
            var pin1 = new PinReference(component.Id, component.Pins[1].Id);
            if (!topology.PinToNetMap.TryGetValue(pin0, out var net0) || !topology.PinToNetMap.TryGetValue(pin1, out var net1)) continue;
            double current = results.OperatingPointCurrents.GetValueOrDefault($"I({component.Name})", component.LiveCurrent);
            sums[net0.NetName] += current;
            sums[net1.NetName] -= current;
        }

        var lines = new List<string> { "KIRCHHOFF CURRENT LAW — signed net current (sum should approach 0 A)" };
        lines.AddRange(sums.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key,-18} {pair.Value,12:G6} A"));
        if (circuit.Components.Any(component => component.Pins.Count > 2))
            lines.Add("Note: multi-terminal device currents are not decomposed per pin by the current solver and are omitted from this residual.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildKvlReport(CircuitNode circuit)
    {
        var branches = GetBranches(circuit);
        var adjacency = new Dictionary<string, List<WalkEdge>>(StringComparer.Ordinal);
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        string Find(string node)
        {
            if (!parent.ContainsKey(node)) parent[node] = node;
            if (parent[node] != node) parent[node] = Find(parent[node]);
            return parent[node];
        }

        var chords = new List<Branch>();
        foreach (var branch in branches)
        {
            var rootA = Find(branch.From);
            var rootB = Find(branch.To);
            if (rootA == rootB)
            {
                chords.Add(branch);
                continue;
            }
            parent[rootB] = rootA;
            AddWalkEdge(adjacency, branch.From, branch.To, branch, true);
            AddWalkEdge(adjacency, branch.To, branch.From, branch, false);
        }

        var lines = new List<string> { "KIRCHHOFF VOLTAGE LAW — fundamental loop residuals" };
        if (chords.Count == 0)
        {
            lines.Add("No closed loops were found in the two-terminal component graph.");
            return string.Join(Environment.NewLine, lines);
        }

        int loop = 1;
        foreach (var chord in chords)
        {
            var path = FindTreePath(adjacency, chord.To, chord.From);
            if (path == null) continue;
            double sum = chord.Voltage + path.Sum(step => step.Forward ? step.Branch.Voltage : -step.Branch.Voltage);
            lines.Add($"Loop {loop++}: {chord.Component.Reference} + {string.Join(" + ", path.Select(step => step.Branch.Component.Reference))} = {sum:G6} V");
        }
        if (circuit.Components.Any(component => component.Pins.Count > 2))
            lines.Add("Note: multi-terminal devices are omitted because the solver does not expose their per-pin voltage branches.");
        if (loop == 1) lines.Add("No independent loops were available for a voltage check.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildMeshReport(CircuitNode circuit)
    {
        var branches = GetBranches(circuit);
        var vertices = branches.SelectMany(branch => new[] { branch.From, branch.To }).Distinct(StringComparer.Ordinal).ToArray();
        var parent = vertices.ToDictionary(node => node, node => node, StringComparer.Ordinal);
        string Find(string node)
        {
            if (parent[node] != node) parent[node] = Find(parent[node]);
            return parent[node];
        }
        foreach (var branch in branches)
        {
            var a = Find(branch.From);
            var b = Find(branch.To);
            if (a != b) parent[b] = a;
        }
        int connectedGroups = vertices.Select(Find).Distinct(StringComparer.Ordinal).Count();
        int independentLoops = Math.Max(0, branches.Count - vertices.Length + connectedGroups);
        var lines = new List<string>
        {
            "MESH / FUNDAMENTAL-LOOP CURRENTS",
            $"Branches: {branches.Count}; nodes: {vertices.Length}; independent loops: {independentLoops}.",
            "A spanning-tree fundamental loop uses its chord branch current as the loop current."
        };
        var treeAdjacency = new Dictionary<string, List<WalkEdge>>(StringComparer.Ordinal);
        var forestParent = vertices.ToDictionary(node => node, node => node, StringComparer.Ordinal);
        string Root(string node)
        {
            if (forestParent[node] != node) forestParent[node] = Root(forestParent[node]);
            return forestParent[node];
        }
        var chords = new List<Branch>();
        foreach (var branch in branches)
        {
            var rootA = Root(branch.From);
            var rootB = Root(branch.To);
            if (rootA == rootB)
            {
                chords.Add(branch);
                continue;
            }
            forestParent[rootB] = rootA;
            AddWalkEdge(treeAdjacency, branch.From, branch.To, branch, true);
            AddWalkEdge(treeAdjacency, branch.To, branch.From, branch, false);
        }
        int mesh = 1;
        foreach (var chord in chords)
        {
            var path = FindTreePath(treeAdjacency, chord.To, chord.From);
            if (path == null) continue;
            double kvlResidual = chord.Voltage + path.Sum(step => step.Forward ? step.Branch.Voltage : -step.Branch.Voltage);
            lines.Add($"Mesh {mesh++} ({chord.Component.Reference}): Im = {chord.Current:G6} A; KVL residual = {kvlResidual:G6} V");
        }
        lines.Add("Two-terminal branches are used; multi-terminal device branch decomposition is not exposed by the solver.");
        if (independentLoops == 0) lines.Add("The selected circuit graph has no closed mesh.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildTheveninReport(CircuitNode circuit, ComponentNode? selected)
    {
        if (selected == null || selected.Pins.Count < 2)
            return "Select a two-terminal component to use its pins as the Thevenin output terminals.";

        try
        {
            var originalTopology = NetlistBuilder.BuildTopology(circuit);
            var originalPin0 = new PinReference(selected.Id, selected.Pins[0].Id);
            var originalPin1 = new PinReference(selected.Id, selected.Pins[1].Id);
            if (!originalTopology.PinToNetMap.TryGetValue(originalPin0, out var originalNet0) ||
                !originalTopology.PinToNetMap.TryGetValue(originalPin1, out var originalNet1))
                return "The selected component terminals could not be resolved to circuit nodes.";

            var rep0 = FindExternalPin(originalNet0, selected.Id);
            var rep1 = FindExternalPin(originalNet1, selected.Id);
            if (rep0 == null || rep1 == null)
                return "Thevenin analysis needs both selected terminals connected to the rest of the circuit.";

            var openCircuit = CloneCircuit(circuit);
            RemoveComponentAndConnections(openCircuit, selected.Id);
            var openTopology = NetlistBuilder.BuildTopology(openCircuit);
            var openResults = new SimulationManager().Run(openCircuit);
            if (!openResults.Success) return $"Open-circuit solve failed: {openResults.Message}";
            double v0 = ReadNodeVoltage(openTopology, rep0, openResults);
            double v1 = ReadNodeVoltage(openTopology, rep1, openResults);
            double vth = v0 - v1;

            var resistanceCircuit = CloneCircuit(circuit);
            RemoveComponentAndConnections(resistanceCircuit, selected.Id);
            DeactivateIndependentSources(resistanceCircuit);
            var testSource = new CurrentSourceComponent("__thevenin_test", 1.0);
            resistanceCircuit.AddChild(testSource);
            resistanceCircuit.AddChild(new ConnectionNode(testSource.Id, testSource.Pins[0].Id, rep0.ComponentId, rep0.PinId));
            resistanceCircuit.AddChild(new ConnectionNode(testSource.Id, testSource.Pins[1].Id, rep1.ComponentId, rep1.PinId));
            var testResults = new SimulationManager().Run(resistanceCircuit);
            if (!testResults.Success) return $"Thevenin resistance solve failed: {testResults.Message}";
            var testTopology = NetlistBuilder.BuildTopology(resistanceCircuit);
            double testV0 = ReadNodeVoltage(testTopology, new PinReference(testSource.Id, testSource.Pins[0].Id), testResults);
            double testV1 = ReadNodeVoltage(testTopology, new PinReference(testSource.Id, testSource.Pins[1].Id), testResults);
            double rth = Math.Abs(testV0 - testV1);
            string currentText = rth > 1e-12 ? $"{vth / rth:G6} A" : (Math.Abs(vth) < 1e-9 ? "indeterminate (0 V / 0 Ω)" : "unbounded (approximately 0 Ω)" );
            return $"THEVENIN EQUIVALENT AT {selected.Reference} TERMINALS\nVth = {vth:G6} V\nRth = {rth:G6} Ω\nIsc = {currentText}\nThe selected component was removed for the open-circuit solve; independent sources were deactivated for Rth.";
        }
        catch (Exception ex)
        {
            return $"Thevenin analysis unavailable: {ex.Message}";
        }
    }

    private static string BuildSuperpositionReport(CircuitNode circuit)
    {
        var sources = circuit.Components.Where(IsIndependentSource).ToList();
        if (sources.Count == 0) return "Superposition requires at least one independent source.";
        if (circuit.Components.Any(component => component is DiodeComponent or LEDComponent or BJTComponent or MOSFETComponent or JFETComponent))
            return "Superposition is only valid for linear circuits; this circuit contains nonlinear semiconductor devices.";

        var contributions = new List<(string Source, Dictionary<string, double> Voltages)>();
        foreach (var source in sources)
        {
            var isolated = CloneCircuit(circuit);
            foreach (var candidate in isolated.Components.Where(IsIndependentSource))
            {
                bool active = candidate.Id == source.Id;
                switch (candidate)
                {
                    case VoltageSourceComponent voltage:
                        voltage.WaveformType = "DC";
                        voltage.Voltage = active ? ((VoltageSourceComponent)source).Voltage : 0;
                        break;
                    case CurrentSourceComponent current:
                        current.WaveformType = "DC";
                        current.Current = active ? ((CurrentSourceComponent)source).Current : 0;
                        break;
                    case PowerComponent power: power.Voltage = active ? ((PowerComponent)source).Voltage : 0; break;
                    case LogicInputComponent logic: logic.State = active && ((LogicInputComponent)source).State; break;
                }
            }
            var result = new SimulationManager().Run(isolated);
            if (!result.Success) return $"Superposition solve for {source.Reference} failed: {result.Message}";
            contributions.Add((source.Reference, result.OperatingPointVoltages
                .Where(pair => !pair.Key.StartsWith("V(", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)));
        }

        var nets = contributions.SelectMany(item => item.Voltages.Keys).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var lines = new List<string> { "SUPERPOSITION — independent-source voltage contributions", "Each source is solved alone with all other independent sources deactivated." };
        foreach (var net in nets)
        {
            var parts = contributions.Select(item => $"{item.Source}: {item.Voltages.GetValueOrDefault(net):G5} V").ToArray();
            lines.Add($"{net,-16} Σ={contributions.Sum(item => item.Voltages.GetValueOrDefault(net)):G5} V  ({string.Join("; ", parts)})");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static List<Branch> GetBranches(CircuitNode circuit)
    {
        var topology = NetlistBuilder.BuildTopology(circuit);
        var branches = new List<Branch>();
        foreach (var component in circuit.Components.Where(component => component.Pins.Count == 2))
        {
            var first = topology.PinToNetMap.GetValueOrDefault(new PinReference(component.Id, component.Pins[0].Id));
            var second = topology.PinToNetMap.GetValueOrDefault(new PinReference(component.Id, component.Pins[1].Id));
            if (first == null || second == null || first.NetName == second.NetName) continue;
            branches.Add(new Branch(component, first.NetName, second.NetName, component.LiveVoltage, component.LiveCurrent));
        }
        return branches;
    }

    private static void AddWalkEdge(Dictionary<string, List<WalkEdge>> adjacency, string from, string to, Branch branch, bool forward)
    {
        if (!adjacency.TryGetValue(from, out var edges)) adjacency[from] = edges = new List<WalkEdge>();
        edges.Add(new WalkEdge(to, branch, forward));
    }

    private static List<WalkEdge>? FindTreePath(Dictionary<string, List<WalkEdge>> adjacency, string start, string goal)
    {
        var queue = new Queue<string>();
        var previous = new Dictionary<string, (string Node, WalkEdge Edge)>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { start };
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal) break;
            if (!adjacency.TryGetValue(current, out var edges)) continue;
            foreach (var edge in edges)
            {
                if (!seen.Add(edge.To)) continue;
                previous[edge.To] = (current, edge);
                queue.Enqueue(edge.To);
            }
        }
        if (!seen.Contains(goal)) return null;
        var path = new List<WalkEdge>();
        var node = goal;
        while (node != start)
        {
            var step = previous[node];
            path.Add(step.Edge);
            node = step.Node;
        }
        path.Reverse();
        return path;
    }

    private static CircuitNode CloneCircuit(CircuitNode circuit) =>
        EeePlatformSerializer.LoadFromEee(EeePlatformSerializer.ExportToEee(circuit));

    private static void RemoveComponentAndConnections(CircuitNode circuit, Guid componentId)
    {
        foreach (var connection in circuit.Connections.Where(connection =>
                     connection.FromComponentId == componentId || connection.ToComponentId == componentId).ToList())
            circuit.RemoveChild(connection);
        var component = circuit.Components.FirstOrDefault(component => component.Id == componentId);
        if (component != null) circuit.RemoveChild(component);
    }

    private static PinReference? FindExternalPin(NetNode net, Guid excludedComponentId)
    {
        foreach (var pin in net.ConnectedPins)
            if (pin.ComponentId != excludedComponentId)
                return pin;
        return null;
    }

    private static double ReadNodeVoltage(CircuitTopology topology, PinReference pin, SimulationResultsData results)
    {
        if (!topology.PinToNetMap.TryGetValue(pin, out var net) || net.IsGround) return 0;
        return results.OperatingPointVoltages.GetValueOrDefault(net.NetName);
    }

    private static void DeactivateIndependentSources(CircuitNode circuit)
    {
        foreach (var component in circuit.Components)
        {
            switch (component)
            {
                case VoltageSourceComponent voltage: voltage.WaveformType = "DC"; voltage.Voltage = 0; break;
                case CurrentSourceComponent current: current.WaveformType = "DC"; current.Current = 0; break;
                case PowerComponent power: power.Voltage = 0; break;
                case LogicInputComponent logic: logic.State = false; break;
            }
        }
    }

    private static bool IsIndependentSource(ComponentNode component) => component is
        VoltageSourceComponent or CurrentSourceComponent or PowerComponent or LogicInputComponent;
}
