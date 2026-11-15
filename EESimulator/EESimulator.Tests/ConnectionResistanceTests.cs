using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.General;
using EESimulator.Core.Electrical;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;
using Xunit;

namespace EESimulator.Tests;

public sealed class ConnectionResistanceTests
{
    [Fact]
    public void Resistive_wire_is_simulated_as_a_real_series_branch()
    {
        var circuit = new CircuitNode();
        var source = new VoltageSourceComponent { Name = "V1", Voltage = 10 };
        var load = new ResistorComponent { Name = "R1", Resistance = 1000 };
        var ground = new GroundNode { Name = "GND" };
        circuit.AddChild(source);
        circuit.AddChild(load);
        circuit.AddChild(ground);
        circuit.AddChild(new ConnectionNode(source.Id, "positive", load.Id, "pin1")
        {
            Resistance = 1000
        });
        circuit.AddChild(new ConnectionNode(load.Id, "pin2", ground.Id, "gnd"));
        circuit.AddChild(new ConnectionNode(source.Id, "negative", ground.Id, "gnd"));

        CircuitTopology topology = NetlistBuilder.BuildTopology(circuit);
        Assert.Single(topology.ResistiveConnections);

        var result = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(result.Success, result.Message);
        Assert.InRange(load.LivePinVoltages["pin1"], 4.9, 5.1);
    }
}
