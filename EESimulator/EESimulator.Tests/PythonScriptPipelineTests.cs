using EESimulator.Commands;
using EESimulator.Core.Models;
using EESimulator.Scripting.API;
using EESimulator.Scripting.Execution;
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

        string[] scriptFiles =
        {
            "test_all_116_components.py",
            "test_component_circuit_tests.py",
            "test_enhanced_component_tests.py",
            "test_complex_linked_circuit_tests.py"
        };

        foreach (string scriptFile in scriptFiles)
        {
            string scriptPath = Path.Combine(pythonRoot, scriptFile);
            _output.WriteLine($"Running managed Python suite: {scriptFile}");
            engine.ExecuteFile(scriptPath);
            _output.WriteLine($"Completed managed Python suite: {scriptFile}");
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
