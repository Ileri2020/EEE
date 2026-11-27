using EESimulator.Commands;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Models;
using EESimulator.Scripting.API;
using EESimulator.Scripting.Execution;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace EESimulator.Tests;

public sealed class PythonScriptPipelineTests
{
    private readonly ITestOutputHelper _output;

    public PythonScriptPipelineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void All_python_test_scripts_run_through_the_app_scripting_executor()
    {
        string repositoryRoot = FindRepositoryRoot();
        string pythonRoot = Path.Combine(repositoryRoot, "EESimulator", "EESimulator.Tests", "Python");

        var executor = new PythonCommandExecutor(new PythonApi(new CommandManager(), new CircuitNode()));
        var engine = new ManagedPythonScriptEngine(executor);
        string[] scriptFiles = Directory.GetFiles(pythonRoot, "test_*.py")
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(scriptFiles);

        foreach (string scriptFile in scriptFiles)
        {
            _output.WriteLine($"Running managed Python suite: {Path.GetFileName(scriptFile)}");
            engine.ExecuteFile(scriptFile);
            _output.WriteLine($"Completed managed Python suite: {Path.GetFileName(scriptFile)}");
        }
    }

    [Fact]
    public void Python_script_can_remove_a_wire_connection()
    {
        var circuit = new CircuitNode();
        var source = new VoltageSourceComponent { Name = "V1" };
        var resistor = new ResistorComponent { Name = "R1" };
        circuit.AddChild(source);
        circuit.AddChild(resistor);
        circuit.AddChild(new ConnectionNode(source.Id, "positive", resistor.Id, "pin1"));

        var executor = new PythonCommandExecutor(new PythonApi(new CommandManager(), circuit));
        bool success = executor.ExecuteScript(
            "circuit.remove_connection(\"V1\", \"positive\", \"R1\", \"pin1\")",
            out string message);

        Assert.True(success, message);
        Assert.Empty(circuit.Connections);
    }

    [Fact]
    public void Python_script_with_logical_grid_waypoints_and_nodes_resolves_coordinates_cleanly()
    {
        var circuit = new CircuitNode();
        var api = new PythonApi(new CommandManager(), circuit);
        var executor = new PythonCommandExecutor(api);

        string script = @"
eesim.set_logical_grid_spacing(2, 2)
vcc = circuit.add_component(""electrical.voltagesource"", name=""VCC"", row=1, col=5)
circuit.add_ground(""GND_VCC"", row=2, col=5)
circuit.add_resistor(""Rc"", 3300, row=3, col=7)
circuit.add_node(""NODE_BASE"", row=7, col=4)
eesim.connect(""VCC"", ""positive"", ""Rc"", ""pin1"", waypoints=[(1, 7)])
eesim.connect(""Rc"", ""pin2"", ""NODE_BASE"", ""pin"", waypoints=[(5, 7), (5, 4)])
";
        bool success = executor.ExecuteScript(script, out string message);
        Assert.True(success, message);

        var nodeBase = circuit.FindChild<ComponentNode>("NODE_BASE");
        Assert.NotNull(nodeBase);
        Assert.Equal(7, nodeBase.LogicalRow);
        Assert.Equal(4, nodeBase.LogicalColumn);

        var conn = circuit.Connections.FirstOrDefault(c =>
            (c.FromComponentId == circuit.FindChild<ComponentNode>("VCC")!.Id &&
             c.ToComponentId == circuit.FindChild<ComponentNode>("Rc")!.Id) ||
            (c.FromComponentId == circuit.FindChild<ComponentNode>("Rc")!.Id &&
             c.ToComponentId == circuit.FindChild<ComponentNode>("VCC")!.Id));

        Assert.NotNull(conn);
        Assert.Single(conn.RoutingPoints);
        // The waypoint (row=1, col=7) should map to logical column 7 (X > 100) and row 1 (Y > 50), NOT raw pixel (1, 7)!
        Assert.True(conn.RoutingPoints[0].X > 100, $"Expected X > 100 but was {conn.RoutingPoints[0].X}");
        Assert.True(conn.RoutingPoints[0].Y > 50, $"Expected Y > 50 but was {conn.RoutingPoints[0].Y}");
    }

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "EEE.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the EEE repository root.");
    }
}
