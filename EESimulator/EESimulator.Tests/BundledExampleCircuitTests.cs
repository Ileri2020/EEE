using System.Text.RegularExpressions;
using EESimulator.Commands;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Scripting.Execution;
using Xunit;

namespace EESimulator.Tests;

public sealed class BundledExampleCircuitTests
{
    [Fact]
    public void Bundled_examples_use_grid_calls_and_round_trip_through_the_python_api()
    {
        string repositoryRoot = FindRepositoryRoot();
        string examplesDirectory = Path.Combine(repositoryRoot, "Resources", "Raw", "Examples");
        string pythonDirectory = Path.Combine(repositoryRoot, "Resources", "Raw", "Python");
        string[] exampleFiles = Directory.GetFiles(examplesDirectory, "*.eee");

        Assert.NotEmpty(exampleFiles);

        foreach (string exampleFile in exampleFiles)
        {
            string exampleName = Path.GetFileNameWithoutExtension(exampleFile);
            CircuitNode sourceCircuit = EeePlatformSerializer.LoadFromEee(exampleFile);

            Assert.All(sourceCircuit.Components, component =>
            {
                Assert.True(component.HasLogicalPlacement, $"{exampleName}: {component.Name} has no row/column.");
                Assert.True(component.LogicalRow > 0, $"{exampleName}: {component.Name} has an invalid row.");
                Assert.True(component.LogicalColumn > 0, $"{exampleName}: {component.Name} has an invalid column.");
            });

            foreach (ConnectionNode connection in sourceCircuit.Connections)
            {
                ComponentNode from = Assert.IsAssignableFrom<ComponentNode>(
                    sourceCircuit.FindChildren<ComponentNode>().SingleOrDefault(component => component.Id == connection.FromComponentId));
                ComponentNode to = Assert.IsAssignableFrom<ComponentNode>(
                    sourceCircuit.FindChildren<ComponentNode>().SingleOrDefault(component => component.Id == connection.ToComponentId));
                Assert.Contains(from.Pins, pin => pin.Id.Equals(connection.FromPin, StringComparison.OrdinalIgnoreCase));
                Assert.Contains(to.Pins, pin => pin.Id.Equals(connection.ToPin, StringComparison.OrdinalIgnoreCase));
            }

            var simulation = new SpiceSharpAdapter().RunSimulation(sourceCircuit, new SimulationSettings());
            Assert.True(simulation.Success, $"{exampleName}: {simulation.Message}");

            string script = PythonCodeGenerator.GenerateFullScript(sourceCircuit);
            var addStatements = Regex.Matches(script, @"(?m)^.+ = circuit\.add_component\(.+$");
            Assert.Equal(sourceCircuit.Components.Count(), addStatements.Count);
            Assert.All(addStatements.Cast<Match>(), statement =>
            {
                Assert.Contains("row=", statement.Value, StringComparison.Ordinal);
                Assert.Contains("col=", statement.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("position=", statement.Value, StringComparison.Ordinal);
            });

            var generatedCircuit = new CircuitNode();
            var executor = new PythonCommandExecutor(
                new PythonApi(new CommandManager(), generatedCircuit));
            var engine = new ManagedPythonScriptEngine(executor);
            engine.Execute(script, $"{exampleName}.py", new[] { pythonDirectory });

            Assert.Equal(sourceCircuit.Components.Count(), generatedCircuit.Components.Count());
            Assert.Equal(sourceCircuit.Connections.Count(), generatedCircuit.Connections.Count());
            Assert.All(generatedCircuit.Components, component => Assert.True(component.HasLogicalPlacement));
            Assert.All(sourceCircuit.Components.OfType<EESimulator.Core.Components.Electronics.LogicInputComponent>(), input =>
                Assert.Equal(input.State, Assert.IsType<EESimulator.Core.Components.Electronics.LogicInputComponent>(
                    generatedCircuit.FindChild<ComponentNode>(input.Name)).State));
        }
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
