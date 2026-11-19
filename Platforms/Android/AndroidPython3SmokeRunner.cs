#if ANDROID
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EESimulator.Commands;
using EESimulator.Core.Models;
using EESimulator.Core.UI;
using EESimulator.Engine;
using EESimulator.Formats;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Scripting.Execution;
using Microsoft.Maui.Storage;

namespace EEE;

internal static class AndroidPython3SmokeRunner
{
    internal const string RequestFileName = "eee-python3android-smoke.request";
    private const string ResultFileName = "eee-python3android-smoke.result";
    private const string ExampleFileName = "complex_linked_component_showcase.eee";

    internal static async Task RunAsync()
    {
        string resultPath = Path.Combine(FileSystem.AppDataDirectory, ResultFileName);
        try
        {
            string pythonDirectory = await StagePythonModulesAsync();
            await using Stream exampleStream = await FileSystem.OpenAppPackageFileAsync($"Examples/{ExampleFileName}");
            using var reader = new StreamReader(exampleStream, Encoding.UTF8);
            CircuitNode expectedCircuit = EeePlatformSerializer.LoadFromEeeJson(await reader.ReadToEndAsync());
            var actualCircuit = new CircuitNode();
            var executor = new PythonCommandExecutor(new PythonApi(
                new CommandManager(), actualCircuit, new SimulationManager(), new UIRootNode()));
            var engine = new ManagedPythonScriptEngine(executor);
            string script = PythonCodeGenerator.GenerateFullScript(expectedCircuit);

            await engine.ExecuteAsync(script, ExampleFileName + ".py", new[] { pythonDirectory });

            if (actualCircuit.Components.Count != expectedCircuit.Components.Count ||
                actualCircuit.Connections.Count() != expectedCircuit.Connections.Count())
            {
                throw new InvalidOperationException(
                    $"Python3Android round-trip produced {actualCircuit.Components.Count} components and " +
                    $"{actualCircuit.Connections.Count()} connections; expected " +
                    $"{expectedCircuit.Components.Count} components and {expectedCircuit.Connections.Count()} connections.");
            }

            WriteResult(resultPath,
                $"PASS|components={actualCircuit.Components.Count}|connections={actualCircuit.Connections.Count()}");
        }
        catch (Exception exception)
        {
            WriteResult(resultPath, $"FAIL|{exception}");
        }
    }

    private static async Task<string> StagePythonModulesAsync()
    {
        string directory = Path.Combine(FileSystem.CacheDirectory, "Python3AndroidSmoke");
        Directory.CreateDirectory(directory);
        foreach (string module in new[] { "eesim.py", "circuit.py" })
        {
            await using Stream source = await FileSystem.OpenAppPackageFileAsync($"Python/{module}");
            await using var destination = File.Create(Path.Combine(directory, module));
            await source.CopyToAsync(destination);
        }
        return directory;
    }

    private static void WriteResult(string path, string result)
    {
        File.WriteAllText(path, result, new UTF8Encoding(false));
        Android.Util.Log.Info("EEE_PYTHON3ANDROID_TEST", result);
    }
}
#endif