using EESimulator.Core.Electrical;
using EESimulator.Core.Models;
using EESimulator.Core.Components.General;
using EESimulator.Commands;
using EESimulator.Commands.Commands;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Scripting.Execution;
using Xunit;

namespace EESimulator.Tests;

public sealed class GroundGroupTests
{
    [Fact]
    public void GroundsInSameGroupShareANet_AndDifferentGroupsRemainSeparate()
    {
        var circuit = new CircuitNode();
        var defaultGround = new GroundNode("GND_DEFAULT");
        var houseGround = new GroundNode("GND_HOUSE") { GroundGroup = "House" };
        var houseGround2 = new GroundNode("GND_HOUSE_2") { GroundGroup = "house" };
        var logicGround = new GroundNode("GND_LOGIC") { GroundGroup = "Logic" };
        circuit.AddChild(defaultGround);
        circuit.AddChild(houseGround);
        circuit.AddChild(houseGround2);
        circuit.AddChild(logicGround);

        CircuitTopology topology = NetlistBuilder.BuildTopology(circuit);
        NetNode defaultNet = topology.PinToNetMap[new PinReference(defaultGround.Id, "gnd")];
        NetNode houseNet = topology.PinToNetMap[new PinReference(houseGround.Id, "gnd")];
        NetNode houseNet2 = topology.PinToNetMap[new PinReference(houseGround2.Id, "gnd")];
        NetNode logicNet = topology.PinToNetMap[new PinReference(logicGround.Id, "gnd")];

        Assert.True(defaultNet.IsGround);
        Assert.Same(houseNet, houseNet2);
        Assert.NotSame(defaultNet, houseNet);
        Assert.NotSame(houseNet, logicNet);
        Assert.Equal("House", houseNet.GroundGroup);
        Assert.Equal("Logic", logicNet.GroundGroup);
        Assert.False(houseNet.IsGround);
        Assert.False(logicNet.IsGround);
    }

    [Fact]
    public void NamedGroundGroupRemainsAnIndependentReferenceInSimulation()
    {
        var circuit = new CircuitNode();
        var globalGround = new GroundNode("GND_GLOBAL");
        var isolatedGround = new GroundNode("GND_ISOLATED") { GroundGroup = "IsolatedSupply" };
        var source = new VoltageSourceNode("V_ISOLATED", 5);
        circuit.AddChild(globalGround);
        circuit.AddChild(isolatedGround);
        circuit.AddChild(source);
        circuit.AddChild(new ConnectionNode(source.Id, "positive", isolatedGround.Id, "gnd"));
        circuit.AddChild(new ConnectionNode(source.Id, "negative", globalGround.Id, "gnd"));

        var result = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(result.Success, result.Message);
        NetNode isolatedNet = NetlistBuilder.BuildTopology(circuit)
            .PinToNetMap[new PinReference(isolatedGround.Id, "gnd")];
        Assert.True(result.OperatingPointVoltages.TryGetValue(isolatedNet.NetName, out double voltage));
        Assert.Equal(5, voltage, 3);
    }

    [Fact]
    public void ProbeMeasuresVoltageRelativeToItsNamedGroundGroup()
    {
        var circuit = new CircuitNode();
        var globalGround = new GroundNode("GND_GLOBAL");
        var isolatedGround = new GroundNode("GND_ISOLATED") { GroundGroup = "IsolatedSupply" };
        var source = new VoltageSourceNode("V_ISOLATED", 5);
        var probe = new ProbeComponent("TP_GLOBAL")
        {
            ReferenceGroundGroup = "IsolatedSupply"
        };
        circuit.AddChild(globalGround);
        circuit.AddChild(isolatedGround);
        circuit.AddChild(source);
        circuit.AddChild(probe);
        circuit.AddChild(new ConnectionNode(source.Id, "positive", isolatedGround.Id, "gnd"));
        circuit.AddChild(new ConnectionNode(source.Id, "negative", globalGround.Id, "gnd"));
        circuit.AddChild(new ConnectionNode(probe.Id, "in", globalGround.Id, "gnd"));

        var result = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.True(result.Success, result.Message);
        NetNode referenceNet = NetlistBuilder.BuildTopology(circuit).Nets.Single(net =>
            net.GroundGroup == "IsolatedSupply");
        double referenceVoltage = result.OperatingPointVoltages.GetValueOrDefault(referenceNet.NetName, double.NaN);
        double reportedVoltage = result.OperatingPointVoltages.GetValueOrDefault("V(TP_GLOBAL)", double.NaN);
        Assert.True(Math.Abs(probe.LiveVoltage + 5) < 0.001,
            $"Expected -5 V; probe={probe.LiveVoltage:G17} V; reference={referenceVoltage:G17} V; " +
            $"reported={reportedVoltage:G17} V; {result.Message}");
    }

