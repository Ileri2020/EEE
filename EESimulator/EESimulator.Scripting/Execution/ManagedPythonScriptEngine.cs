using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;

namespace EESimulator.Scripting.Execution;

/// <summary>
/// Runs Python scripts through IronPython so the test process executes the same app scripting
/// pipeline used by the live viewport, without depending on a native CPython runtime.
/// </summary>
public sealed class ManagedPythonScriptBridge
{
    private readonly PythonCommandExecutor _executor;

    public ManagedPythonScriptBridge(PythonCommandExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public Dictionary<string, object?> Execute(string script)
    {
        bool success = _executor.ExecuteScript(script, out string message);
        return new Dictionary<string, object?>
        {
            ["success"] = success,
            ["message"] = message,
            ["result"] = _executor.LastResult
        };
    }
}

public sealed class ManagedPythonScriptEngine
{
    private readonly PythonCommandExecutor _executor;

    public ManagedPythonScriptEngine(PythonCommandExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public void ExecuteFile(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new ArgumentException("Script path is required.", nameof(scriptPath));
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Python script not found: {scriptPath}", scriptPath);

        Console.OutputEncoding = new UTF8Encoding(false);
        ScriptEngine engine = Python.CreateEngine();
        ScriptScope scope = engine.CreateScope();
        var bridge = new ManagedPythonScriptBridge(_executor);

        scope.SetVariable("app_bridge", bridge);
        scope.SetVariable("true", true);
        scope.SetVariable("false", false);
        scope.SetVariable("null", null);
        scope.SetVariable("none", null);
        engine.Execute("import builtins; builtins.app_bridge = app_bridge", scope);

        var searchPaths = engine.GetSearchPaths();
        string? scriptDirectory = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrEmpty(scriptDirectory))
            searchPaths.Add(scriptDirectory);

        string stdlibPath = Path.Combine(AppContext.BaseDirectory, "lib");

        if (Directory.Exists(stdlibPath))
        {
            searchPaths.Add(stdlibPath);
            string sitePackagesPath = Path.Combine(stdlibPath, "site-packages");
            if (Directory.Exists(sitePackagesPath))
                searchPaths.Add(sitePackagesPath);
        }

        engine.SetSearchPaths(searchPaths);
        engine.Execute("import eesim; import circuit", scope);

        engine.ExecuteFile(scriptPath, scope);
    }
}
