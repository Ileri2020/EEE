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

    public object? LastResult { get; private set; }
    public Action<string>? OnLog { get; set; }

    public PythonCommandExecutor(PythonApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public bool ExecuteScript(string script, out string message)
    {
        LastResult = null;
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
                _api.ApplyLogicalGridLayout();
                message = $"Line {lineNum} error: {ex.Message}";
                return false;
            }
        }

        var layout = _api.ApplyLogicalGridLayout();
        if (layout.UnresolvedCollisions.Count > 0)
            message = $"Completed with {layout.UnresolvedCollisions.Count} unresolved component collision(s).";
        return true;
    }

    private void ExecuteLine(string line)
    {
        // Strip leading variable assignment if present: e.g. "vcc = circuit.add_..." or "node_1 = ..."
        int eqIdx = line.IndexOf('=');
        if (eqIdx > 0 && !line[..eqIdx].Contains('(') && !line[..eqIdx].Contains('"') && !line[..eqIdx].Contains('\'') && !line[..eqIdx].Contains('['))
        {
            var varName = line[..eqIdx].Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(varName, @"^[A-Za-z_]\w*$"))
            {
                line = line[(eqIdx + 1)..].Trim();
            }
        }

        var digitalReadMatch = Regex.Match(line,
            @"(?:\w+\s*=\s*)?(?:circuit|eesim)\.read_digital_output_state\s*\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (digitalReadMatch.Success)
        {
            LastResult = _api.ReadDigitalOutputState(digitalReadMatch.Groups[1].Value, digitalReadMatch.Groups[2].Value);
            return;
        }

        var voltageReadMatch = Regex.Match(line,
            @"(?:\w+\s*=\s*)?(?:circuit|eesim)\.read_pin_voltage\s*\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (voltageReadMatch.Success)
        {
            LastResult = _api.ReadPinVoltage(voltageReadMatch.Groups[1].Value, voltageReadMatch.Groups[2].Value);
            return;
        }

        if (Regex.IsMatch(line, @"(?:circuit|eesim)\.evaluate_combinational\s*\(\s*\)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            _api.EvaluateCombinational();
            return;
        }

        var advanceMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.advance_simulation\s*\(\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (advanceMatch.Success)
        {
            _api.AdvanceSimulation(double.Parse(advanceMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture));
            return;
        }

        var logicMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.set_logic\s*\(\s*""([^""]+)""\s*,\s*(true|false)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (logicMatch.Success)
        {
            _api.SetLogic(logicMatch.Groups[1].Value, bool.Parse(logicMatch.Groups[2].Value));
            return;
        }

        var memoryWordMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.set_memory_word\s*\(\s*""([^""]+)""\s*,\s*(\d+)\s*,\s*([-+]?\d+)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (memoryWordMatch.Success)
        {
            _api.SetMemoryWord(
                memoryWordMatch.Groups[1].Value,
                int.Parse(memoryWordMatch.Groups[2].Value, CultureInfo.InvariantCulture),
                long.Parse(memoryWordMatch.Groups[3].Value, CultureInfo.InvariantCulture));
            return;
        }

        var simulateMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.simulate\s*\(\s*(?:""([^""]*)""|'([^']*)')?(?:\s*,\s*stop_time\s*=\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?))?(?:\s*,\s*step_time\s*=\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?))?\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (simulateMatch.Success)
        {
            string analysisType = simulateMatch.Groups[1].Success
                ? simulateMatch.Groups[1].Value
                : simulateMatch.Groups[2].Success
                    ? simulateMatch.Groups[2].Value
                    : "OperatingPoint";
            double? stopTime = simulateMatch.Groups[3].Success
                ? double.Parse(simulateMatch.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture)
                : null;
            double? stepTime = simulateMatch.Groups[4].Success
                ? double.Parse(simulateMatch.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture)
                : null;
            var results = _api.Simulate(analysisType, stopTime, stepTime);
            LastResult = results;
            // Surface failure as a warning message on the output but do not throw —
            // scripts that run simulate() without checking its return value (e.g. test
            // suites that just print PASSED afterwards) must not abort.  The UI layer
            // can inspect LastResult.Success and LastResult.Message to show a warning.
            if (!results.Success)
                OnLog?.Invoke($"[WARN] Simulation failed: {results.Message}");
            return;
        }

        var gridSpacingMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.set_logical_grid_spacing\s*\(\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (gridSpacingMatch.Success)
        {
            double rowSpacing = double.Parse(gridSpacingMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            double columnSpacing = double.Parse(gridSpacingMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            _api.SetLogicalGridSpacing(rowSpacing, columnSpacing);
            return;
        }

        // Example: eesim.set_component_property("R1", "Resistance", 2200)
        var propertyMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.set_component_property\s*\(\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""\s*,\s*(true|false|null|none|""(?:\\.|[^""\\])*""|'[^']*'|[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (propertyMatch.Success)
        {
            string componentName = JsonSerializer.Deserialize<string>($"\"{propertyMatch.Groups[1].Value}\"") ?? string.Empty;
            string propertyName = JsonSerializer.Deserialize<string>($"\"{propertyMatch.Groups[2].Value}\"") ?? string.Empty;
            string rawValue = propertyMatch.Groups[3].Value;
            object? value = rawValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? true
                : rawValue.Equals("false", StringComparison.OrdinalIgnoreCase) ? false
                : rawValue.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                  rawValue.Equals("none", StringComparison.OrdinalIgnoreCase) ? null
                : rawValue.Length >= 2 && rawValue[0] == '"' ? JsonSerializer.Deserialize<string>(rawValue) ?? string.Empty
                : rawValue.Length >= 2 && rawValue[0] == '\'' ? rawValue[1..^1]
                : double.Parse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture);
            _api.SetComponentProperty(componentName, propertyName, value);
            return;
        }

        var gridAddMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.add_component\s*\(\s*""((?:\\.|[^""\\])*)""\s*,\s*name\s*=\s*""((?:\\.|[^""\\])*)""\s*,\s*row\s*=\s*(\d+)\s*,\s*col\s*=\s*(\d+)(?<extra>(?:\s*,\s*\w+\s*=\s*(?:true|false|""(?:\\.|[^""\\])*""|'[^']*'|[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?))*)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (gridAddMatch.Success)
        {
            string type = JsonSerializer.Deserialize<string>($"\"{gridAddMatch.Groups[1].Value}\"") ?? string.Empty;
            string name = JsonSerializer.Deserialize<string>($"\"{gridAddMatch.Groups[2].Value}\"") ?? string.Empty;
            int row = int.Parse(gridAddMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            int column = int.Parse(gridAddMatch.Groups[4].Value, CultureInfo.InvariantCulture);
            string extra = gridAddMatch.Groups["extra"].Value;
            var paramMatch = Regex.Match(extra, @"\bparam\s*=\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            double param = paramMatch.Success
                ? double.Parse(paramMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture)
                : 0.0;
            _api.AddComponentAtGrid(type, name, row, column, param);
            foreach (Match namedPropertyMatch in Regex.Matches(extra,
                         @"(\w+)\s*=\s*(true|false|""[^""]*""|'[^']*'|[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)",
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                string propertyName = namedPropertyMatch.Groups[1].Value;
                if (propertyName.Equals("param", StringComparison.OrdinalIgnoreCase)) continue;
                string rawValue = namedPropertyMatch.Groups[2].Value;
                object value = rawValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? true
                    : rawValue.Equals("false", StringComparison.OrdinalIgnoreCase) ? false
                    : rawValue.Length >= 2 && rawValue[0] == '"' ? JsonSerializer.Deserialize<string>(rawValue) ?? string.Empty
                    : rawValue.Length >= 2 && rawValue[0] == '\'' ? rawValue[1..^1]
                    : double.Parse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture);
                _api.SetComponentProperty(name, propertyName, value);
            }
            return;
        }

        // Example: r1 = circuit.add_component("Resistor", name="R1", position=(100, 200), resistance=1000)
        var positionalAddMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.add_component\s*\(\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)(?:\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?))?\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (positionalAddMatch.Success)
        {
            string type = JsonSerializer.Deserialize<string>($"\"{positionalAddMatch.Groups[1].Value}\"") ?? string.Empty;
            string name = JsonSerializer.Deserialize<string>($"\"{positionalAddMatch.Groups[2].Value}\"") ?? string.Empty;
            double x = double.Parse(positionalAddMatch.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            double y = double.Parse(positionalAddMatch.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            double param = positionalAddMatch.Groups[5].Success
                ? double.Parse(positionalAddMatch.Groups[5].Value, NumberStyles.Float, CultureInfo.InvariantCulture)
                : 0.0;
            _api.AddComponent(type, name, x, y, param);
            return;
        }

        var addMatch = Regex.Match(line,
            @"add_component\s*\(\s*""((?:\\.|[^""\\])*)""\s*,\s*name\s*=\s*""((?:\\.|[^""\\])*)""\s*,\s*position\s*=\s*\(\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*\)(?:\s*,\s*(\w+)\s*=\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?))?\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (addMatch.Success)
        {
            string type = JsonSerializer.Deserialize<string>($"\"{addMatch.Groups[1].Value}\"") ?? string.Empty;
            string name = JsonSerializer.Deserialize<string>($"\"{addMatch.Groups[2].Value}\"") ?? string.Empty;
            double x = double.Parse(addMatch.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            double y = double.Parse(addMatch.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            double param = addMatch.Groups[6].Success
                ? double.Parse(addMatch.Groups[6].Value, NumberStyles.Float, CultureInfo.InvariantCulture)
                : 0.0;
            _api.AddComponent(type, name, x, y, param);
            return;
        }

        // Clear circuit: circuit.clear(), circuit.clear_circuit(), eesim.clear_circuit()
        if (Regex.IsMatch(line, @"^(?:(?:circuit|eesim)\.)?clear(?:_circuit)?\s*\(\s*\)$", RegexOptions.IgnoreCase))
        {
            _api.ClearCircuit();
            return;
        }

        // Print & log statements: print("..."), eesim.print("..."), _log("...")
        var printMatch = Regex.Match(line, @"^(?:(?:circuit|eesim)\.)?(?:print|_log)\s*\(\s*(?:f?[""'](.*)[""']|([^)]*))\s*\)$", RegexOptions.IgnoreCase);
        if (printMatch.Success)
        {
            string msg = printMatch.Groups[1].Success ? printMatch.Groups[1].Value : printMatch.Groups[2].Value;
            Console.WriteLine(msg);
            OnLog?.Invoke(msg);
            return;
        }

        // Helper: _bus_input("prefix", "uut", "A", val, width)
        var busInputMatch = Regex.Match(line,
            @"(?:_bus_input|(?:circuit|eesim)\.set_bus_input)\s*\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*([-+]?(?:0x[\da-fA-F]+|\d+))\s*,\s*(\d+)\s*\)",
            RegexOptions.IgnoreCase);
        if (busInputMatch.Success)
        {
            string prefix = busInputMatch.Groups[1].Value;
            string uut = busInputMatch.Groups[2].Value;
            string pinPrefix = busInputMatch.Groups[3].Value;
            string rawVal = busInputMatch.Groups[4].Value;
            long val = rawVal.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt64(rawVal, 16)
                : Convert.ToInt64(rawVal, CultureInfo.InvariantCulture);
            int width = int.Parse(busInputMatch.Groups[5].Value, CultureInfo.InvariantCulture);
            for (int b = 0; b < width; b++)
            {
                bool bitVal = ((val >> b) & 1) != 0;
                string drv = $"{prefix}_{pinPrefix}{b}_drv";
                _api.AddComponent("logicinput", drv, 10 + b * 20, 10);
                _api.SetLogic(drv, bitVal);
                _api.Connect(drv, "out", uut, $"{pinPrefix}{b}");
            }
            return;
        }

        // Helper: _pin_input("drv", "uut", "pin", bool)
        var pinInputMatch = Regex.Match(line,
            @"(?:_pin_input|(?:circuit|eesim)\.set_pin_input)\s*\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*(true|false)\s*\)",
            RegexOptions.IgnoreCase);
        if (pinInputMatch.Success)
        {
            string drv = pinInputMatch.Groups[1].Value;
            string uut = pinInputMatch.Groups[2].Value;
            string pin = pinInputMatch.Groups[3].Value;
            bool val = bool.Parse(pinInputMatch.Groups[4].Value);
            _api.AddComponent("logicinput", drv, 10, 200);
            _api.SetLogic(drv, val);
            _api.Connect(drv, "out", uut, pin);
            return;
        }

        // Helper: _bus_output("uut", "Y", width)
        var busOutputMatch = Regex.Match(line,
            @"(?:\w+\s*=\s*)?(?:_bus_output|(?:circuit|eesim)\.read_bus_output)\s*\(\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*(\d+)\s*\)",
            RegexOptions.IgnoreCase);
        if (busOutputMatch.Success)
        {
            string uut = busOutputMatch.Groups[1].Value;
            string pinPrefix = busOutputMatch.Groups[2].Value;
            int width = int.Parse(busOutputMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            long res = 0;
            for (int b = 0; b < width; b++)
            {
                if (_api.ReadDigitalOutputState(uut, $"{pinPrefix}{b}"))
                    res |= (1L << b);
            }
            LastResult = res;
            return;
        }

        var namedConnectionMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.connect\s*\(\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""(?:\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?))?(?<extra>[^)]*)\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (namedConnectionMatch.Success)
        {
            string fromComponent = JsonSerializer.Deserialize<string>($"\"{namedConnectionMatch.Groups[1].Value}\"") ?? string.Empty;
            string fromPin = JsonSerializer.Deserialize<string>($"\"{namedConnectionMatch.Groups[2].Value}\"") ?? string.Empty;
            string toComponent = JsonSerializer.Deserialize<string>($"\"{namedConnectionMatch.Groups[3].Value}\"") ?? string.Empty;
            string toPin = JsonSerializer.Deserialize<string>($"\"{namedConnectionMatch.Groups[4].Value}\"") ?? string.Empty;
            double resistance = namedConnectionMatch.Groups[5].Success
                ? double.Parse(namedConnectionMatch.Groups[5].Value, NumberStyles.Float, CultureInfo.InvariantCulture)
                : 0.0;
            var waypoints = ParseWaypoints(line);
            _api.Connect(fromComponent, fromPin, toComponent, toPin, resistance, waypoints);
            return;
        }

        // Example: circuit.connect("v1.positive", "r1.pin1") or eesim.connect("v1.positive", "r1.pin1")
        var dotQuotedConnMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.connect\s*\(\s*""([^""\.]+)\.([^""]+)""\s*,\s*""([^""\.]+)\.([^""]+)""(?:\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?))?(?<extra>[^)]*)\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (dotQuotedConnMatch.Success)
        {
            string fromComp = dotQuotedConnMatch.Groups[1].Value;
            string fromPin = dotQuotedConnMatch.Groups[2].Value;
            string toComp = dotQuotedConnMatch.Groups[3].Value;
            string toPin = dotQuotedConnMatch.Groups[4].Value;
            double resistance = dotQuotedConnMatch.Groups[5].Success
                ? double.Parse(dotQuotedConnMatch.Groups[5].Value, NumberStyles.Float, CultureInfo.InvariantCulture)
                : 0.0;
            var waypoints = ParseWaypoints(line);
            _api.Connect(fromComp, fromPin, toComp, toPin, resistance, waypoints);
            return;
        }

        // Example: circuit.connect(v1.positive, r1.pin1)
        var connMatch = Regex.Match(line, @"(?:circuit|eesim)\.connect\s*\(\s*(\w+)\.(\w+)\s*,\s*(\w+)\.(\w+)\s*(?<extra>[^)]*)\)", RegexOptions.IgnoreCase);
        if (connMatch.Success)
        {
            string fromComp = connMatch.Groups[1].Value;
            string fromPin = connMatch.Groups[2].Value;
            string toComp = connMatch.Groups[3].Value;
            string toPin = connMatch.Groups[4].Value;
            var waypoints = ParseWaypoints(line);
            _api.Connect(fromComp, fromPin, toComp, toPin, 0.0, waypoints);
            return;
        }

        // Macro helpers: add_resistor, add_capacitor, add_inductor, add_ground, add_node, add_wire, add_voltage_source, add_microphone
        var macroMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.(add_resistor|add_capacitor|add_inductor|add_ground|add_node|add_wire|add_voltage_source|add_microphone)\s*\(\s*""((?:\\.|[^""\\])*)""(?<args>[^)]*)\)",
            RegexOptions.IgnoreCase);
        if (macroMatch.Success)
        {
            string macroType = macroMatch.Groups[1].Value.ToLowerInvariant();
            string name = macroMatch.Groups[2].Value;
            string argsStr = macroMatch.Groups["args"].Value;

            var rowMatch = Regex.Match(argsStr, @"\brow\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            var colMatch = Regex.Match(argsStr, @"\bcol\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            bool hasGrid = rowMatch.Success && colMatch.Success;
            int r = hasGrid ? int.Parse(rowMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
            int c = hasGrid ? int.Parse(colMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 1;

            var valMatch = Regex.Match(argsStr, @"^\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)");
            double val = valMatch.Success ? double.Parse(valMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture) : 0;

            switch (macroType)
            {
                case "add_resistor":
                    double res = val > 0 ? val : 1000.0;
                    if (hasGrid) _api.AddResistorAtGrid(name, res, r, c);
                    else _api.AddResistor(name, 0, 0, res);
                    break;
                case "add_capacitor":
                    double cap = val > 0 ? val : 1e-6;
                    if (hasGrid) _api.AddCapacitorAtGrid(name, cap, r, c);
                    else _api.AddCapacitor(name, 0, 0, cap);
                    break;
                case "add_inductor":
                    double ind = val > 0 ? val : 1e-3;
                    if (hasGrid) _api.AddInductorAtGrid(name, ind, r, c);
                    else _api.AddInductor(name, 0, 0, ind);
                    break;
                case "add_ground":
                    if (hasGrid) _api.AddGroundAtGrid(name, r, c);
                    else _api.AddComponent("ground", name, 0, 0, 0);
                    break;
                case "add_node":
                    if (hasGrid) _api.AddNodeAtGrid(name, r, c);
                    else _api.AddNode(name, 0, 0);
                    break;
                case "add_wire":
                    var dirMatch = Regex.Match(argsStr, @"\bdirection\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
                    string dir = dirMatch.Success ? dirMatch.Groups[1].Value : "horizontal";
                    if (hasGrid) _api.AddWireAtGrid(name, dir, r, c);
                    else _api.AddWire(name, 0, 0, dir);
                    break;
                case "add_voltage_source":
                    double dc = val > 0 ? val : 5.0;
                    if (hasGrid) _api.AddComponentAtGrid("voltagesource", name, r, c, dc);
                    else _api.AddComponent("voltagesource", name, 0, 0, dc);
                    break;
                case "add_microphone":
                    double bw = 20000.0;
                    double amp = 1.0;
                    string? audioFile = null;
                    var bwMatch = Regex.Match(argsStr, @"\bbandwidth\s*=\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)", RegexOptions.IgnoreCase);
                    if (bwMatch.Success) bw = double.Parse(bwMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    var ampMatch = Regex.Match(argsStr, @"\bamplitude\s*=\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)", RegexOptions.IgnoreCase);
                    if (ampMatch.Success) amp = double.Parse(ampMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    var fileMatch = Regex.Match(argsStr, @"\baudio_file\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    if (fileMatch.Success) audioFile = fileMatch.Groups[1].Value;

                    if (hasGrid) _api.AddMicrophoneAtGrid(name, audioFile, bw, amp, r, c);
                    else _api.AddMicrophone(name, audioFile, bw, amp);
                    break;
            }
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

        var removeConnectionMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.(?:remove_connection|delete_connection)\s*\(\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (removeConnectionMatch.Success)
        {
            string fromComponent = JsonSerializer.Deserialize<string>($"\"{removeConnectionMatch.Groups[1].Value}\"") ?? string.Empty;
            string fromPin = JsonSerializer.Deserialize<string>($"\"{removeConnectionMatch.Groups[2].Value}\"") ?? string.Empty;
            string toComponent = JsonSerializer.Deserialize<string>($"\"{removeConnectionMatch.Groups[3].Value}\"") ?? string.Empty;
            string toPin = JsonSerializer.Deserialize<string>($"\"{removeConnectionMatch.Groups[4].Value}\"") ?? string.Empty;
            _api.DeleteConnection(fromComponent, fromPin, toComponent, toPin);
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
        var rotMatch = Regex.Match(line,
            @"(?:(?:circuit|eesim)\.)?rotate_component\s*\(\s*""((?:\\.|[^""\\])*)""\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (rotMatch.Success)
        {
            string componentName = JsonSerializer.Deserialize<string>($"\"{rotMatch.Groups[1].Value}\"") ?? string.Empty;
            _api.RotateComponent(componentName, double.Parse(rotMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture));
            return;
        }
        var compRotMatch = Regex.Match(line, @"(\w+)\.rotate\s*\(\s*([\d\.-]+)\s*\)", RegexOptions.IgnoreCase);
        if (compRotMatch.Success)
        {
            _api.RotateComponent(compRotMatch.Groups[1].Value, double.Parse(compRotMatch.Groups[2].Value));
            return;
        }

        // Stretch: r1.stretch(1.5, 2.0) or circuit.stretch_component("R1", 1.5, 2.0)
        var stretchMatch = Regex.Match(line,
            @"(?:(?:circuit|eesim)\.)?stretch_component\s*\(\s*""((?:\\.|[^""\\])*)""\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*,\s*([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (stretchMatch.Success)
        {
            string componentName = JsonSerializer.Deserialize<string>($"\"{stretchMatch.Groups[1].Value}\"") ?? string.Empty;
            _api.StretchComponent(
                componentName,
                double.Parse(stretchMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture),
                double.Parse(stretchMatch.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture));
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
        var namedDescMatch = Regex.Match(line,
            @"(?:circuit|eesim)\.set_description\s*\(\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (namedDescMatch.Success)
        {
            string componentName = JsonSerializer.Deserialize<string>($"\"{namedDescMatch.Groups[1].Value}\"") ?? string.Empty;
            string description = JsonSerializer.Deserialize<string>($"\"{namedDescMatch.Groups[2].Value}\"") ?? string.Empty;
            _api.SetDescription(componentName, description);
            return;
        }

        var descMatch = Regex.Match(line, @"(\w+)\.description\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
        if (descMatch.Success)
        {
            _api.SetDescription(descMatch.Groups[1].Value, descMatch.Groups[2].Value);
            return;
        }

        var customPropMatch = Regex.Match(line,
            @"(?:circuit|eesim|app)\.set_custom_property\s*\(\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""\s*,\s*(true|false|null|none|""(?:\\.|[^""\\])*""|'[^']*'|[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (customPropMatch.Success)
        {
            string rawValue = customPropMatch.Groups[3].Value;
            object value = rawValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? true
                : rawValue.Equals("false", StringComparison.OrdinalIgnoreCase) ? false
                : rawValue.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                  rawValue.Equals("none", StringComparison.OrdinalIgnoreCase) ? string.Empty
                : rawValue.Length >= 2 && rawValue[0] == '"' ? JsonSerializer.Deserialize<string>(rawValue) ?? string.Empty
                : rawValue.Length >= 2 && rawValue[0] == '\'' ? rawValue[1..^1]
                : double.Parse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture);
            string componentName = JsonSerializer.Deserialize<string>($"\"{customPropMatch.Groups[1].Value}\"") ?? string.Empty;
            string propertyName = JsonSerializer.Deserialize<string>($"\"{customPropMatch.Groups[2].Value}\"") ?? string.Empty;
            _api.SetCustomProperty(componentName, propertyName, value);
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

    private List<EESimulator.Core.Models.Point2D>? ParseWaypoints(string line)
    {
        var wpMatch = Regex.Match(line, @"waypoints\s*=\s*\[([^\]]*)\]", RegexOptions.IgnoreCase);
        if (!wpMatch.Success) return null;
        var list = new List<EESimulator.Core.Models.Point2D>();
        var tupleMatches = Regex.Matches(wpMatch.Groups[1].Value, @"\(\s*([-+]?\d+(?:\.\d+)?)\s*,\s*([-+]?\d+(?:\.\d+)?)\s*\)");

        // Check if circuit has logical grid components
        var layout = _api.Circuit.Components.Any(c => c.HasLogicalPlacement)
            ? EESimulator.Core.Models.LogicalGridSchematicLayout.Calculate(_api.Circuit)
            : null;

        foreach (Match tm in tupleMatches)
        {
            double a = double.Parse(tm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            double b = double.Parse(tm.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture);

            // If coordinates are small integers (grid coordinates e.g. row, col or x, y < 60) and layout exists
            if (layout != null && a < 60 && b < 60 && a >= 1 && b >= 1)
            {
                var compAtCell = layout.Placements.FirstOrDefault(p =>
                    p.Component.LogicalRow == (int)Math.Round(a) && p.Component.LogicalColumn == (int)Math.Round(b));

                if (compAtCell.Component != null)
                {
                    list.Add(new EESimulator.Core.Models.Point2D(compAtCell.X, compAtCell.Y));
                    continue;
                }

                list.Add(layout.GetCellCenter((int)Math.Round(a), (int)Math.Round(b)));
                continue;
            }

            list.Add(new EESimulator.Core.Models.Point2D(a, b));
        }
        return list.Count > 0 ? list : null;
    }
}
