using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Storage;
using EESimulator.Commands;
using EESimulator.Commands.Commands;
using EESimulator.Core.Models;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.General;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.UI;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Engine;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using EEE.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EEE.ViewModels;
public partial class EEWorkspaceViewModel
{
    private string? DispatchApiCall(string line)
    {
        // Strip variable assignment if present: e.g. "TL1 = circuit.add_..." or "v1 = eesim.add_..."
        int eqIdx = line.IndexOf('=');
        if (eqIdx > 0 && !line[..eqIdx].Contains('(') && !line[..eqIdx].Contains('"') && !line[..eqIdx].Contains('\''))
        {
            line = line[(eqIdx + 1)..].Trim();
        }

        string call;
        if (line.StartsWith("eesim.", StringComparison.OrdinalIgnoreCase))
            call = line["eesim.".Length..];
        else if (line.StartsWith("circuit.", StringComparison.OrdinalIgnoreCase))
            call = line["circuit.".Length..];
        else
            return $"[unknown] {line}";

        var parenIdx = call.IndexOf('(');
        if (parenIdx < 0) return "[error] malformed call";

        var method = call[..parenIdx].Trim();
        var lastParen = call.LastIndexOf(')');
        var argsStr = lastParen > parenIdx ? call[(parenIdx + 1)..lastParen].Trim() : string.Empty;
        var args = SplitArgs(argsStr);
        var namedArguments = ParseNamedArgs(args);
        bool hasLogicalCoordinates = namedArguments.ContainsKey("row") || namedArguments.ContainsKey("col");
        bool hasNamedPosition = namedArguments.ContainsKey("position_x") || namedArguments.ContainsKey("position_y") ||
            namedArguments.ContainsKey("x") || namedArguments.ContainsKey("y");

        try
        {
            if (method.StartsWith("add_", StringComparison.OrdinalIgnoreCase) &&
                (hasLogicalCoordinates || hasNamedPosition))
                return AddScriptComponentWithCoordinates(method, namedArguments, hasLogicalCoordinates);

            switch (method)
            {
                case "set_workspace_setting" when args.Length >= 2:
                    return SetWorkspaceSettingFromScript(Unquote(args[0]), ParseScriptValue(args[1]));

                case "get_workspace_setting" when args.Length >= 1:
                {
                    var val = GetWorkspaceSettingFromScript(Unquote(args[0]));
                    _lastScriptResult = val;
                    return $"[ok] {val}";
                }

                case "reset_workspace_settings" or "reset_settings_to_defaults" or "reset_to_defaults":
                    return ResetWorkspaceSettingsFromScript();

                case "set_solver_step" when args.Length >= 1:
                    return SetWorkspaceSettingFromScript("solver_step", ParseD(args[0]));

                case "set_simulation_duration" when args.Length >= 1:
                    return SetWorkspaceSettingFromScript("duration", ParseD(args[0]));

                case "set_simulation_start_time" when args.Length >= 1:
                    return SetWorkspaceSettingFromScript("start_time", ParseD(args[0]));

                case "set_auto_run_simulation" when args.Length >= 1:
                    return SetWorkspaceSettingFromScript("auto_run_simulation", ParseB(args[0]));

                case "set_automatic_time_step" or "set_automatic_time_step_selection" when args.Length >= 1:
                    return SetWorkspaceSettingFromScript("automatic_time_step_selection", ParseB(args[0]));

                case "set_subtitle" or "edit_subtitle" or "subtitle" when args.Length >= 1:
                {
                    string sub = Unquote(args[0]);
                    SubtitleText = sub;
                    CircuitChanged?.Invoke();
                    return $"[ok] subtitle set to '{sub}'";
                }

                case "clear_subtitle":
                    SubtitleText = string.Empty;
                    CircuitChanged?.Invoke();
                    return "[ok] subtitle cleared";

                case "get_subtitle":
                    _lastScriptResult = SubtitleText;
                    return $"[ok] {SubtitleText}";

                case "wait" or "sleep" when args.Length >= 1:
                    double waitSec = ParseD(args[0]);
                    return $"[ok] wait {waitSec}s";

                case "new_circuit" when args.Length >= 1:
                    ClearCircuitForScript(resetScript: false);
                    Circuit.Title = Unquote(args[0]);
                    return $"[ok] new circuit '{Circuit.Title}' created";

                case "clear_circuit" or "clear":
                    ClearCircuitForScript(resetScript: false);
                    return "[ok] circuit cleared";

                case "add_component" when args.Length >= 4:
                    PythonApi.AddComponent(Unquote(args[0]), Unquote(args[1]),
                        ParseD(args[2]), ParseD(args[3]),
                        args.Length > 4 ? ParseD(args[4]) : 0);
                    return $"[ok] component {Unquote(args[1])} added";

                case "add_ttl" when args.Length >= 4:
                    PythonApi.AddTtlPackage(Unquote(args[0]), Unquote(args[1]), ParseD(args[2]), ParseD(args[3]));
                    return $"[ok] TTL {Unquote(args[0])} package {Unquote(args[1])} added";

                case "add_resistor" when args.Length >= 3:
                    PythonApi.AddResistor(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 1000.0,
                        args.Length > 4 ? ParseD(args[4]) : 0.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0,
                        args.Length > 6 ? ParseD(args[6]) : 27.0);
                    return $"[ok] resistor {Unquote(args[0])} added";

                case "add_capacitor" when args.Length >= 3:
                    PythonApi.AddCapacitor(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 1e-6,
                        args.Length > 4 ? ParseD(args[4]) : 0.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0,
                        args.Length > 6 ? ParseD(args[6]) : 0.0,
                        args.Length > 7 ? ParseD(args[7]) : 27.0);
                    return $"[ok] capacitor {Unquote(args[0])} added";

                case "add_inductor" when args.Length >= 3:
                    PythonApi.AddInductor(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 1e-3,
                        args.Length > 4 ? ParseD(args[4]) : 0.0,
                        args.Length > 5 ? ParseD(args[5]) : 1.0,
                        args.Length > 6 ? ParseD(args[6]) : 1.0);
                    return $"[ok] inductor {Unquote(args[0])} added";

                case "add_mutual_inductance" when args.Length >= 5:
                    PythonApi.AddMutualInductance(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        Unquote(args[3]), Unquote(args[4]),
                        args.Length > 5 ? ParseD(args[5]) : 0.99);
                    return $"[ok] mutual inductance {Unquote(args[0])} added";

                case "add_voltage_source" when args.Length >= 3:
                    PythonApi.AddVoltageSource(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 5.0,
                        args.Length > 4 ? ParseD(args[4]) : 0.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0,
                        args.Length > 6 ? Unquote(args[6]) : "DC");
                    return $"[ok] voltage source {Unquote(args[0])} added";

                case "add_current_source" when args.Length >= 3:
                    PythonApi.AddCurrentSource(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 0.001,
                        args.Length > 4 ? ParseD(args[4]) : 0.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0,
                        args.Length > 6 ? Unquote(args[6]) : "DC");
                    return $"[ok] current source {Unquote(args[0])} added";

                case "connect":
                {
                    string fromComp, fromPin, toComp, toPin;
                    double resistance = 0.0;
                    if (args.Length >= 4 && !args[0].Contains('.') && !args[2].Contains('.'))
                    {
                        fromComp = Unquote(args[0]);
                        fromPin = Unquote(args[1]);
                        toComp = Unquote(args[2]);
                        toPin = Unquote(args[3]);
                        if (args.Length > 4 && double.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedResistance1))
                            resistance = parsedResistance1;
                    }
                    else if (args.Length >= 2)
                    {
                        var fromParts = Unquote(args[0]).Split('.');
                        var toParts = Unquote(args[1]).Split('.');
                        fromComp = fromParts[0];
                        fromPin = fromParts.Length > 1 ? fromParts[1] : "pin1";
                        toComp = toParts[0];
                        toPin = toParts.Length > 1 ? toParts[1] : "pin1";
                        if (args.Length > 2 && double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedResistance2))
                            resistance = parsedResistance2;
                    }
                    else
                    {
                        return "[error] connect expects component endpoints";
                    }

                    List<EESimulator.Core.Models.Point2D>? waypoints = null;
                    var wpMatch = Regex.Match(line, @"waypoints\s*=\s*\[([^\]]*)\]", RegexOptions.IgnoreCase);
                    if (wpMatch.Success)
                    {
                        var tupleMatches = Regex.Matches(wpMatch.Groups[1].Value, @"\(\s*([-+]?\d+(?:\.\d+)?)\s*,\s*([-+]?\d+(?:\.\d+)?)\s*\)");
                        var layout = Circuit.Components.Any(c => c.HasLogicalPlacement)
                            ? LogicalGridSchematicLayout.Calculate(Circuit)
                            : null;

                        var wpList = new List<EESimulator.Core.Models.Point2D>();
                        foreach (Match tm in tupleMatches)
                        {
                            double a = double.Parse(tm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                            double b = double.Parse(tm.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture);

                            if (layout != null && a < 60 && b < 60 && a >= 1 && b >= 1)
                            {
                                var compAtCell = layout.Placements.FirstOrDefault(p =>
                                    p.Component.LogicalRow == (int)Math.Round(a) && p.Component.LogicalColumn == (int)Math.Round(b));
                                if (compAtCell.Component != null)
                                {
                                    wpList.Add(new EESimulator.Core.Models.Point2D(compAtCell.X, compAtCell.Y));
                                    continue;
                                }
                                wpList.Add(layout.GetCellCenter((int)Math.Round(a), (int)Math.Round(b)));
                                continue;
                            }
                            wpList.Add(new EESimulator.Core.Models.Point2D(a, b));
                        }
                        if (wpList.Count > 0) waypoints = wpList;
                    }

                    PythonApi.Connect(fromComp, fromPin, toComp, toPin, resistance, waypoints);
                    return "[ok] connected";
                }

                case "remove_connection" or "delete_connection" when args.Length >= 4:
                    PythonApi.DeleteConnection(
                        Unquote(args[0]), Unquote(args[1]),
                        Unquote(args[2]), Unquote(args[3]));
                    return "[ok] wire connection removed";

                case "set_position" when args.Length >= 3:
                    PythonApi.SetPosition(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]));
                    return "[ok] position set";

                case "set_property" when args.Length >= 3:
                    PythonApi.SetProperty(Unquote(args[0]), Unquote(args[1]), ParseD(args[2]));
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return "[ok] property set";

                case "set_component_property" when args.Length >= 3:
                    PythonApi.SetComponentProperty(Unquote(args[0]), Unquote(args[1]), ParseScriptValue(args[2]));
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return "[ok] component property set";

                case "set_custom_property" when args.Length >= 3:
                    PythonApi.SetCustomProperty(Unquote(args[0]), Unquote(args[1]), ParseScriptValue(args[2]));
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return "[ok] custom property set";

