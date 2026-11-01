using System;
using System.Text.RegularExpressions;
using EESimulator.Scripting.API;

namespace EESimulator.Scripting.Execution;

/// <summary>
/// Lightweight parser and executor that parses Python statements written by the user in the Script Viewport
/// and dispatches them through the CommandManager to update the schematic.
/// </summary>
public class PythonCommandExecutor
{
    private readonly PythonApi _api;

    public PythonCommandExecutor(PythonApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public bool ExecuteScript(string script, out string message)
    {
        message = "Success";
        if (string.IsNullOrWhiteSpace(script))
            return true;

        var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int lineNum = 0;

        foreach (var rawLine in lines)
        {
            lineNum++;
            var line = rawLine.Trim();
            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                ExecuteLine(line);
            }
            catch (Exception ex)
            {
                message = $"Line {lineNum} error: {ex.Message}";
                return false;
            }
        }

        return true;
    }

    private void ExecuteLine(string line)
    {
        // Example: r1 = circuit.add_component("Resistor", name="R1", position=(100, 200), resistance=1000)
        var addMatch = Regex.Match(line, @"add_component\s*\(\s*""([^""]+)""\s*,\s*name\s*=\s*""([^""]+)""\s*,\s*position\s*=\s*\(\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*\)(?:\s*,\s*(\w+)\s*=\s*([\d\.-]+))?\s*\)", RegexOptions.IgnoreCase);
        if (addMatch.Success)
        {
            string type = addMatch.Groups[1].Value;
            string name = addMatch.Groups[2].Value;
            double x = double.Parse(addMatch.Groups[3].Value);
            double y = double.Parse(addMatch.Groups[4].Value);
            double param = addMatch.Groups[6].Success ? double.Parse(addMatch.Groups[6].Value) : 0.0;

            _api.AddComponent(type, name, x, y, param);
            return;
        }

        // Example: circuit.connect(v1.positive, r1.pin1)
        var connMatch = Regex.Match(line, @"circuit\.connect\s*\(\s*(\w+)\.(\w+)\s*,\s*(\w+)\.(\w+)\s*\)", RegexOptions.IgnoreCase);
        if (connMatch.Success)
        {
            string fromComp = connMatch.Groups[1].Value;
            string fromPin = connMatch.Groups[2].Value;
            string toComp = connMatch.Groups[3].Value;
            string toPin = connMatch.Groups[4].Value;

            _api.Connect(fromComp, fromPin, toComp, toPin);
            return;
        }

        // Example: app.open_window("Properties")
        var winMatch = Regex.Match(line, @"app\.open_window\s*\(\s*""([^""]+)""\s*\)", RegexOptions.IgnoreCase);
        if (winMatch.Success)
        {
            _api.OpenWindow(winMatch.Groups[1].Value);
            return;
        }

        // Example: r1.position = (300, 200)
        var posMatch = Regex.Match(line, @"(\w+)\.position\s*=\s*\(\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*\)", RegexOptions.IgnoreCase);
        if (posMatch.Success)
        {
            string name = posMatch.Groups[1].Value;
            double x = double.Parse(posMatch.Groups[2].Value);
            double y = double.Parse(posMatch.Groups[3].Value);

            _api.SetPosition(name, x, y);
            return;
        }

        // Example: r1.resistance = 2200
        var propMatch = Regex.Match(line, @"(\w+)\.(\w+)\s*=\s*([\d\.-]+)", RegexOptions.IgnoreCase);
        if (propMatch.Success)
        {
            string name = propMatch.Groups[1].Value;
            string prop = propMatch.Groups[2].Value;
            double val = double.Parse(propMatch.Groups[3].Value);

            _api.SetProperty(name, prop, val);
            return;
        }
    }
}
