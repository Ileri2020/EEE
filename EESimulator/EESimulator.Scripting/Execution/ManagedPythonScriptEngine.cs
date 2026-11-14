using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private readonly Func<string, Dictionary<string, object?>> _execute;

    public ManagedPythonScriptBridge(PythonCommandExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        _execute = script =>
        {
            bool success = executor.ExecuteScript(script, out string message);
            return new Dictionary<string, object?>
            {
                ["success"] = success,
                ["message"] = message,
                ["result"] = executor.LastResult
            };
        };
    }

    public ManagedPythonScriptBridge(Func<string, Dictionary<string, object?>> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public Dictionary<string, object?> Execute(string script) => _execute(script);
}

public sealed class ManagedPythonScriptEngine
{
    private readonly ManagedPythonScriptBridge _bridge;

    public ManagedPythonScriptEngine(PythonCommandExecutor executor)
        : this(new ManagedPythonScriptBridge(executor))
    {
    }

    public ManagedPythonScriptEngine(ManagedPythonScriptBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public void ExecuteFile(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new ArgumentException("Script path is required.", nameof(scriptPath));
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Python script not found: {scriptPath}", scriptPath);

        Execute(File.ReadAllText(scriptPath), scriptPath,
            new[] { Path.GetDirectoryName(scriptPath) ?? string.Empty });
    }

    public void Execute(string script, string scriptName, IEnumerable<string>? additionalSearchPaths = null,
        Action<string>? onOutput = null)
    {
        if (script is null)
            throw new ArgumentNullException(nameof(script));
        if (string.IsNullOrWhiteSpace(scriptName))
            throw new ArgumentException("Script name is required.", nameof(scriptName));

        Console.OutputEncoding = new UTF8Encoding(false);
        ScriptEngine engine = Python.CreateEngine();
        ScriptScope scope = engine.CreateScope();

        scope.SetVariable("app_bridge", _bridge);
        scope.SetVariable("true", true);
        scope.SetVariable("false", false);
        scope.SetVariable("null", null);
        scope.SetVariable("none", null);
        engine.Execute("import builtins; builtins.app_bridge = app_bridge", scope);

        var searchPaths = engine.GetSearchPaths();
        foreach (string searchPath in additionalSearchPaths ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(searchPath) && Directory.Exists(searchPath))
                searchPaths.Add(searchPath);
        }

        string runtimePythonPath = Path.Combine(AppContext.BaseDirectory, "Python");
        if (Directory.Exists(runtimePythonPath))
            searchPaths.Add(runtimePythonPath);

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

        using var output = new MemoryStream();
        engine.Runtime.IO.SetOutput(output, new UTF8Encoding(false));
        try
        {
            engine.CreateScriptSourceFromString(script, scriptName, Microsoft.Scripting.SourceCodeKind.File)
                .Execute(scope);
        }
        finally
        {
            output.Flush();
            if (output.Length > 0)
                onOutput?.Invoke(Encoding.UTF8.GetString(output.ToArray()));
        }
    }
}