                case "set_memory_word" when args.Length >= 3:
                    PythonApi.SetMemoryWord(Unquote(args[0]), (int)ParseD(args[1]), (long)ParseD(args[2]));
                    CircuitChanged?.Invoke();
                    return "[ok] memory word set";

                case "set_live_probe" or "set_probe" when args.Length >= 2:
                    PythonApi.SetLiveProbe(Unquote(args[0]), bool.TryParse(Unquote(args[1]), out var lp) && lp);
                    CircuitChanged?.Invoke();
                    return $"[ok] live probe on {Unquote(args[0])} set to {args[1]}";

                case "set_waveform" when args.Length >= 2:
                    SetWaveformTarget(Unquote(args[0]), bool.TryParse(Unquote(args[1]), out var sw) && sw);
                    CircuitChanged?.Invoke();
                    return $"[ok] waveform for {Unquote(args[0])} set to {args[1]}";

                case "toggle_waveform" when args.Length >= 1:
                    string waveformTarget = Unquote(args[0]);
                    var waveformComponent = FindComponentTarget(waveformTarget);
                    if (waveformComponent == null) return $"[error] component or node '{waveformTarget}' not found";
                    SetWaveformTarget(waveformTarget, !waveformComponent.ShowWaveform);
                    CircuitChanged?.Invoke();
                    return $"[ok] waveform for {waveformTarget} toggled";

                case "rename_pin" when args.Length >= 3:
                    PythonApi.RenamePin(Unquote(args[0]), Unquote(args[1]), Unquote(args[2]));
                    CircuitChanged?.Invoke();
                    return $"[ok] pin {Unquote(args[1])} on {Unquote(args[0])} renamed to {Unquote(args[2])}";

                case "set_wire_resistance" when args.Length >= 2:
                    PythonApi.SetWireResistance(Unquote(args[0]), ParseD(args[1]));
                    CircuitChanged?.Invoke();
                    return $"[ok] wire resistance on {Unquote(args[0])} set to {args[1]} Ω";

                case "register_control_button" when args.Length >= 2:
                    PythonApi.RegisterControlButton(Unquote(args[0]), Unquote(args[1]));
                    return $"[ok] registered control button [{Unquote(args[0])}] -> {Unquote(args[1])}";

                case "set_active_tool" when args.Length >= 1:
                    PythonApi.SetActiveTool(Unquote(args[0]));
                    return $"[ok] active tool set to {Unquote(args[0])}";

                case "simulate":
                    var r = PythonApi.Simulate(args.Length > 0 ? Unquote(args[0]) : "OperatingPoint");
                    DispatchTriggeredUiEvents();
                    return FormatResults(r);

                case "fire_event" or "ui.fire_event" when args.Length >= 1:
                    string firedEventName = Unquote(args[0]);
                    string? firedEventMessage = args.Length > 1 ? Unquote(args[1]) : null;
                    FireNamedUiEvent(firedEventName, firedEventMessage);
                    return $"[ok] UI event '{firedEventName}' fired";

                case "toggle_event" or "ui.toggle_event" when args.Length >= 1:
                    ToggleNamedUiEvent(Unquote(args[0]));
                    return $"[ok] UI event '{Unquote(args[0])}' toggled";

                // UI Hierarchy Operations (RootNode script interface)
                case "ui.open_dialog" or "ui.dialog.open":
                    var targetComp = EditingComponent ?? Circuit.FindChildren<ComponentNode>().FirstOrDefault();
                    if (targetComp != null)
                        OpenEditDialog(targetComp);
                    return targetComp != null ? "[ok] edit dialog opened" : "[error] no component available to edit";

                case "ui.close_dialog" or "ui.dialog.close":
                    CloseEditDialog();
                    return "[ok] edit dialog closed";

                case "ui.toggle_waveform" or "ui.waveform.toggle":
                    ToggleWaveformDrawer();
                    return $"[ok] waveform drawer is now {(IsWaveformDrawerOpen ? "open" : "closed")}";

                case "ui.set_viewport" when args.Length >= 1:
                    PythonApi.SetActiveViewport(Unquote(args[0]));
                    return $"[ok] viewport changed to {Unquote(args[0])}";

                case "ui.get" when args.Length >= 1:
                    var node = UI.Get(Unquote(args[0]));
                    return node != null ? $"[ok] UI node '{node.Name}' ({node.NodeType}), Visible={node.Visible}" : $"[not found] UI node {Unquote(args[0])}";

                case "ui.click" when args.Length >= 1:
                    var btn = UI.Button(Unquote(args[0]));
                    if (btn != null)
                    {
                        btn.Click();
                        return $"[ok] clicked button '{btn.Name}'";
                    }
                    return $"[not found] button {Unquote(args[0])}";

                case "add_bulb" when args.Length >= 3:
                    PythonApi.AddBulb(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 50.0,
                        args.Length > 4 ? ParseD(args[4]) : 6.0);
                    return $"[ok] bulb {Unquote(args[0])} added";

                case "add_led" when args.Length >= 3:
                    PythonApi.AddLED(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? Unquote(args[3]) : "Red",
                        args.Length > 4 ? ParseD(args[4]) : 2.0);
                    return $"[ok] led {Unquote(args[0])} added";

                case "add_logic_input" when args.Length >= 3:
                    PythonApi.AddLogicInput(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 && (bool.TryParse(Unquote(args[3]), out var bIni) ? bIni : ParseD(args[3]) > 0.5),
                        args.Length > 4 ? ParseD(args[4]) : 5.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0);
                    return $"[ok] logic input {Unquote(args[0])} added";

                case "add_logic_output" when args.Length >= 3:
                    PythonApi.AddLogicOutput(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 2.5);
                    return $"[ok] logic output {Unquote(args[0])} added";

                case "add_clock" when args.Length >= 3:
                    PythonApi.AddClock(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 1000.0,
                        args.Length > 4 ? ParseD(args[4]) : 0.5,
                        args.Length > 5 ? ParseD(args[5]) : 5.0,
                        args.Length > 6 ? ParseD(args[6]) : 0.0);
                    return $"[ok] clock {Unquote(args[0])} added";

                case "add_pin" when args.Length >= 3:
                    PythonApi.AddPin(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? Unquote(args[3]) : "Input",
                        args.Length > 4 ? (int)ParseD(args[4]) : 1,
                        args.Length > 5 ? (long)ParseD(args[5]) : 0L);
                    return $"[ok] pin {Unquote(args[0])} added";

                case "add_power" when args.Length >= 3:
                    PythonApi.AddPower(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 5.0);
                    return $"[ok] power rail {Unquote(args[0])} added";

                case "add_pull_resistor" or "add_pullresistor" when args.Length >= 3:
                    PythonApi.AddPullResistor(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? Unquote(args[3]) : "PullUp",
                        args.Length > 4 ? ParseD(args[4]) : 10000.0,
                        args.Length > 5 ? ParseD(args[5]) : 5.0);
                    return $"[ok] pull resistor {Unquote(args[0])} added";

                case "add_tunnel" when args.Length >= 3:
                    PythonApi.AddTunnel(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? Unquote(args[3]) : "NET");
                    return $"[ok] tunnel {Unquote(args[0])} added";

                case "add_probe" when args.Length >= 3:
                    PythonApi.AddProbe(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 10e6);
                    return $"[ok] probe {Unquote(args[0])} added";

                case "add_digital_transistor" or "add_transistor" when args.Length >= 3:
                    PythonApi.AddDigitalTransistor(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? Unquote(args[3]) : "N-Type",
                        args.Length > 4 ? ParseD(args[4]) : 0.1,
                        args.Length > 5 ? ParseD(args[5]) : 10e6,
                        args.Length > 6 ? ParseD(args[6]) : 2.5);
                    return $"[ok] digital transistor {Unquote(args[0])} added";

                case "add_transmission_gate" or "add_transmissiongate" when args.Length >= 3:
                    PythonApi.AddTransmissionGate(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 0.2,
                        args.Length > 4 ? ParseD(args[4]) : 10e6);
                    return $"[ok] transmission gate {Unquote(args[0])} added";

                case "add_bit_extender" or "add_bitextender" when args.Length >= 3:
                    PythonApi.AddBitExtender(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? (int)ParseD(args[3]) : 1,
                        args.Length > 4 ? (int)ParseD(args[4]) : 8,
                        args.Length > 5 ? Unquote(args[5]) : "Zero");
                    return $"[ok] bit extender {Unquote(args[0])} added";

                case "add_constant" when args.Length >= 3:
                    PythonApi.AddConstant(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? (long)ParseD(args[3]) : 1L,
                        args.Length > 4 ? ParseD(args[4]) : 5.0,
                        args.Length > 5 ? ParseD(args[5]) : 0.0);
                    return $"[ok] constant {Unquote(args[0])} added";

