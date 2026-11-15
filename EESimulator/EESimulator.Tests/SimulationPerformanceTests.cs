using System.Linq;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Electrical;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;
using Xunit;

namespace EESimulator.Tests;

public sealed class SimulationPerformanceTests
{
    [Fact]
    public void Runtime_state_changes_do_not_invalidate_circuit_structure()
    {
        var circuit = new CircuitNode();
        var clock = new ClockComponent();
        circuit.AddChild(clock);

        long structureVersion = circuit.StructureVersion;
        long stateVersion = circuit.StateVersion;
        clock.State = !clock.State;

        Assert.Equal(structureVersion, circuit.StructureVersion);
        Assert.True(circuit.StateVersion > stateVersion);
    }

    [Fact]
    public void Component_cache_tracks_hierarchy_changes()
    {
        var circuit = new CircuitNode();
        var first = new LogicInputComponent();
        circuit.AddChild(first);

        Assert.Contains(first, circuit.Components);

        var second = new LogicGateComponent();
        circuit.AddChild(second);
        Assert.Contains(second, circuit.Components);

        circuit.RemoveChild(first);
        Assert.DoesNotContain(first, circuit.Components);
    }

    [Fact]
    public void Combinational_gates_evaluate_in_dependency_layers()
    {
        var circuit = new CircuitNode();
        var highA = new LogicInputComponent { State = true };
        var highB = new LogicInputComponent { State = true };
        circuit.AddChild(highA);
        circuit.AddChild(highB);

        var parallelGates = Enumerable.Range(0, 5)
            .Select(index => new LogicGateComponent($"G{index}", LogicGateType.AND))
            .ToArray();
        foreach (var gate in parallelGates)
            circuit.AddChild(gate);

        var finalGate = new LogicGateComponent("G_FINAL", LogicGateType.AND);
        circuit.AddChild(finalGate);

        foreach (var gate in parallelGates)
        {
            circuit.AddChild(new ConnectionNode(highA.Id, "out", gate.Id, "in1"));
            circuit.AddChild(new ConnectionNode(highB.Id, "out", gate.Id, "in2"));
        }

        circuit.AddChild(new ConnectionNode(parallelGates[0].Id, "out", finalGate.Id, "in1"));
        circuit.AddChild(new ConnectionNode(highA.Id, "out", finalGate.Id, "in2"));

        DigitalLogicSimulator.EvaluateCombinational(circuit);

        Assert.True(DigitalLogicSimulator.ReadOutputState(finalGate, "out"));
    }
}
