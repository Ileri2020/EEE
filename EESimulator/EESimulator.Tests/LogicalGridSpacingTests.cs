using EESimulator.Commands;
using EESimulator.Commands.Commands;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;
using EESimulator.Formats;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Scripting.Execution;
using Xunit;

namespace EESimulator.Tests;

public sealed class LogicalGridSpacingTests
{
    [Fact]
    public void Spacing_changes_reflow_logical_components_and_support_undo_redo()
    {
        CircuitNode circuit = CreateGridCircuit();
        ComponentNode topLeft = Assert.IsType<ResistorComponent>(circuit.FindChild<ComponentNode>("R11"));
        ComponentNode bottomLeft = Assert.IsType<ResistorComponent>(circuit.FindChild<ComponentNode>("R21"));
        ComponentNode topRight = Assert.IsType<ResistorComponent>(circuit.FindChild<ComponentNode>("R12"));
        ComponentNode bottomRight = Assert.IsType<ResistorComponent>(circuit.FindChild<ComponentNode>("R22"));
        var commandManager = new CommandManager();
        ApplyLogicalGridLayout(circuit);
        Assert.Empty(LogicalGridSchematicLayout.Calculate(circuit).UnresolvedCollisions);
        double minimumRowDistance = bottomLeft.Y - topLeft.Y;
        double minimumColumnDistance = topRight.X - topLeft.X;

        var command = new SetLogicalGridSpacingCommand(circuit, 60, 80);
        Assert.Contains("eesim.set_logical_grid_spacing(60, 80)", command.ToPythonScript(), StringComparison.Ordinal);
        Assert.True(commandManager.ExecuteCommand(command));
        Assert.Equal(60, circuit.LogicalGridRowSpacing);
        Assert.Equal(80, circuit.LogicalGridColumnSpacing);
        Assert.Empty(LogicalGridSchematicLayout.Calculate(circuit).UnresolvedCollisions);

        double expandedRowDistance = bottomLeft.Y - topLeft.Y;
        double expandedColumnDistance = topRight.X - topLeft.X;
        Assert.Equal(minimumRowDistance + 58, expandedRowDistance);
        Assert.Equal(minimumColumnDistance + 78, expandedColumnDistance);
        Assert.True(commandManager.Undo());
        Assert.Equal(CircuitNode.MinimumLogicalGridSpacing, circuit.LogicalGridRowSpacing);
        Assert.Equal(CircuitNode.MinimumLogicalGridSpacing, circuit.LogicalGridColumnSpacing);
        Assert.Equal(minimumRowDistance, bottomLeft.Y - topLeft.Y);
        Assert.Equal(minimumColumnDistance, topRight.X - topLeft.X);

        Assert.True(commandManager.Redo());
        Assert.Equal(60, circuit.LogicalGridRowSpacing);
        Assert.Equal(80, circuit.LogicalGridColumnSpacing);
        Assert.Equal(expandedRowDistance, bottomRight.Y - topRight.Y);
    }

    [Fact]
    public void Python_api_and_eee_round_trip_preserve_logical_grid_spacing()
    {
        CircuitNode circuit = CreateGridCircuit();
        var commandManager = new CommandManager();
        var executor = new PythonCommandExecutor(new PythonApi(commandManager, circuit));

        Assert.True(executor.ExecuteScript("eesim.set_logical_grid_spacing(40, 60)", out string message), message);
        Assert.Equal(40, circuit.LogicalGridRowSpacing);
        Assert.Equal(60, circuit.LogicalGridColumnSpacing);
        Assert.True(commandManager.Undo());
        Assert.Equal(CircuitNode.MinimumLogicalGridSpacing, circuit.LogicalGridRowSpacing);
        Assert.True(commandManager.Redo());
        Assert.Equal(60, circuit.LogicalGridColumnSpacing);

        string generatedScript = PythonCodeGenerator.GenerateFullScript(circuit);
        Assert.Contains("eesim.set_logical_grid_spacing(40, 60)", generatedScript, StringComparison.Ordinal);
        CircuitNode restored = EeePlatformSerializer.LoadFromEeeJson(EeePlatformSerializer.ExportToEee(circuit));
        Assert.Equal(circuit.LogicalGridRowSpacing, restored.LogicalGridRowSpacing);
        Assert.Equal(circuit.LogicalGridColumnSpacing, restored.LogicalGridColumnSpacing);
    }

    [Fact]
    public void Eee_round_trip_rebuilds_parameterized_component_pins()
    {
        var circuit = new CircuitNode();
        circuit.AddChild(new MultiplexerComponent("MUX")
        {
            SelectBits = 1,
            BitWidth = 4
        });

        CircuitNode restored = EeePlatformSerializer.LoadFromEeeJson(EeePlatformSerializer.ExportToEee(circuit));
        var multiplexer = Assert.IsType<MultiplexerComponent>(restored.FindChild<ComponentNode>("MUX"));

        Assert.Contains(multiplexer.Pins, pin => pin.Id == "in0_b0");
        Assert.Contains(multiplexer.Pins, pin => pin.Id == "in1_b3");
        Assert.Contains(multiplexer.Pins, pin => pin.Id == "out_b3");
    }

    [Fact]
    public void Grid_spacing_cannot_be_reduced_below_the_safe_minimum()
    {
        CircuitNode circuit = CreateGridCircuit();
        var commandManager = new CommandManager();
        var executor = new PythonCommandExecutor(new PythonApi(commandManager, circuit));

        Assert.False(executor.ExecuteScript("eesim.set_logical_grid_spacing(0, 20)", out _));
        Assert.Equal(CircuitNode.MinimumLogicalGridSpacing, circuit.LogicalGridRowSpacing);

        Assert.False(commandManager.ExecuteCommand(new SetLogicalGridSpacingCommand(
            circuit, CircuitNode.MinimumLogicalGridSpacing, CircuitNode.MinimumLogicalGridSpacing)));
    }

    [Fact]
    public void Logical_layout_supports_circuits_that_only_contain_wires()
    {
        var circuit = new CircuitNode();
        circuit.AddChild(new WireComponent
        {
            Name = "W1",
            LogicalRow = 1,
            LogicalColumn = 1
        });

        LogicalGridLayoutResult layout = LogicalGridSchematicLayout.Calculate(circuit);

        Assert.Single(layout.Placements);
        Assert.Empty(layout.UnresolvedCollisions);
    }

    private static CircuitNode CreateGridCircuit()
    {
        var circuit = new CircuitNode();
        AddResistor(circuit, "R11", 1, 1);
        AddResistor(circuit, "R21", 2, 1);
        AddResistor(circuit, "R12", 1, 2);
        AddResistor(circuit, "R22", 2, 2);
        return circuit;
    }

    private static void ApplyLogicalGridLayout(CircuitNode circuit)
    {
        foreach (LogicalGridPlacement placement in LogicalGridSchematicLayout.Calculate(circuit).Placements)
        {
            placement.Component.X = placement.X;
            placement.Component.Y = placement.Y;
        }
    }

    private static void AddResistor(CircuitNode circuit, string name, int row, int column)
    {
        var resistor = new ResistorComponent(name)
        {
            LogicalRow = row,
            LogicalColumn = column
        };
        circuit.AddChild(resistor);
    }
}
