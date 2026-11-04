using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
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
        // Example: eesim.set_component_property("R1", "Resistance", 2200)
        var propertyMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.set_component_property\s*\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*(true|false|""[^""]*""|'[^']*'|[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (propertyMatch.Success)
        {
            string componentName = propertyMatch.Groups[1].Value;
            string propertyName = propertyMatch.Groups[2].Value;
            string rawValue = propertyMatch.Groups[3].Value;
            object value = rawValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? true
                : rawValue.Equals("false", StringComparison.OrdinalIgnoreCase) ? false
                : rawValue.Length >= 2 && rawValue[0] == '"' ? JsonSerializer.Deserialize<string>(rawValue) ?? string.Empty
                : rawValue.Length >= 2 && rawValue[0] == '\'' ? rawValue[1..^1]
                : double.Parse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture);
            _api.SetComponentProperty(componentName, propertyName, value);
            return;
        }

        // Example: r1 = circuit.add_component("Resistor", name="R1", position=(100, 200), resistance=1000)
        var positionalAddMatch = Regex.Match(line, @"(?:circuit|eesim)\.add_component\s*\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*([\d\.-]+)\s*,\s*([\d\.-]+)(?:\s*,\s*([\d\.-]+))?\s*\)", RegexOptions.IgnoreCase);
        if (positionalAddMatch.Success)
        {
            string type = positionalAddMatch.Groups[1].Value;
            string name = positionalAddMatch.Groups[2].Value;
            double x = double.Parse(positionalAddMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            double y = double.Parse(positionalAddMatch.Groups[4].Value, CultureInfo.InvariantCulture);
            double param = positionalAddMatch.Groups[5].Success
                ? double.Parse(positionalAddMatch.Groups[5].Value, CultureInfo.InvariantCulture)
                : 0.0;
            _api.AddComponent(type, name, x, y, param);
            return;
        }

        var addMatch = Regex.Match(line, @"add_component\s*\(\s*""([^""]+)""\s*,\s*name\s*=\s*""([^""]+)""\s*,\s*position\s*=\s*\(\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*\)(?:\s*,\s*(\w+)\s*=\s*([\d\.-]+))?\s*\)", RegexOptions.IgnoreCase);
        if (addMatch.Success)
        {
            string type = addMatch.Groups[1].Value;
            string name = addMatch.Groups[2].Value;
            double x = double.Parse(addMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            double y = double.Parse(addMatch.Groups[4].Value, CultureInfo.InvariantCulture);
            double param = addMatch.Groups[6].Success ? double.Parse(addMatch.Groups[6].Value, CultureInfo.InvariantCulture) : 0.0;
            _api.AddComponent(type, name, x, y, param);
            return;
        }

        // Example: circuit.connect(v1.positive, r1.pin1)
        var connMatch = Regex.Match(line, @"(?:circuit|eesim)\.connect\s*\(\s*(\w+)\.(\w+)\s*,\s*(\w+)\.(\w+)\s*\)", RegexOptions.IgnoreCase);
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

        // Undo / Redo: circuit.undo(), app.undo(), undo()
        if (Regex.IsMatch(line, @"^(?:(?:circuit|app)\.)?undo\s*\(\s*\)$", RegexOptions.IgnoreCase))
        {
            _api.Undo();
            return;
        }

        if (Regex.IsMatch(line, @"^(?:(?:circuit|app)\.)?redo\s*\(\s*\)$", RegexOptions.IgnoreCase))
        {
            _api.Redo();
            return;
        }

        // Copy: circuit.copy_component("R1") or app.copy_component("R1")
        var copyMatch = Regex.Match(line, @"(?:circuit|app)\.copy_component\s*\(\s*""([^""]+)""\s*\)", RegexOptions.IgnoreCase);
        if (copyMatch.Success)
        {
            _api.CopyComponent(copyMatch.Groups[1].Value);
            return;
        }

        // Remove / Delete: circuit.remove_component("R1") or circuit.delete_component("R1")
        var removeMatch = Regex.Match(line, @"(?:circuit|app)\.(?:remove_component|delete_component)\s*\(\s*""([^""]+)""\s*\)", RegexOptions.IgnoreCase);
        if (removeMatch.Success)
        {
            _api.DeleteComponent(removeMatch.Groups[1].Value);
            return;
        }

        // Rotate: r1.rotate(90) or circuit.rotate_component("R1", 90)
        var rotMatch = Regex.Match(line, @"(?:circuit\.)?rotate_component\s*\(\s*""([^""]+)""\s*,\s*([\d\.-]+)\s*\)", RegexOptions.IgnoreCase);
        if (rotMatch.Success)
        {
            _api.RotateComponent(rotMatch.Groups[1].Value, double.Parse(rotMatch.Groups[2].Value));
            return;
        }
        var compRotMatch = Regex.Match(line, @"(\w+)\.rotate\s*\(\s*([\d\.-]+)\s*\)", RegexOptions.IgnoreCase);
        if (compRotMatch.Success)
        {
            _api.RotateComponent(compRotMatch.Groups[1].Value, double.Parse(compRotMatch.Groups[2].Value));
            return;
        }

        // Stretch: r1.stretch(1.5, 2.0) or circuit.stretch_component("R1", 1.5, 2.0)
        var stretchMatch = Regex.Match(line, @"(?:circuit\.)?stretch_component\s*\(\s*""([^""]+)""\s*,\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*\)", RegexOptions.IgnoreCase);
        if (stretchMatch.Success)
        {
            _api.StretchComponent(stretchMatch.Groups[1].Value, double.Parse(stretchMatch.Groups[2].Value), double.Parse(stretchMatch.Groups[3].Value));
            return;
        }
        var compStretchMatch = Regex.Match(line, @"(\w+)\.stretch\s*\(\s*([\d\.-]+)\s*,\s*([\d\.-]+)\s*\)", RegexOptions.IgnoreCase);
        if (compStretchMatch.Success)
        {
            _api.StretchComponent(compStretchMatch.Groups[1].Value, double.Parse(compStretchMatch.Groups[2].Value), double.Parse(compStretchMatch.Groups[3].Value));
            return;
        }

        // Sidebars & UI
        if (Regex.IsMatch(line, @"app\.toggle_component_sidebar\s*\(\s*\)", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(line, @"app\.toggle_sidebar\s*\(\s*\)", RegexOptions.IgnoreCase))
        {
            _api.ToggleComponentSidebar();
            return;
        }
        if (Regex.IsMatch(line, @"app\.toggle_control_sidebar\s*\(\s*\)", RegexOptions.IgnoreCase))
        {
            _api.ToggleControlSidebar();
            return;
        }
        if (Regex.IsMatch(line, @"app\.toggle_description_sidebar\s*\(\s*\)", RegexOptions.IgnoreCase))
        {
            _api.ToggleDescriptionSidebar();
            return;
        }

        // Simulation playback
        if (Regex.IsMatch(line, @"(?:app|circuit)\.(?:play|start_simulation)\s*\(\s*\)", RegexOptions.IgnoreCase))
        {
            _api.PlaySimulation();
            return;
        }
        if (Regex.IsMatch(line, @"(?:app|circuit)\.(?:pause|pause_simulation)\s*\(\s*\)", RegexOptions.IgnoreCase))
        {
            _api.PauseSimulation();
            return;
        }
        if (Regex.IsMatch(line, @"(?:app|circuit)\.(?:stop|stop_simulation)\s*\(\s*\)", RegexOptions.IgnoreCase))
        {
            _api.StopSimulation();
            return;
        }

        // Breaking current: r1.breaking_current = 0.05
        var breakMatch = Regex.Match(line, @"(\w+)\.breaking_current\s*=\s*([\d\.-]+)", RegexOptions.IgnoreCase);
        if (breakMatch.Success)
        {
            _api.SetBreakingCurrent(breakMatch.Groups[1].Value, double.Parse(breakMatch.Groups[2].Value));
            return;
        }

        // Save / Save As
        var saveAsMatch = Regex.Match(line, @"(?:app|circuit)\.save_as\s*\(\s*""([^""]+)""(?:\s*,\s*""([^""]+)""\s*)?\)", RegexOptions.IgnoreCase);
        if (saveAsMatch.Success)
        {
            string path = saveAsMatch.Groups[1].Value;
            string format = saveAsMatch.Groups[2].Success ? saveAsMatch.Groups[2].Value : "eee";
            _api.SaveProjectAs(path, format);
            return;
        }

        var saveMatch = Regex.Match(line, @"(?:app|circuit)\.save\s*\(\s*(?:""([^""]+)"")?\s*\)", RegexOptions.IgnoreCase);
        if (saveMatch.Success)
        {
            string? path = saveMatch.Groups[1].Success ? saveMatch.Groups[1].Value : null;
            _api.SaveProject(path);
            return;
        }

        // Description: r1.description = "..."
        var descMatch = Regex.Match(line, @"(\w+)\.description\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
        if (descMatch.Success)
        {
            _api.SetDescription(descMatch.Groups[1].Value, descMatch.Groups[2].Value);
            return;
        }

        var customPropMatch = Regex.Match(line, "(?:circuit|app)\\.set_custom_property\\s*\\(\\s*\"([^\"]+)\"\\s*,\\s*\"([^\"]+)\"\\s*,\\s*(.+?)\\s*\\)", RegexOptions.IgnoreCase);
        if (customPropMatch.Success)
        {
            string rawValue = customPropMatch.Groups[3].Value.Trim();
            object value = rawValue.Trim('"', '\'');
            if (bool.TryParse(rawValue, out var boolean)) value = boolean;
            else if (long.TryParse(rawValue, out var integer)) value = integer;
            else if (double.TryParse(rawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var number)) value = number;
            _api.SetCustomProperty(customPropMatch.Groups[1].Value, customPropMatch.Groups[2].Value, value);
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