    [Fact]
    public void ProbeReportsAMissingReferenceGroupAsASimulationError()
    {
        var circuit = new CircuitNode();
        var ground = new GroundNode("GND");
        var probe = new ProbeComponent("TP")
        {
            ReferenceGroundGroup = "Missing"
        };
        circuit.AddChild(ground);
        circuit.AddChild(probe);
        circuit.AddChild(new ConnectionNode(probe.Id, "in", ground.Id, "gnd"));

        var result = new SpiceSharpAdapter().RunSimulation(circuit, new SimulationSettings());

        Assert.False(result.Success);
        Assert.Contains("Probe reference ground group 'Missing' does not exist", result.Message);
    }

    [Fact]
    public void ChangingProbeReferenceGroupIsUndoableAndRedoable()
    {
        var circuit = new CircuitNode();
        var probe = new ProbeComponent("TP") { ReferenceGroundGroup = "House" };
        circuit.AddChild(probe);
        var manager = new CommandManager();

        manager.ExecuteCommand(new SetComponentPropertyCommand(
            circuit, probe.Name, nameof(ProbeComponent.ReferenceGroundGroup), "Logic"));

        Assert.Equal("Logic", probe.ReferenceGroundGroup);
        manager.Undo();
        Assert.Equal("House", probe.ReferenceGroundGroup);
        manager.Redo();
        Assert.Equal("Logic", probe.ReferenceGroundGroup);
    }

    [Fact]
    public void NamedGroundGroupSurvivesEeeSerialization()
    {
        var circuit = new CircuitNode();
        var ground = new GroundNode("GND_HOUSE") { GroundGroup = "House" };
        var probe = new ProbeComponent("TP_HOUSE") { ReferenceGroundGroup = "House" };
        circuit.AddChild(ground);
        circuit.AddChild(probe);

        CircuitNode restored = EeePlatformSerializer.LoadFromEeeJson(EeePlatformSerializer.ExportToEee(circuit));

        Assert.Equal("House", Assert.IsType<GroundNode>(restored.Components.Single(component => component.Name == "GND_HOUSE")).GroundGroup);
        Assert.Equal("House", Assert.IsType<ProbeComponent>(restored.Components.Single(component => component.Name == "TP_HOUSE")).ReferenceGroundGroup);
    }

    [Fact]
    public void ChangingGroundGroupIsUndoableAndInvalidatesCircuitTopology()
    {
        var circuit = new CircuitNode();
        var ground = new GroundNode("GND") { GroundGroup = "House" };
        circuit.AddChild(ground);
        var manager = new CommandManager();
        long version = circuit.StructureVersion;

        manager.ExecuteCommand(new SetComponentPropertyCommand(
            circuit, ground.Name, nameof(GroundNode.GroundGroup), "Logic"));
        long changedVersion = circuit.StructureVersion;

        Assert.Equal("Logic", ground.GroundGroup);
        Assert.True(changedVersion > version);
        manager.Undo();
        Assert.Equal("House", ground.GroundGroup);
        manager.Redo();
        Assert.Equal("Logic", ground.GroundGroup);
    }

    [Fact]
    public void GroundGroupPropertyRoundTripsThroughGeneratedPythonApi()
    {
        var sourceCircuit = new CircuitNode();
        sourceCircuit.AddChild(new GroundNode("GND_GRID")
        {
            GroundGroup = "Grid",
            LogicalRow = 1,
            LogicalColumn = 2
        });
        sourceCircuit.AddChild(new GroundNode("GND_HOUSE")
        {
            GroundGroup = "House",
            LogicalRow = 3,
            LogicalColumn = 4
        });
        sourceCircuit.AddChild(new ProbeComponent("TP_HOUSE")
        {
            ReferenceGroundGroup = "House",
            LogicalRow = 5,
            LogicalColumn = 6
        });
        string script = PythonCodeGenerator.GenerateFullScript(sourceCircuit);
        var generatedCircuit = new CircuitNode();
        var executor = new PythonCommandExecutor(
            new PythonApi(new CommandManager(), generatedCircuit));
        var engine = new ManagedPythonScriptEngine(executor);

        engine.Execute(script, "ground_groups.py", Array.Empty<string>());

        var generatedGridGround = Assert.IsType<GroundNode>(generatedCircuit.FindChild<ComponentNode>("GND_GRID"));
        var generatedHouseGround = Assert.IsType<GroundNode>(generatedCircuit.FindChild<ComponentNode>("GND_HOUSE"));
        var generatedProbe = Assert.IsType<ProbeComponent>(generatedCircuit.FindChild<ComponentNode>("TP_HOUSE"));
        Assert.Equal("Grid", generatedGridGround.GroundGroup);
        Assert.Equal("House", generatedHouseGround.GroundGroup);
        Assert.Equal((1, 2), (generatedGridGround.LogicalRow, generatedGridGround.LogicalColumn));
        Assert.Equal((3, 4), (generatedHouseGround.LogicalRow, generatedHouseGround.LogicalColumn));
        Assert.Equal("House", generatedProbe.ReferenceGroundGroup);
        Assert.Equal((5, 6), (generatedProbe.LogicalRow, generatedProbe.LogicalColumn));
    }
}
