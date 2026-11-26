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