                case "add_dnc" or "add_do_not_connect" when args.Length >= 3:
                    PythonApi.AddDoNotConnect(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]));
                    return $"[ok] DNC marker {Unquote(args[0])} added";

                case "add_node" when args.Length >= 1:
                    if (args.Length >= 3)
                        PythonApi.AddNode(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]));
                    else
                        PythonApi.AddNode(Unquote(args[0]), 0, 0);
                    return $"[ok] node {Unquote(args[0])} added";

                case "add_wire" when args.Length >= 1:
                    if (args.Length >= 3)
                        PythonApi.AddWire(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]), args.Length > 3 ? Unquote(args[3]) : "horizontal");
                    else
                        PythonApi.AddWire(Unquote(args[0]), 0, 0, args.Length > 1 ? Unquote(args[1]) : "horizontal");
                    return $"[ok] wire {Unquote(args[0])} added";

                case "add_lossy_transmission_line" or "add_lossy_transmissionline" when args.Length >= 3:
                    PythonApi.AddLossyTransmissionLine(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 0.2,
                        args.Length > 4 ? ParseD(args[4]) : 250e-9,
                        args.Length > 5 ? ParseD(args[5]) : 10e-6,
                        args.Length > 6 ? ParseD(args[6]) : 100e-12,
                        args.Length > 7 ? ParseD(args[7]) : 1.0);
                    return $"[ok] lossy transmission line {Unquote(args[0])} added";

                case "add_coaxial_cable" or "add_coax" when args.Length >= 3:
                    PythonApi.AddCoaxialCable(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 50.0,
                        args.Length > 4 ? ParseD(args[4]) : 0.66,
                        args.Length > 5 ? ParseD(args[5]) : 1.0,
                        args.Length > 6 ? ParseD(args[6]) : 0.05);
                    return $"[ok] coaxial cable {Unquote(args[0])} added";

                case "add_microstrip_line" or "add_microstrip" when args.Length >= 3:
                    PythonApi.AddMicrostripLine(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]),
                        args.Length > 3 ? ParseD(args[3]) : 1.5e-3,
                        args.Length > 4 ? ParseD(args[4]) : 0.8e-3,
                        args.Length > 5 ? ParseD(args[5]) : 4.4,
                        args.Length > 6 ? ParseD(args[6]) : 0.05);
                    return $"[ok] microstrip line {Unquote(args[0])} added";

                case "toggle_logic" when args.Length >= 1:
                    PythonApi.ToggleLogic(Unquote(args[0]));
                    return $"[ok] logic component {Unquote(args[0])} toggled";

                case "set_logic" when args.Length >= 2:
                    bool lState = bool.TryParse(Unquote(args[1]), out var bS) ? bS : ParseD(args[1]) > 0.5;
                    PythonApi.SetLogic(Unquote(args[0]), lState);
                    return $"[ok] logic component {Unquote(args[0])} set to {lState}";

                case "press_button" or "set_button" when args.Length >= 2:
                    PythonApi.PressButton(Unquote(args[0]), ParseB(args[1]));
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] button {Unquote(args[0])} set to {ParseB(args[1])}";

                case "set_dip_switch" when args.Length >= 2:
                    PythonApi.SetDipSwitch(Unquote(args[0]), ParseScriptLong(args[1]));
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] DIP switch {Unquote(args[0])} set";

                case "set_dip_switch_pole" when args.Length >= 3:
                    PythonApi.SetDipSwitchPole(Unquote(args[0]), checked((int)ParseScriptLong(args[1])), ParseB(args[2]));
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] DIP switch {Unquote(args[0])} pole {args[1]} set to {args[2]}";

                case "set_logic_bus" or "set_binary_inputs" when args.Length >= 2:
                {
                    string rawNames = Unquote(args[0]);
                    var names = rawNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    long busVal = ParseScriptLong(args[1]);
                    PythonApi.SetLogicBus(names, busVal);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] logic bus [{rawNames}] set to {busVal}";
                }

                case "set_constant" when args.Length >= 2:
                {
                    var n = ParseNamedArgs(args);
                    long cVal = ParseScriptLong(args[1]);
                    int? bW = n.TryGetValue("bit_width", out var sbw) ? (int?)ParseD(sbw) : (args.Length > 2 ? (int?)ParseD(args[2]) : null);
                    double? vH = n.TryGetValue("high_voltage", out var svh) ? (double?)ParseD(svh) : (args.Length > 3 ? (double?)ParseD(args[3]) : null);
                    double? vL = n.TryGetValue("low_voltage", out var svl) ? (double?)ParseD(svl) : (args.Length > 4 ? (double?)ParseD(args[4]) : null);
                    PythonApi.SetConstant(Unquote(args[0]), cVal, bW, vH, vL);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] constant {Unquote(args[0])} set to {cVal}";
                }

                case "set_voltage_switch" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    double? th = n.TryGetValue("threshold", out var st) ? (double?)ParseD(st) : (args.Length > 1 ? (double?)ParseD(args[1]) : null);
                    double? hy = n.TryGetValue("hysteresis", out var sh) ? (double?)ParseD(sh) : (args.Length > 2 ? (double?)ParseD(args[2]) : null);
                    double? ron = n.TryGetValue("ron", out var sr) ? (double?)ParseD(sr) : (args.Length > 3 ? (double?)ParseD(args[3]) : null);
                    double? roff = n.TryGetValue("roff", out var srf) ? (double?)ParseD(srf) : (args.Length > 4 ? (double?)ParseD(args[4]) : null);
                    PythonApi.SetVoltageSwitch(Unquote(args[0]), th, hy, ron, roff);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] voltage switch {Unquote(args[0])} configured";
                }

                case "set_current_switch" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    double? th = n.TryGetValue("threshold", out var st) ? (double?)ParseD(st) : (args.Length > 1 ? (double?)ParseD(args[1]) : null);
                    double? hy = n.TryGetValue("hysteresis", out var sh) ? (double?)ParseD(sh) : (args.Length > 2 ? (double?)ParseD(args[2]) : null);
                    double? ron = n.TryGetValue("ron", out var sr) ? (double?)ParseD(sr) : (args.Length > 3 ? (double?)ParseD(args[3]) : null);
                    double? roff = n.TryGetValue("roff", out var srf) ? (double?)ParseD(srf) : (args.Length > 4 ? (double?)ParseD(args[4]) : null);
                    string? cs = n.TryGetValue("controlling_source", out var sc) ? Unquote(sc) : (args.Length > 5 ? Unquote(args[5]) : null);
                    PythonApi.SetCurrentSwitch(Unquote(args[0]), th, hy, ron, roff, cs);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] current switch {Unquote(args[0])} configured";
                }

                case "set_relay" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    double? vpi = n.TryGetValue("pull_in_voltage", out var sp) ? (double?)ParseD(sp) : (args.Length > 1 ? (double?)ParseD(args[1]) : null);
                    double? vdo = n.TryGetValue("dropout_voltage", out var sd) ? (double?)ParseD(sd) : (args.Length > 2 ? (double?)ParseD(args[2]) : null);
                    double? cr = n.TryGetValue("coil_resistance", out var scr) ? (double?)ParseD(scr) : (args.Length > 3 ? (double?)ParseD(args[3]) : null);
                    double? conr = n.TryGetValue("contact_resistance", out var scn) ? (double?)ParseD(scn) : (args.Length > 4 ? (double?)ParseD(args[4]) : null);
                    double? or_ = n.TryGetValue("open_resistance", out var sor) ? (double?)ParseD(sor) : (args.Length > 5 ? (double?)ParseD(args[5]) : null);
                    PythonApi.SetRelay(Unquote(args[0]), vpi, vdo, cr, conr, or_);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] relay {Unquote(args[0])} configured";
                }

                case "set_microphone_audio" or "set_microphone" when args.Length >= 2:
                {
                    var n = ParseNamedArgs(args);
                    string filePath = Unquote(args[1]);
                    double? amp = n.TryGetValue("amplitude", out var sa) ? (double?)ParseD(sa) : (args.Length > 2 ? (double?)ParseD(args[2]) : null);
                    double? bw = n.TryGetValue("bandwidth", out var sb) ? (double?)ParseD(sb) : (args.Length > 3 ? (double?)ParseD(args[3]) : null);
                    double? off = n.TryGetValue("offset", out var so) ? (double?)ParseD(so) : (args.Length > 4 ? (double?)ParseD(args[4]) : null);
                    PythonApi.SetMicrophoneAudio(Unquote(args[0]), filePath, amp, bw, off);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] microphone {Unquote(args[0])} audio configured";
                }

                case "set_dc_signal" when args.Length >= 2:
                    PythonApi.SetDcSignal(Unquote(args[0]), ParseD(args[1]));
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] DC signal on {Unquote(args[0])} set to {args[1]}V";

                case "set_sine_signal" when args.Length >= 3:
                {
                    var n = ParseNamedArgs(args);
                    double amp = ParseD(args[1]);
                    double freq = ParseD(args[2]);
                    double off = n.TryGetValue("offset", out var so) ? ParseD(so) : (args.Length > 3 ? ParseD(args[3]) : 0.0);
                    double ph = n.TryGetValue("phase", out var sp) ? ParseD(sp) : (args.Length > 4 ? ParseD(args[4]) : 0.0);
                    double dl = n.TryGetValue("delay", out var sd) ? ParseD(sd) : (args.Length > 5 ? ParseD(args[5]) : 0.0);
                    double th = n.TryGetValue("theta", out var st) ? ParseD(st) : (args.Length > 6 ? ParseD(args[6]) : 0.0);
                    PythonApi.SetSineSignal(Unquote(args[0]), amp, freq, off, ph, dl, th);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] sine signal on {Unquote(args[0])} set to {amp}V @ {freq}Hz";
                }

                case "set_pulse_signal" when args.Length >= 5:
                {
                    var n = ParseNamedArgs(args);
                    double vLow = ParseD(args[1]);
                    double vHigh = ParseD(args[2]);
                    double per = ParseD(args[3]);
                    double wid = ParseD(args[4]);
                    double dl = n.TryGetValue("delay", out var sd) ? ParseD(sd) : (args.Length > 5 ? ParseD(args[5]) : 0.0);
                    double tr = n.TryGetValue("rise_time", out var sr) ? ParseD(sr) : (args.Length > 6 ? ParseD(args[6]) : 1e-6);
                    double tf = n.TryGetValue("fall_time", out var sf) ? ParseD(sf) : (args.Length > 7 ? ParseD(args[7]) : 1e-6);
                    PythonApi.SetPulseSignal(Unquote(args[0]), vLow, vHigh, per, wid, dl, tr, tf);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] pulse signal on {Unquote(args[0])} set: [{vLow}V -> {vHigh}V, T={per}s]";
                }

                case "set_step_signal" when args.Length >= 3:
                {
                    double vInit = ParseD(args[1]);
                    double vStep = ParseD(args[2]);
                    double dl = args.Length > 3 ? ParseD(args[3]) : 0.0;
                    PythonApi.SetStepSignal(Unquote(args[0]), vInit, vStep, dl);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] step signal on {Unquote(args[0])} set: {vInit}V -> {vStep}V at t={dl}s";
                }

                case "set_ramp_signal" when args.Length >= 4:
                {
                    double vStart = ParseD(args[1]);
                    double vPeak = ParseD(args[2]);
                    double dur = ParseD(args[3]);
                    double dl = args.Length > 4 ? ParseD(args[4]) : 0.0;
                    double? per = args.Length > 5 ? (double?)ParseD(args[5]) : null;
                    PythonApi.SetRampSignal(Unquote(args[0]), vStart, vPeak, dur, dl, per);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] ramp signal on {Unquote(args[0])} set: {vStart}V -> {vPeak}V over {dur}s";
                }

                case "set_triangle_signal" when args.Length >= 4:
                {
                    double vLow = ParseD(args[1]);
                    double vHigh = ParseD(args[2]);
                    double per = ParseD(args[3]);
                    double dl = args.Length > 4 ? ParseD(args[4]) : 0.0;
                    PythonApi.SetTriangleSignal(Unquote(args[0]), vLow, vHigh, per, dl);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] triangle signal on {Unquote(args[0])} set: [{vLow}V <-> {vHigh}V, T={per}s]";
                }

                case "set_pwl_signal" when args.Length >= 2:
                {
                    string pwl = Unquote(args[1]);
                    PythonApi.SetPwlSignal(Unquote(args[0]), pwl);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] PWL signal on {Unquote(args[0])} configured";
                }

                case "set_ac_source" when args.Length >= 3:
                {
                    var n = ParseNamedArgs(args);
                    double vRms = ParseD(args[1]);
                    double freq = ParseD(args[2]);
                    string wf = n.TryGetValue("waveform", out var swf) ? Unquote(swf) : (args.Length > 3 ? Unquote(args[3]) : "Sine");
                    double off = n.TryGetValue("offset", out var so) ? ParseD(so) : (args.Length > 4 ? ParseD(args[4]) : 0.0);
                    double ph = n.TryGetValue("phase", out var sp) ? ParseD(sp) : (args.Length > 5 ? ParseD(args[5]) : 0.0);
                    PythonApi.SetAcSource(Unquote(args[0]), vRms, freq, wf, off, ph);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] AC source {Unquote(args[0])} set to {vRms}V RMS @ {freq}Hz ({wf})";
                }

                case "set_clock_signal" when args.Length >= 2:
                {
                    var n = ParseNamedArgs(args);
                    double freq = ParseD(args[1]);
                    double duty = n.TryGetValue("duty_cycle", out var sd) ? ParseD(sd) : (args.Length > 2 ? ParseD(args[2]) : 0.5);
                    double vH = n.TryGetValue("high_voltage", out var svh) ? ParseD(svh) : (args.Length > 3 ? ParseD(args[3]) : 5.0);
                    double vL = n.TryGetValue("low_voltage", out var svl) ? ParseD(svl) : (args.Length > 4 ? ParseD(args[4]) : 0.0);
                    PythonApi.SetClockSignal(Unquote(args[0]), freq, duty, vH, vL);
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] clock signal on {Unquote(args[0])} set to {freq}Hz (duty {duty:P0})";
                }

                case "set_joystick" when args.Length >= 3:
                    PythonApi.SetJoystick(
                        Unquote(args[0]),
                        checked((int)ParseScriptLong(args[1])),
                        checked((int)ParseScriptLong(args[2])),
                        args.Length > 3 && ParseB(args[3]));
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] joystick {Unquote(args[0])} set";

                case "send_keyboard_key" when args.Length >= 2:
                    PythonApi.SendKeyboardKey(Unquote(args[0]), checked((int)ParseScriptLong(args[1])));
                    CircuitChanged?.Invoke();
                    RequestSimulationRefresh();
                    return $"[ok] keyboard input sent to {Unquote(args[0])}";

                case "schedule_input" when args.Length >= 4:
                    return ScheduleInputFromScript(
                        ParseFiniteScriptNumber(args[0], "delay_seconds"),
                        Unquote(args[1]),
                        Unquote(args[2]),
                        ParseScriptValue(args[3]));

                case "clear_scheduled_inputs":
                    ClearScheduledInputs();
                    return "[ok] scheduled inputs cleared";

                case "set_logic_levels" when args.Length >= 3:
                    PythonApi.SetLogicLevels(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]));
                    return $"[ok] logic levels on {Unquote(args[0])} set to High={args[1]}V, Low={args[2]}V";

                case "set_output_state" when args.Length >= 2:
                    bool oState = bool.TryParse(Unquote(args[1]), out var bO) ? bO : ParseD(args[1]) > 0.5;
                    PythonApi.SetOutputState(Unquote(args[0]), oState);
                    CircuitChanged?.Invoke();
                    return $"[ok] output component {Unquote(args[0])} state set to {oState}";

                case "rotate_component" or "rotate" when args.Length >= 2:
                    PythonApi.RotateComponent(Unquote(args[0]), ParseD(args[1]));
                    return $"[ok] component {Unquote(args[0])} rotated by {args[1]}°";

                case "stretch_component" or "stretch" or "set_scale" or "resize" when args.Length >= 3:
                    PythonApi.StretchComponent(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]));
                    return $"[ok] component {Unquote(args[0])} stretched to scale ({args[1]}, {args[2]})";

                case "play" or "play_simulation" or "simulation_play" or "start_simulation" or "start":
                    PythonApi.PlaySimulation();
                    return "[ok] simulation playback started";

                case "pause" or "pause_simulation" or "simulation_pause":
                    PythonApi.PauseSimulation();
                    return "[ok] simulation playback paused";

                case "play_pause" or "simulation_toggle" or "toggle_simulation":
                    PythonApi.ToggleSimulationPlayback();
                    return "[ok] simulation playback toggled";

                case "stop" or "stop_simulation" or "simulation_stop":
                    PythonApi.StopSimulation();
                    return "[ok] simulation playback stopped";

                case "step" or "step_simulation":
                    StepSimulationOnce();
                    return "[ok] simulation stepped";

                case "close_sidebars" or "hide_sidebars":
                    PythonApi.CloseSidebars();
                    return "[ok] all sidebars closed for maximum viewport";

                case "open_sidebars" or "show_sidebars":
                    PythonApi.OpenSidebars();
                    return "[ok] sidebars opened";

                case "toggle_sidebar" or "toggle_component_sidebar" or "toggle_right_sidebar":
                    bool? reqVis = args.Length > 0 ? (bool.TryParse(Unquote(args[0]), out var vb) ? vb : null) : null;
                    PythonApi.ToggleComponentSidebar(reqVis);
                    return $"[ok] right sidebar is now {(IsSidebarVisible ? "visible" : "hidden")}";

                case "toggle_left_toolbar" or "toggle_toolbar" or "toggle_left_sidebar":
                    bool? reqLeftVis = args.Length > 0 ? (bool.TryParse(Unquote(args[0]), out var lvb) ? lvb : null) : null;
                    PythonApi.ToggleLeftToolbar(reqLeftVis);
                    return $"[ok] left sidebar is now {(IsLeftToolbarVisible ? "visible" : "hidden")}";

                case "open_left_sidebar" or "show_left_sidebar":
                    PythonApi.SetLeftSidebarVisible(true);
                    return "[ok] left sidebar opened";

                case "close_left_sidebar" or "hide_left_sidebar":
                    PythonApi.SetLeftSidebarVisible(false);
                    return "[ok] left sidebar closed";

                case "set_left_sidebar" when args.Length >= 1:
                    bool leftVis = ParseB(args[0]);
                    PythonApi.SetLeftSidebarVisible(leftVis);
                    return $"[ok] left sidebar is now {(leftVis ? "visible" : "hidden")}";

                case "open_right_sidebar" or "show_right_sidebar":
                    PythonApi.SetRightSidebarVisible(true);
                    return "[ok] right sidebar opened";

                case "close_right_sidebar" or "hide_right_sidebar":
                    PythonApi.SetRightSidebarVisible(false);
                    return "[ok] right sidebar closed";

                case "set_right_sidebar" when args.Length >= 1:
                    bool rightVis = ParseB(args[0]);
                    PythonApi.SetRightSidebarVisible(rightVis);
                    return $"[ok] right sidebar is now {(rightVis ? "visible" : "hidden")}";

                case "toggle_control_sidebar":
                    bool? reqCtrlVis = args.Length > 0 ? (bool.TryParse(Unquote(args[0]), out var cvb) ? cvb : null) : null;
                    PythonApi.ToggleControlSidebar(reqCtrlVis);
                    return $"[ok] control sidebar is now {(IsControlSidebarVisible ? "visible" : "hidden")}";

                case "set_sidebar" when args.Length >= 2:
                    bool sVis = bool.TryParse(Unquote(args[1]), out var sbv) && sbv;
                    PythonApi.SetSidebarVisible(Unquote(args[0]), sVis);
                    return $"[ok] sidebar '{Unquote(args[0])}' set to {sVis}";

                case "open_graph_drawer" or "open_waveform_drawer":
                    PythonApi.OpenWaveformDrawer();
                    return "[ok] graph drawer opened";

                case "close_graph_drawer" or "close_waveform_drawer":
                    PythonApi.CloseWaveformDrawer();
                    return "[ok] graph drawer closed";

                case "toggle_graph_drawer" or "toggle_waveform_drawer":
                    if (args.Length > 0 && bool.TryParse(Unquote(args[0]), out var tgv))
                        PythonApi.SetWaveformDrawerVisible(tgv);
                    else
                        PythonApi.ToggleWaveformDrawer();
                    return $"[ok] graph drawer is now {(IsWaveformDrawerOpen ? "open" : "closed")}";

                case "set_graph_drawer" or "set_waveform_drawer" when args.Length >= 1:
                    bool gVis = ParseB(args[0]);
                    PythonApi.SetWaveformDrawerVisible(gVis);
                    return $"[ok] graph drawer is now {(gVis ? "open" : "closed")}";

                case "undo":
                    PythonApi.Undo();
                    return "[ok] undo performed";

                case "redo":
                    PythonApi.Redo();
                    return "[ok] redo performed";

                case "copy_component" or "copy" when args.Length >= 1:
                    PythonApi.CopyComponent(Unquote(args[0]));
                    return $"[ok] copied component '{Unquote(args[0])}'";

                case "delete_component" or "remove_component" or "delete" or "remove" when args.Length >= 1:
                    PythonApi.DeleteComponent(Unquote(args[0]));
                    return $"[ok] deleted component '{Unquote(args[0])}'";

                case "set_breaking_current" when args.Length >= 2:
                    PythonApi.SetBreakingCurrent(Unquote(args[0]), ParseD(args[1]));
                    CircuitChanged?.Invoke();
                    return $"[ok] breaking current for '{Unquote(args[0])}' set to {args[1]}A";

                case "set_description" when args.Length >= 2:
                    PythonApi.SetDescription(Unquote(args[0]), Unquote(args[1]));
                    CircuitChanged?.Invoke();
                    return $"[ok] description for '{Unquote(args[0])}' updated";

                case "toggle_description_sidebar":
                    bool? reqDescVis = args.Length > 0 ? (bool.TryParse(Unquote(args[0]), out var dvb) ? dvb : null) : null;
                    if (reqDescVis.HasValue)
                        IsDescriptionSidebarVisible = reqDescVis.Value;
                    else
                        ToggleDescriptionSidebar();
                    return $"[ok] description sidebar is now {(IsDescriptionSidebarVisible ? "visible" : "hidden")}";

                case "set_project_name" when args.Length >= 1:
                    PythonApi.SetProjectName(Unquote(args[0]));
                    return $"[ok] project name set to '{Unquote(args[0])}'";

                case "save" or "save_project":
                    string? savePath = args.Length > 0 ? Unquote(args[0]) : null;
                    if (string.IsNullOrEmpty(savePath)) SaveProject();
                    else { SaveToPath(savePath); CurrentFilePath = savePath; ProjectName = System.IO.Path.GetFileName(savePath); }
                    return $"[ok] saved project to {ProjectName}";

                case "save_as" or "save_project_as" when args.Length >= 1:
                    string asPath = Unquote(args[0]);
                    string asFmt = args.Length >= 2 ? Unquote(args[1]) : "eee";
                    SelectedSaveFormat = asFmt;
                    SaveToPath(asPath);
                    CurrentFilePath = asPath;
                    ProjectName = System.IO.Path.GetFileName(asPath);
                    return $"[ok] saved project as {ProjectName} ({asFmt})";

                case "save_circuit_image":
                {
                    string? imgPath = args.Length > 0 ? Unquote(args[0]) : null;
                    string imgView = namedArguments.TryGetValue("view", out var iv) ? Unquote(iv) : "recentre";
                    PythonApi.SaveCircuitImage(imgPath, imgView);
                    return $"[ok] circuit image export requested ({imgView} view)";
                }

                case "start_video_recording":
                {
                    string? vidPath = args.Length > 0 && !string.IsNullOrWhiteSpace(Unquote(args[0])) && !string.Equals(Unquote(args[0]), "null", StringComparison.OrdinalIgnoreCase) && !string.Equals(Unquote(args[0]), "none", StringComparison.OrdinalIgnoreCase) ? Unquote(args[0]) : null;
                    int vidFps = args.Length > 1 ? checked((int)ParseD(args[1])) : 10;
                    string vidFmt = args.Length > 2 ? Unquote(args[2]) : "mp4";
                    string vidView = namedArguments.TryGetValue("view", out var vv) ? Unquote(vv) : "recentre";
                    double? vidDuration = namedArguments.TryGetValue("duration", out var vd) ? (double?)ParseD(vd) : null;
                    PythonApi.StartVideoRecording(vidPath, vidFps, vidFmt, vidView, vidDuration);
                    string durInfo = vidDuration.HasValue ? $", auto-stops at {vidDuration:F1}s" : "";
                    return $"[ok] {PythonApi.VideoFormat} recording started at {PythonApi.VideoFrameRate} fps ({vidView} view{durInfo})";
                }

                case "stop_video_recording" or "stop_recording":
                    PythonApi.StopVideoRecording();
                    return "[ok] video recording stop requested";

                case "start_gif_recording":
                {
                    string? vidPath = args.Length > 0 && !string.IsNullOrWhiteSpace(Unquote(args[0])) && !string.Equals(Unquote(args[0]), "null", StringComparison.OrdinalIgnoreCase) && !string.Equals(Unquote(args[0]), "none", StringComparison.OrdinalIgnoreCase) ? Unquote(args[0]) : null;
                    int vidFps = args.Length > 1 ? checked((int)ParseD(args[1])) : 10;
                    string vidView = namedArguments.TryGetValue("view", out var vv) ? Unquote(vv) : "recentre";
                    double? gifDuration = namedArguments.TryGetValue("duration", out var gd) ? (double?)ParseD(gd) : null;
                    PythonApi.StartVideoRecording(vidPath, vidFps, "GIF", vidView, gifDuration);
                    string durInfo = gifDuration.HasValue ? $", auto-stops at {gifDuration:F1}s" : "";
                    return $"[ok] GIF recording started at {PythonApi.VideoFrameRate} fps ({vidView} view{durInfo})";
                }

                case "stop_gif_recording":
                    PythonApi.StopVideoRecording();
                    return "[ok] GIF recording stop requested";

                case "is_video_recording":
                    return PythonApi.IsVideoRecording ? "True" : "False";



                case "add_single_phase_transmission_line" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddSinglePhaseTransmissionLine(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 230, 3), NamedD(n, "frequency", 50, 4), NamedD(n, "length_km", 1, 5),
                        NamedD(n, "resistance_per_km", 0.2, 6), NamedD(n, "inductance_per_km", 1e-3, 7),
                        NamedD(n, "capacitance_per_km", 10e-9, 8), NamedD(n, "conductance_per_km", 0, 9), NamedS(n, "model_type", "Short Line", 10));
                    return $"[ok] single-phase transmission line {Unquote(args[0])} added";
                }

                case "add_two_phase_transmission_line" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddTwoPhaseTransmissionLine(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 240, 3), NamedD(n, "frequency", 60, 4), NamedD(n, "length_km", 1, 5),
                        NamedD(n, "phase_displacement", 90, 6), NamedS(n, "configuration", "TrueTwoPhase", 7));
                    return $"[ok] two-phase transmission line {Unquote(args[0])} added";
                }

                case "add_three_phase_transmission_line" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddThreePhaseTransmissionLine(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_line_voltage", 11000, 3), NamedD(n, "frequency", 50, 4), NamedD(n, "length_km", 1, 5),
                        NamedS(n, "phase_sequence", "ABC", 6), NamedS(n, "connection", "Three-wire", 7), NamedS(n, "model_type", "Short", 8));
                    return $"[ok] three-phase transmission line {Unquote(args[0])} added";
                }

                case "add_ac_transformer" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddACTransformer(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "primary_voltage", 230, 3), NamedD(n, "secondary_voltage", 115, 4), NamedD(n, "apparent_power", 1000, 5),
                        NamedD(n, "frequency", 50, 6), NamedS(n, "connection", "Single-phase", 7));
                    return $"[ok] AC transformer {Unquote(args[0])} added";
                }

                case "add_synchronous_generator" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddSynchronousGenerator(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 400, 3), NamedD(n, "rated_frequency", 50, 4), NamedD(n, "rated_power", 10000, 5),
                        (int)NamedD(n, "phases", 3, 6), (int)NamedD(n, "poles", 4, 7));
                    return $"[ok] synchronous generator {Unquote(args[0])} added";
                }

                case "add_induction_motor" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddInductionMotor(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 400, 3), NamedD(n, "rated_power", 1500, 4), NamedD(n, "frequency", 50, 5), (int)NamedD(n, "poles", 4, 6));
                    return $"[ok] induction motor {Unquote(args[0])} added";
                }

                case "add_ac_bulb" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddACBulb(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 230, 3), NamedD(n, "rated_power", 60, 4), NamedB(n, "initially_on", true, 5), NamedD(n, "dimming_percent", 100, 6));
                    return $"[ok] AC bulb {Unquote(args[0])} added";
                }

                case "add_ac_fan" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddACFan(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "rated_voltage", 230, 3), NamedD(n, "rated_power", 75, 4), NamedD(n, "power_factor", 0.8, 5), (int)NamedD(n, "selected_speed", 3, 6));
                    return $"[ok] AC fan {Unquote(args[0])} added";
                }

                case "add_solar_panel" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddSolarPanel(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "maximum_power_rating", 300, 3), NamedD(n, "open_circuit_voltage", 40, 4),
                        NamedD(n, "short_circuit_current", 9, 5), NamedD(n, "irradiance", 1000, 6));
                    return $"[ok] solar panel {Unquote(args[0])} added";
                }

                case "add_battery" when args.Length >= 1:
                {
                    var n = ParseNamedArgs(args);
                    PythonApi.AddBattery(Unquote(args[0]), NamedD(n, "position_x", 100, 1), NamedD(n, "position_y", 100, 2),
                        NamedD(n, "nominal_voltage", 12, 3), NamedD(n, "capacity_ah", 100, 4),
                        NamedD(n, "initial_soc_percent", 80, 5), NamedS(n, "chemistry", "Lithium-ion", 6));
                    return $"[ok] battery {Unquote(args[0])} added";
                }

                default:
                    return $"[unknown method] {method}";
            }
        }
        catch (Exception ex)
        {
            return $"[error] {ex.Message}";
        }
    }

    private string SetWorkspaceSettingFromScript(string name, object value)
    {
        string key = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        switch (key)
        {
            case "autorunsimulation":
                _settingsViewModel.AutoRunSimulation = RequireWorkspaceSettingBoolean(name, value);
                break;
            case "defaultcategory":
            case "defaultpalettecategory":
            case "palettecategory":
            {
                string category = RequireWorkspaceSettingString(name, value);
                string? availableCategory = _settingsViewModel.AvailableCategories
                    .FirstOrDefault(candidate => candidate.Equals(category, StringComparison.OrdinalIgnoreCase));
                if (availableCategory == null)
                    throw new ArgumentException($"Unsupported palette category '{category}'.");
                _settingsViewModel.DefaultCategory = availableCategory;
                break;
            }
            case "viewportrefreshhz":
                _settingsViewModel.ViewportRefreshHz = RequireWorkspaceSettingInteger(name, value, 1, 120);
                _lastSettingsCacheRefreshUtc = DateTime.MinValue;
                break;
            case "graphrefreshhz":
                _settingsViewModel.GraphRefreshHz = RequireWorkspaceSettingInteger(name, value, 1, 120);
                _lastSettingsCacheRefreshUtc = DateTime.MinValue;
                break;
            case "interactionloophz":
                _settingsViewModel.InteractionLoopHz = RequireWorkspaceSettingInteger(name, value, 15, 120);
                break;
            case "uisnapshotdelayms":
                _settingsViewModel.UiSnapshotDelayMs = RequireWorkspaceSettingInteger(name, value, 0, 1000);
                _lastSettingsCacheRefreshUtc = DateTime.MinValue;
                break;
            case "voltagedeltathreshold":
            case "voltagechangethreshold":
                _settingsViewModel.VoltageChangeThreshold = RequireWorkspaceSettingNumber(name, value, 0);
                _lastSettingsCacheRefreshUtc = DateTime.MinValue;
                break;
            case "currentdeltathreshold":
            case "currentchangethreshold":
                _settingsViewModel.CurrentChangeThreshold = RequireWorkspaceSettingNumber(name, value, 0);
                _lastSettingsCacheRefreshUtc = DateTime.MinValue;
                break;
            case "reactiveoscillationthreshold":
                _settingsViewModel.ReactiveOscillationThreshold = RequireWorkspaceSettingNumber(name, value, 0);
                _lastSettingsCacheRefreshUtc = DateTime.MinValue;
                break;
            case "hcvpapibaseurl":
            case "remoteapibaseurl":
                _settingsViewModel.RemoteApiBaseUrl = RequireWorkspaceApiBaseUrl(name, value);
                break;
            case "enabletimescaling":
            case "timescalingenabled":
                _settingsViewModel.EnableTimeScaling = RequireWorkspaceSettingBoolean(name, value);
                break;
            case "simulationtime":
            case "simulationtimeamount":
                _settingsViewModel.SimulationTimeAmount = RequireWorkspaceSettingNumber(name, value, 1e-9);
                break;
            case "simulationtimeunit":
                _settingsViewModel.SimulationTimeUnit = RequireWorkspaceTimeUnit(name, value);
                break;
            case "devicetime":
            case "devicetimeamount":
                _settingsViewModel.DeviceTimeAmount = RequireWorkspaceSettingNumber(name, value, 1e-9);
                break;
            case "devicetimeunit":
                _settingsViewModel.DeviceTimeUnit = RequireWorkspaceTimeUnit(name, value);
                break;
            case "solverstep":
            case "simulationtimestepseconds":
                double stepVal = RequireWorkspaceSettingNumber(name, value, 1e-6);
                _settingsViewModel.SimulationTimeStepSeconds = stepVal;
                SimTransientStepTime = stepVal;
                break;
            case "automatictimestepselection":
                _settingsViewModel.AutomaticTimeStepSelection = RequireWorkspaceSettingBoolean(name, value);
                break;
            case "starttime":
            case "simulationstarttimeseconds":
                double startVal = RequireWorkspaceSettingNumber(name, value, 0);
                _settingsViewModel.SimulationStartTimeSeconds = startVal;
                SimTransientStartTime = startVal;
                SimTransientStopTime = startVal + _settingsViewModel.SimulationDurationSeconds;
                break;
            case "duration":
            case "simulationdurationseconds":
                double durVal = RequireWorkspaceSettingNumber(name, value, 1e-6);
                _settingsViewModel.SimulationDurationSeconds = durVal;
                SimTransientStopTime = SimTransientStartTime + durVal;
                break;
            default:
                throw new ArgumentException(
                    $"Unknown Workspace setting '{name}'. Use a Workspace setting name such as 'solver_step' or 'duration'.");
        }

        return $"[ok] workspace setting '{name}' updated";
    }

    private object GetWorkspaceSettingFromScript(string name)
    {
        string key = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return key switch
        {
            "autorunsimulation" => _settingsViewModel.AutoRunSimulation,
            "defaultcategory" or "defaultpalettecategory" or "palettecategory" => _settingsViewModel.DefaultCategory,
            "viewportrefreshhz" => _settingsViewModel.ViewportRefreshHz,
            "graphrefreshhz" => _settingsViewModel.GraphRefreshHz,
            "interactionloophz" => _settingsViewModel.InteractionLoopHz,
            "uisnapshotdelayms" => _settingsViewModel.UiSnapshotDelayMs,
            "voltagedeltathreshold" or "voltagechangethreshold" => _settingsViewModel.VoltageChangeThreshold,
            "currentdeltathreshold" or "currentchangethreshold" => _settingsViewModel.CurrentChangeThreshold,
            "reactiveoscillationthreshold" => _settingsViewModel.ReactiveOscillationThreshold,
            "hcvpapibaseurl" or "remoteapibaseurl" => _settingsViewModel.RemoteApiBaseUrl,
            "enabletimescaling" or "timescalingenabled" => _settingsViewModel.EnableTimeScaling,
            "simulationtime" or "simulationtimeamount" => _settingsViewModel.SimulationTimeAmount,
            "simulationtimeunit" => _settingsViewModel.SimulationTimeUnit,
            "devicetime" or "devicetimeamount" => _settingsViewModel.DeviceTimeAmount,
            "devicetimeunit" => _settingsViewModel.DeviceTimeUnit,
            "solverstep" or "simulationtimestepseconds" => _settingsViewModel.SimulationTimeStepSeconds,
            "automatictimestepselection" => _settingsViewModel.AutomaticTimeStepSelection,
            "starttime" or "simulationstarttimeseconds" => _settingsViewModel.SimulationStartTimeSeconds,
            "duration" or "simulationdurationseconds" => _settingsViewModel.SimulationDurationSeconds,
            "timescalingratio" => _settingsViewModel.TimeScalingRatio,
            _ => throw new ArgumentException($"Unknown Workspace setting '{name}'.")
        };
    }

    private string ResetWorkspaceSettingsFromScript()
    {
        _settingsViewModel.AutoRunSimulation = false;
        _settingsViewModel.DefaultCategory = "All";
        _settingsViewModel.ViewportRefreshHz = 30;
        _settingsViewModel.GraphRefreshHz = 20;
        _settingsViewModel.InteractionLoopHz = 60;
        _settingsViewModel.UiSnapshotDelayMs = 40;
        _settingsViewModel.VoltageChangeThreshold = 0.01;
        _settingsViewModel.CurrentChangeThreshold = 0.001;
        _settingsViewModel.ReactiveOscillationThreshold = 0.02;
        _settingsViewModel.RemoteApiBaseUrl = string.Empty;
        _settingsViewModel.EnableTimeScaling = false;
        _settingsViewModel.SimulationTimeAmount = 30.0;
        _settingsViewModel.SimulationTimeUnit = "seconds";
        _settingsViewModel.DeviceTimeAmount = 1.0;
        _settingsViewModel.DeviceTimeUnit = "hours";
        _settingsViewModel.SimulationTimeStepSeconds = 0.04;
        _settingsViewModel.AutomaticTimeStepSelection = true;
        _settingsViewModel.SimulationStartTimeSeconds = 0.0;
        _settingsViewModel.SimulationDurationSeconds = 60.0;
        _lastSettingsCacheRefreshUtc = DateTime.MinValue;
        SimTransientStepTime = 0.04;
        SimTransientStopTime = 60.0;
        SimTransientStartTime = 0.0;
        return "[ok] workspace settings reset to defaults";
    }

    private static bool ParseB(string s)
    {
        string raw = Unquote(s).Trim();
        if (bool.TryParse(raw, out var b)) return b;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d > 0.5;
        return false;
    }

    private string ScheduleInputFromScript(
        double delaySeconds,
        string componentName,
        string propertyName,
        object? value)
    {
        if (!double.IsFinite(delaySeconds) || delaySeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(delaySeconds), "Input delay must be finite and non-negative.");
        if (string.IsNullOrWhiteSpace(componentName))
            throw new ArgumentException("A component name is required.", nameof(componentName));
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("A property name is required.", nameof(propertyName));

        var component = Circuit.FindChild<ComponentNode>(componentName)
            ?? throw new KeyNotFoundException($"Component '{componentName}' was not found.");
        ValidateScheduledInputProperty(component, propertyName, value);

        double configuredStart = Math.Max(
            0, Preferences.Default.Get("Settings.SimulationStartTimeSeconds", 0.0));
        double playbackStart = _solverTimeSeconds > configuredStart
            ? _solverTimeSeconds
            : configuredStart;
        double eventTime = playbackStart + delaySeconds;
        if (!double.IsFinite(eventTime))
            throw new ArgumentOutOfRangeException(nameof(delaySeconds), "Scheduled input time is outside the supported numeric range.");

        if (delaySeconds == 0)
        {
            if (IsSimulationPlaying && _scheduledInputBoundaryPending)
                throw new InvalidOperationException("Cannot schedule an immediate input while a simulation-time event is being applied.");
            PythonApi.SetComponentProperty(componentName, propertyName, value);
            CircuitChanged?.Invoke();
            return $"[ok] input {componentName}.{propertyName} applied immediately";
        }

        lock (_scheduledInputLock)
        {
            var scheduled = new ScheduledSimulationInput(
                eventTime, _scheduledInputSequence++, componentName, propertyName, value);
            _scheduledSimulationInputs.Add(scheduled);
            _scheduledSimulationInputs.Sort(static (left, right) =>
            {
                int timeOrder = left.TimeSeconds.CompareTo(right.TimeSeconds);
                return timeOrder != 0 ? timeOrder : left.Sequence.CompareTo(right.Sequence);
            });
        }

        return $"[ok] input {componentName}.{propertyName} scheduled for simulation time {eventTime.ToString("G", CultureInfo.InvariantCulture)}s";
    }

    private static void ValidateScheduledInputProperty(ComponentNode component, string propertyName, object? value)
    {
        var property = component.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property?.SetMethod?.IsPublic == true &&
            property.GetIndexParameters().Length == 0 &&
            property.Name is not nameof(ComponentNode.Position) and
                not nameof(ComponentNode.X) and not nameof(ComponentNode.Y) and
                not nameof(ComponentNode.Rotation) and not nameof(ComponentNode.ScaleX) and
                not nameof(ComponentNode.ScaleY) and not nameof(ComponentNode.CustomProperties) and
                not nameof(ComponentNode.ElectricalParameters))
        {
            Type targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (value is null)
            {
                if (targetType.IsValueType)
                    throw new ArgumentException($"Property '{property.Name}' does not accept null.");
                return;
            }

            if (targetType.IsInstanceOfType(value))
                return;
            if (targetType.IsEnum && value is string enumText)
            {
                Enum.Parse(targetType, enumText, true);
                return;
            }

            Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return;
        }

        if (component.ElectricalParameters.ContainsKey(propertyName))
        {
            if (value is null || !double.TryParse(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out _))
                throw new ArgumentException($"Electrical parameter '{propertyName}' requires a numeric value.");
            return;
        }

        if (component.CustomProperties.ContainsKey(propertyName) && value is not null)
            return;

        throw new InvalidOperationException(
            $"'{propertyName}' is not an editable property on {component.ComponentType}.");
    }

    private double GetNextScheduledInputTime()
    {
        lock (_scheduledInputLock)
            return _scheduledSimulationInputs.Count == 0
                ? double.PositiveInfinity
                : _scheduledSimulationInputs[0].TimeSeconds;
    }

    private void ApplyDueScheduledInputs(double timeSeconds)
    {
        List<ScheduledSimulationInput> dueInputs;
        lock (_scheduledInputLock)
        {
            int dueCount = 0;
            while (dueCount < _scheduledSimulationInputs.Count &&
                   _scheduledSimulationInputs[dueCount].TimeSeconds <= timeSeconds + 1e-9)
                dueCount++;
            dueInputs = _scheduledSimulationInputs.GetRange(0, dueCount);
            _scheduledSimulationInputs.RemoveRange(0, dueCount);
        }

        foreach (ScheduledSimulationInput scheduled in dueInputs)
        {
            try
            {
                PythonApi.SetComponentProperty(
                    scheduled.ComponentName, scheduled.PropertyName, scheduled.Value);
                if (scheduled.TimeSeconds < timeSeconds - 1e-9)
                {
                    ConsoleOutput +=
                        $"Scheduled input for {scheduled.ComponentName}.{scheduled.PropertyName} " +
                        $"was applied late at {timeSeconds.ToString("G", CultureInfo.InvariantCulture)}s.\n";
                }
            }
            catch (Exception ex)
            {
                ConsoleOutput +=
                    $"Scheduled input failed at {scheduled.TimeSeconds.ToString("G", CultureInfo.InvariantCulture)}s " +
                    $"({scheduled.ComponentName}.{scheduled.PropertyName}): {ex.Message}\n";
            }
        }

        if (dueInputs.Count > 0)
            CircuitChanged?.Invoke();
    }

    private void ClearScheduledInputs()
    {
        lock (_scheduledInputLock)
            _scheduledSimulationInputs.Clear();
        _scheduledInputBoundaryPending = false;
    }

    private static double ParseFiniteScriptNumber(string raw, string argumentName)
    {
        if (!double.TryParse(
                Unquote(raw),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value) ||
            !double.IsFinite(value))
            throw new ArgumentException($"'{argumentName}' must be a finite number.");
        return value;
    }

    private static long ParseScriptLong(string raw)
    {
        if (!long.TryParse(
                Unquote(raw),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long value))
            throw new ArgumentException("Expected an integer value.");
        return value;
    }

    private void StepSimulationOnce()
    {
        double step = Math.Max(SimTransientStepTime, 1e-6);
        PythonApi.AdvanceSimulation(step);
        CircuitChanged?.Invoke();
    }

    private static bool RequireWorkspaceSettingBoolean(string name, object value) =>
        value is bool boolean
            ? boolean
            : throw new ArgumentException($"Workspace setting '{name}' requires a boolean value.");

    private static string RequireWorkspaceSettingString(string name, object value) =>
        value is string text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : throw new ArgumentException($"Workspace setting '{name}' requires a non-empty string.");

    private static int RequireWorkspaceSettingInteger(string name, object value, int minimum, int maximum)
    {
        double number = RequireWorkspaceSettingNumber(name, value, minimum);
        if (number != Math.Truncate(number) || number > maximum)
            throw new ArgumentOutOfRangeException(name, $"Workspace setting '{name}' must be an integer from {minimum} to {maximum}.");
        return (int)number;
    }

    private static double RequireWorkspaceSettingNumber(string name, object value, double minimum)
    {
        if (value is string || value is bool || value is not IConvertible)
            throw new ArgumentException($"Workspace setting '{name}' requires a numeric value.");

        double number;
        try
        {
            number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new ArgumentException($"Workspace setting '{name}' requires a numeric value.", exception);
        }

        if (!double.IsFinite(number) || number < minimum)
            throw new ArgumentOutOfRangeException(name, $"Workspace setting '{name}' must be finite and at least {minimum}.");
        return number;
    }

    private static string RequireWorkspaceTimeUnit(string name, object value)
    {
        string unit = RequireWorkspaceSettingString(name, value);
        string? availableUnit = new[] { "seconds", "minutes", "hours", "days" }
            .FirstOrDefault(candidate => candidate.Equals(unit, StringComparison.OrdinalIgnoreCase));
        return availableUnit
            ?? throw new ArgumentException($"Workspace setting '{name}' must be seconds, minutes, hours, or days.");
    }

    private static string RequireWorkspaceApiBaseUrl(string name, object value)
    {
        string baseUrl = RequireWorkspaceSettingString(name, value);
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Workspace setting '{name}' must be an absolute HTTPS URL.");

        bool allowed = uri.Scheme == Uri.UriSchemeHttps;
#if DEBUG
        allowed |= uri.Scheme == Uri.UriSchemeHttp &&
            (uri.IsLoopback || string.Equals(uri.Host, "10.0.2.2", StringComparison.OrdinalIgnoreCase));
#endif
        return allowed
            ? baseUrl
            : throw new ArgumentException($"Workspace setting '{name}' must use HTTPS.");
    }

    private string AddScriptComponentWithCoordinates(string method, Dictionary<string, string> named, bool logicalPlacement)
    {
        var normalizedNamed = named.ToDictionary(
            pair => NormalizeScriptArgumentName(pair.Key), pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        int row = 0;
        int column = 0;
        if (logicalPlacement)
        {
            if (!normalizedNamed.TryGetValue("row", out string? rowText) ||
                !int.TryParse(Unquote(rowText), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out row) || row < 1)
                return "[error] logical placement requires a positive 1-based row";
            if (!normalizedNamed.TryGetValue("col", out string? columnText) ||
                !int.TryParse(Unquote(columnText), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out column) || column < 1)
                return "[error] logical placement requires a positive 1-based column";
        }

        string apiName = method switch
        {
            "add_ttl" or "add_ttl_package" => "AddTtlPackage",
            "add_dnc" => "AddDoNotConnect",
            "add_coax" => "AddCoaxialCable",
            "add_microstrip" => "AddMicrostripLine",
            _ => "Add" + string.Concat(method[4..].Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]))
        };
        var apiMethod = typeof(PythonApi).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(candidate => candidate.Name.Equals(apiName, StringComparison.OrdinalIgnoreCase));
        if (apiMethod == null)
            return $"[error] no Python API component creator is registered for '{method}'";

        var positional = normalizedNamed
            .Where(pair => pair.Key.StartsWith("arg", StringComparison.Ordinal) &&
                int.TryParse(pair.Key[3..], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
            .OrderBy(pair => int.Parse(pair.Key[3..], System.Globalization.CultureInfo.InvariantCulture))
            .Select(pair => pair.Value)
            .ToArray();
        var parameters = apiMethod.GetParameters();
        var values = new object?[parameters.Length];
        int positionalIndex = 0;
        int nameIndex = -1;

        for (int index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            string key = NormalizeScriptArgumentName(parameter.Name ?? string.Empty);
            if (key == "name") nameIndex = index;
            if (key is "x" or "y")
            {
                if (logicalPlacement)
                    values[index] = 0.0;
                else if (normalizedNamed.TryGetValue("position" + key, out string? positionValue))
                    values[index] = ConvertScriptArgument(positionValue, parameter.ParameterType);
                else if (normalizedNamed.TryGetValue(key, out string? coordinateValue))
                    values[index] = ConvertScriptArgument(coordinateValue, parameter.ParameterType);
                else
                    values[index] = 0.0;
                continue;
            }

            if (normalizedNamed.TryGetValue(key, out string? namedValue))
            {
                values[index] = ConvertScriptArgument(namedValue, parameter.ParameterType);
            }
            else if (positionalIndex < positional.Length)
            {
                values[index] = ConvertScriptArgument(positional[positionalIndex++], parameter.ParameterType);
            }
            else if (parameter.HasDefaultValue)
            {
                values[index] = parameter.DefaultValue;
            }
            else
            {
                return $"[error] missing required '{parameter.Name}' argument for {method}";
            }
        }

        if (nameIndex < 0 || values[nameIndex] is not string componentName || string.IsNullOrWhiteSpace(componentName))
            return "[error] a component name is required for logical placement";

        try
        {
            apiMethod.Invoke(PythonApi, values);
        }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw new InvalidOperationException(exception.InnerException.Message, exception.InnerException);
        }

        foreach (var pair in normalizedNamed)
        {
            if (pair.Key.StartsWith("arg", StringComparison.Ordinal) || pair.Key is "position" or "positionx" or "positiony" or "x" or "y" or "row" or "col")
                continue;
            if (parameters.Any(parameter => NormalizeScriptArgumentName(parameter.Name ?? string.Empty) == pair.Key))
                continue;

            ComponentNode? component = Circuit.FindChild<ComponentNode>(componentName);
            var property = component?.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(candidate => NormalizeScriptPropertyName(candidate.Name) == NormalizeScriptPropertyName(pair.Key) &&
                    candidate.SetMethod?.IsPublic == true && candidate.GetIndexParameters().Length == 0);
            if (property != null)
                PythonApi.SetComponentProperty(componentName, property.Name, ParseScriptValue(pair.Value));
            else
                PythonApi.SetCustomProperty(componentName, pair.Key, ParseScriptValue(pair.Value));
        }

        if (!logicalPlacement)
            return $"[ok] component {componentName} added at explicit position";

        PythonApi.SetLogicalGridPosition(componentName, row, column);
        return $"[ok] component {componentName} added at row {row}, column {column}";
    }

    private static string NormalizeScriptArgumentName(string name) =>
        name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "ratedprimaryvoltage" => "primaryvoltage",
            "ratedsecondaryvoltage" => "secondaryvoltage",
            "ratedapparentpower" => "apparentpower",
            "numberofphases" => "phases",
            "numberofpoles" => "poles",
            "acmag" => "acmagnitude",
            "sineamp" => "sineamplitude",
            "sinefreq" => "sinefrequency",
            "esr" => "equivalentseriesresistance",
            "dcr" => "dcresistance",
            "is" => "isat",
            "rs" => "seriesresistance",
            "bv" => "breakdownvoltage",
            "cj0" => "junctioncapacitance",
            "tt" => "transittime",
            "ibv" => "breakdowncurrent",
            "waveformtype" => "waveform",
            "inductor1" => "ind1",
            "inductor2" => "ind2",
            "gain" => "voltagegain",
            "selectlocation" => "selectloc",
            "voltagehigh" => "vhigh",
            "voltagelow" => "vlow",
            _ => name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant()
        };

    private static string NormalizeScriptPropertyName(string name) =>
        name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "isat" or "is" => "saturationcurrent",
            "bv" => "breakdownvoltage",
            "rs" => "seriesresistance",
            "cj0" => "junctioncapacitance",
            "tt" => "transittime",
            "ibv" => "breakdowncurrent",
            "vf" => "forwardvoltage",
            "acmag" => "acmagnitude",
            "sineamp" => "sineamplitude",
            "sinefreq" => "sinefrequency",
            "esr" => "equivalentseriesresistance",
            "dcr" => "dcresistance",
            _ => name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant()
        };

    private void ApplyLogicalSchematicLayout()
    {
        LogicalGridLayoutResult layout = PythonApi.ApplyLogicalGridLayout();

        // Re-resolve any connection routing points that were stored as grid-cell world
        // coordinates BEFORE the physical layout was applied.  After ApplyLogicalGridLayout
        // the component positions are authoritative; we recompute the routing points from
        // the now-known cell centers so that the wire router has correct intermediate coords.
        foreach (var connection in Circuit.Connections)
        {
            if (connection.RoutingPoints.Count == 0) continue;

            bool anySmall = connection.RoutingPoints.Any(p =>
                p.X < 60 && p.Y < 60 && p.X >= 1 && p.Y >= 1);
            if (!anySmall) continue; // already in world coords — skip

            // All points in this connection were grid-cell references; re-resolve them.
            var resolved = new List<EESimulator.Core.Models.Point2D>(connection.RoutingPoints.Count);
            foreach (var pt in connection.RoutingPoints)
            {
                // If the value looks like a grid cell coord (small positive int), convert it
                // using the now-valid layout. Otherwise keep the raw value.
                if (pt.X >= 1 && pt.X < 60 && pt.Y >= 1 && pt.Y < 60)
                {
                    int row = (int)Math.Round(pt.Y);
                    int col = (int)Math.Round(pt.X);
                    // Check if a component sits exactly at this cell; prefer its centre.
                    var compAtCell = layout.Placements.FirstOrDefault(p =>
                        p.Component.LogicalRow == row && p.Component.LogicalColumn == col);
                    resolved.Add(compAtCell.Component != null
                        ? new EESimulator.Core.Models.Point2D(compAtCell.X, compAtCell.Y)
                        : layout.GetCellCenter(row, col));
                }
                else
                {
                    resolved.Add(pt);
                }
            }
            connection.RoutingPoints.Clear();
            connection.RoutingPoints.AddRange(resolved);
        }

        if (layout.UnresolvedCollisions.Count == 0) return;
        string examples = string.Join(", ", layout.UnresolvedCollisions.Take(3)
            .Select(collision => $"{collision.First.Reference}/{collision.Second.Reference}"));
        string warning = $"Automatic layout left {layout.UnresolvedCollisions.Count} unresolved component collision(s): {examples}.";
        ConsoleOutput += $"\n[layout] {warning}\n";
        StatusText = warning;
    }

    private static object? ConvertScriptArgument(string text, Type destination)
    {
        Type target = Nullable.GetUnderlyingType(destination) ?? destination;
        object value = ParseScriptValue(text);
        if (target.IsInstanceOfType(value)) return value;
        if (target.IsEnum)
            return Enum.Parse(target, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!, true);
        if (target == typeof(string)) return Unquote(text);
        return Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, string> ParseNamedArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].Trim();
            var eqIdx = arg.IndexOf('=');
            if (eqIdx > 0)
            {
                var key = arg[..eqIdx].Trim();
                var val = arg[(eqIdx + 1)..].Trim();
                if (key.Equals("position", StringComparison.OrdinalIgnoreCase) && val.StartsWith('(') && val.EndsWith(')'))
                {
                    var coords = val.Trim('(', ')').Split(',');
                    if (coords.Length >= 2)
                    {
                        map["position_x"] = coords[0].Trim();
                        map["position_y"] = coords[1].Trim();
                    }
                }
                else
                {
                    map[key] = val;
                }
            }
            else
            {
                map[$"arg_{i}"] = arg;
            }
        }
        return map;
    }

    private static double NamedD(Dictionary<string, string> named, string key, double fallback = 0, int posIndex = -1)
    {
        if (named.TryGetValue(key, out string? value)) return ParseD(Unquote(value));
        if (posIndex >= 0 && named.TryGetValue($"arg_{posIndex}", out string? posVal)) return ParseD(Unquote(posVal));
        return fallback;
    }

    private static string NamedS(Dictionary<string, string> named, string key, string fallback, int posIndex = -1)
    {
        if (named.TryGetValue(key, out string? value)) return Unquote(value);
        if (posIndex >= 0 && named.TryGetValue($"arg_{posIndex}", out string? posVal)) return Unquote(posVal);
        return fallback;
    }

    private static bool NamedB(Dictionary<string, string> named, string key, bool fallback, int posIndex = -1)
    {
        if (named.TryGetValue(key, out string? value) && bool.TryParse(Unquote(value), out bool parsed)) return parsed;
        if (posIndex >= 0 && named.TryGetValue($"arg_{posIndex}", out string? posVal) && bool.TryParse(Unquote(posVal), out bool posParsed)) return posParsed;
        return fallback;
    }

    private static string[] SplitArgs(string argsStr)
    {
        // Split top-level commas while preserving quoted text and nested tuples.
        var result = new System.Collections.Generic.List<string>();
        int depth = 0;
        char quote = '\0';
        bool escaped = false;
        var cur = new StringBuilder();
        foreach (char c in argsStr)
        {
            if (escaped)
            {
                cur.Append(c);
                escaped = false;
                continue;
            }
            if (c == '\\' && quote != '\0')
            {
                cur.Append(c);
                escaped = true;
                continue;
            }
            if (quote != '\0')
            {
                cur.Append(c);
                if (c == quote) quote = '\0';
                continue;
            }
            if (c is '\'' or '"')
            {
                quote = c;
                cur.Append(c);
            }
            else if (c is '(' or '[' or '{')
            {
                depth++;
                cur.Append(c);
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
                cur.Append(c);
            }
            else if (c == ',' && depth == 0)
            {
                result.Add(cur.ToString().Trim());
                cur.Clear();
            }
            else cur.Append(c);
        }
        if (cur.Length > 0) result.Add(cur.ToString().Trim());
        return result.ToArray();
    }

    private static string Unquote(string s)
    {
        string value = s.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            try { return JsonSerializer.Deserialize<string>(value) ?? string.Empty; }
            catch { /* Fall back to the existing permissive quote trimming for legacy scripts. */ }
        }
        return value.Trim('\'').Trim('"');
    }

    private static double ParseD(string s) =>
        double.TryParse(s.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static object ParseScriptValue(string raw)
    {
        string value = Unquote(raw);
        if (bool.TryParse(value, out var boolean)) return boolean;
        if (long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer)) return integer;
        if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var number)) return number;
        return value;
    }

    private static string FormatResults(SimulationResultsData r)
    {
        if (!r.Success)
            return $"Error: {r.Message}";

        var sb = new StringBuilder();
        sb.AppendLine("=== Simulation Results ===");
        foreach (var kv in r.OperatingPointVoltages)
            sb.AppendLine($"  Net {kv.Key}: {kv.Value:F4} V");
        foreach (var kv in r.OperatingPointCurrents)
            sb.AppendLine($"  {kv.Key}: {kv.Value:G4} A");
        if (r.OperatingPointVoltages.Count == 0 && r.OperatingPointCurrents.Count == 0)
            sb.AppendLine("  (No results — check circuit connectivity)");
        if (r.Message.Contains("Transient waveform capture failed", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"\n{r.Message}");
        return sb.ToString();
    }

    /// <summary>
    /// Appends time-variant array output for all probes in the circuit that have
    /// <see cref="ProbeComponent.ArrayOutputEnabled"/> set to true.
    /// Called after every simulation step (and optionally after every live-playback tick
    /// for probes with <see cref="ProbeComponent.RealtimeArray"/> true).
    /// </summary>
    private string FormatProbeArrayOutput(bool realtimeTickOnly = false)
    {
        var probes = Circuit.FindChildren<ProbeComponent>()
            .Where(p => p.ArrayOutputEnabled && (!realtimeTickOnly || p.RealtimeArray))
            .ToList();

        if (probes.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("=== Probe Waveform Arrays ===");

        foreach (var probe in probes)
        {
            List<double> times;
            List<double> values;

            lock (probe)
            {
                times  = new List<double>(probe.WaveformTime);
                // Use the "in" pin voltage as the primary reading (index 0 pin)
                var pinId = probe.Pins.FirstOrDefault()?.Id ?? "in";
                if (probe.WaveformNodeVoltages.TryGetValue(pinId, out var nodeVols) && nodeVols.Count > 0)
                    values = new List<double>(nodeVols);
                else
                    values = new List<double>(probe.WaveformVoltage);
            }

            if (times.Count == 0 || values.Count == 0)
            {
                sb.AppendLine($"  {probe.Reference} ({probe.MeasurementMode}): [no waveform data — run a Transient simulation]");
                continue;
            }

            // --- Apply time window ---
            double winStart = probe.TimeRangeStart;
            double winEnd   = probe.TimeRangeDuration > 0
                ? winStart + probe.TimeRangeDuration
                : double.MaxValue;

            // --- Sub-sample and collect ---
            int step     = Math.Max(1, probe.TimeScaleFactor);
            int maxCount = probe.MaxReadings > 0 ? Math.Min(probe.MaxReadings, 500) : 500;
            string unit  = probe.MeasurementMode.Equals("Current", StringComparison.OrdinalIgnoreCase) ? "A" : "V";

            var entries = new List<(double t, double v)>();
            for (int i = 0; i < times.Count && i < values.Count; i += step)
            {
                double t = times[i];
                if (t < winStart || t > winEnd) continue;
                entries.Add((t, values[i]));
                if (entries.Count >= maxCount) break;
            }

            if (entries.Count == 0)
            {
                sb.AppendLine($"  {probe.Reference}: [no data in window t=[{winStart:G}s … {(probe.TimeRangeDuration > 0 ? (winStart + probe.TimeRangeDuration).ToString("G") + "s" : "end")}]]");
                continue;
            }

            // Format: P1 (Voltage, t=0s..2s, step×5, 40 pts): [0.0000V@0.000s, 1.2340V@0.010s, …]
            string windowDesc = probe.TimeRangeDuration > 0
                ? $"t={winStart:G}s‥{winStart + probe.TimeRangeDuration:G}s"
                : winStart > 0 ? $"t≥{winStart:G}s" : "all time";
            string oscTag = probe.IsOscillating
                ? $", Osc: {probe.Frequency:F1}Hz"
                : ", Steady DC";
            string meta = $"{probe.MeasurementMode}, {windowDesc}" +
                          (step > 1 ? $", ×{step} step" : "") +
                          $", Min: {probe.Minimum:G4}{unit}, Max: {probe.Maximum:G4}{unit}, Range: {probe.AmplitudeRange:G4}{unit}{oscTag}" +
                          $", {entries.Count} pts";

            sb.Append($"  {probe.Reference} ({meta}): [");
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{entries[i].v:G4}{unit}@{entries[i].t:G4}s");
            }
            sb.AppendLine("]");
        }

        return sb.ToString();
    }

}


