using EESimulator.Core.Components.General;
using EESimulator.Core.Electrical;
using EESimulator.Core.Models;
using Xunit;
using ComponentRegistry = EESimulator.Core.Components.Registry.ComponentRegistry;

namespace EESimulator.Tests;

public sealed class NetlistBuilderTests
{
    [Fact]
    public void Large_topology_keeps_connected_pins_mapped_to_the_same_net()
    {
        const int componentCount = 130;
        var circuit = new CircuitNode();
        var resistors = Enumerable.Range(0, componentCount)
            .Select(index => ComponentRegistry.CreateInstance("Resistor", $"R{index}"))
            .ToArray();

        foreach (var resistor in resistors)
            circuit.AddChild(resistor);

        for (int index = 0; index < resistors.Length - 1; index++)
        {
            circuit.AddChild(new ConnectionNode(
                resistors[index].Id, resistors[index].Pins[1].Id,
                resistors[index + 1].Id, resistors[index + 1].Pins[0].Id));
        }

        CircuitTopology topology = NetlistBuilder.BuildTopology(circuit);

        Assert.Equal(componentCount + 1, topology.Nets.Count);
        for (int index = 0; index < resistors.Length - 1; index++)
        {
            var leftPin = new PinReference(resistors[index].Id, resistors[index].Pins[1].Id);
            var rightPin = new PinReference(resistors[index + 1].Id, resistors[index + 1].Pins[0].Id);
            Assert.Same(topology.PinToNetMap[leftPin], topology.PinToNetMap[rightPin]);
        }
    }

    [Fact]
    public void Tunnel_net_named_zero_is_not_mistaken_for_ground()
    {
        var circuit = new CircuitNode();
        var first = new TunnelComponent { TunnelLabel = "0" };
        var second = new TunnelComponent { TunnelLabel = "0" };
        circuit.AddChild(first);
        circuit.AddChild(second);

        CircuitTopology topology = NetlistBuilder.BuildTopology(circuit);

        var net = topology.PinToNetMap[new PinReference(first.Id, first.Pins[0].Id)];
        Assert.NotEqual("0", net.NetName);
        Assert.False(net.IsGround);
        Assert.Same(net, topology.PinToNetMap[new PinReference(second.Id, second.Pins[0].Id)]);
    }
}