/// <summary>Record representing an item in the component palette sidebar.</summary>
public record ComponentPaletteItem(
    string Type,
    string Symbol,
    string Unit,
    string Color,
    string Category = "Electrical",
    string Description = "",
    string SvgIcon = "")
{
    public string DisplayText => $"{Symbol}  {Type}";
    public string IconLetter  => Symbol;
    public bool IsCustomCircuit => Category.Equals("My Circuits", StringComparison.OrdinalIgnoreCase);
    public bool HasCustomSvg => IsCustomCircuit && SvgIcon.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    public bool IsCustomWithoutSvg => IsCustomCircuit && !HasCustomSvg;
    public HtmlWebViewSource? CustomSvgSource => HasCustomSvg
        ? new HtmlWebViewSource
        {
            Html = $"<html><head><meta name='viewport' content='width=device-width, initial-scale=1'/><style>html,body{{margin:0;width:100%;height:100%;background:transparent;overflow:hidden}}svg{{width:100%;height:100%}}</style></head><body>{SvgIcon}</body></html>"
        }
        : null;
    public string TooltipText => $"{Type}  [{Category}]\n{Description}\nUnit: {Unit}";
    public string ImageSource => !string.IsNullOrEmpty(SvgIcon)
        ? (SvgIcon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? SvgIcon[..^4] + ".png" : SvgIcon)
        : "";
    public string SvgSource   => SvgIcon;
    public string AssetName   => !string.IsNullOrEmpty(SvgIcon) && SvgIcon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
        ? SvgIcon[..^4]
        : SvgIcon;
}

/// <summary>Editable pin/node model for component edit dialog.</summary>
public class PinEditItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string Id { get; set; } = string.Empty;

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public PinEditItem() { }

    public PinEditItem(string id, string name)
    {
        Id = id;
        _name = name;
    }
}

/// <summary>A named group of component palette items shown with a category header.</summary>
public class ComponentPaletteGroup
{
    public string Header { get; }
    public IReadOnlyList<ComponentPaletteItem> Items { get; }
    public ComponentPaletteGroup(string header, IEnumerable<ComponentPaletteItem> items)
    {
        Header = header;
        Items  = items.ToList();
    }
}

public partial class CustomCircuitPortDraft : ObservableObject
{
    public Guid InnerComponentId { get; }
    public string InnerPinId { get; }
    public string ComponentReference { get; }
    public string OriginalPinName { get; }
    public double RelativeX { get; }
    public double RelativeY { get; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    public CustomCircuitPortDraft(Guid innerComponentId, string innerPinId, string componentReference,
        string originalPinName, string name, double relativeX, double relativeY)
    {
        InnerComponentId = innerComponentId;
        InnerPinId = innerPinId;
        ComponentReference = componentReference;
        OriginalPinName = originalPinName;
        Name = name;
        RelativeX = relativeX;
        RelativeY = relativeY;
    }
}
