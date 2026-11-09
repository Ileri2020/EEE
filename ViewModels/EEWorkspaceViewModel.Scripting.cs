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

                case "connect" when args.Length >= 4:
                    PythonApi.Connect(Unquote(args[0]), Unquote(args[1]),
                        Unquote(args[2]), Unquote(args[3]));
                    return "[ok] connected";

                case "set_position" when args.Length >= 3:
                    PythonApi.SetPosition(Unquote(args[0]), ParseD(args[1]), ParseD(args[2]));
                    return "[ok] position set";

                case "set_property" when args.Length >= 3:
                    PythonApi.SetProperty(Unquote(args[0]), Unquote(args[1]), ParseD(args[2]));
                    return "[ok] property set";

                case "set_component_property" when args.Length >= 3:
                    PythonApi.SetComponentProperty(Unquote(args[0]), Unquote(args[1]), ParseScriptValue(args[2]));
                    CircuitChanged?.Invoke();
                    return "[ok] component property set";

                case "set_custom_property" when args.Length >= 3:
                    PythonApi.SetCustomProperty(Unquote(args[0]), Unquote(args[1]), ParseScriptValue(args[2]));
                    CircuitChanged?.Invoke();
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

                case "play" or "simulation_play":
                    PythonApi.PlaySimulation();
                    return "[ok] simulation playback started";

                case "pause" or "simulation_pause":
                    PythonApi.PauseSimulation();
                    return "[ok] simulation playback paused";

                case "play_pause" or "simulation_toggle":
                    PythonApi.ToggleSimulationPlayback();
                    return "[ok] simulation playback toggled";

                case "stop" or "simulation_stop":
                    PythonApi.StopSimulation();
                    return "[ok] simulation playback stopped";

                case "toggle_sidebar" or "toggle_component_sidebar":
                    bool? reqVis = args.Length > 0 ? (bool.TryParse(Unquote(args[0]), out var vb) ? vb : null) : null;
                    PythonApi.ToggleComponentSidebar(reqVis);
                    return $"[ok] component sidebar is now {(IsSidebarVisible ? "visible" : "hidden")}";

                case "toggle_control_sidebar":
                    bool? reqCtrlVis = args.Length > 0 ? (bool.TryParse(Unquote(args[0]), out var cvb) ? cvb : null) : null;
                    PythonApi.ToggleControlSidebar(reqCtrlVis);
                    return $"[ok] control sidebar is now {(IsControlSidebarVisible ? "visible" : "hidden")}";

                case "set_sidebar" when args.Length >= 2:
                    bool sVis = bool.TryParse(Unquote(args[1]), out var sbv) && sbv;
                    PythonApi.SetSidebarVisible(Unquote(args[0]), sVis);
                    return $"[ok] sidebar '{Unquote(args[0])}' set to {sVis}";

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
            else if (c == '(')
            {
                depth++;
                cur.Append(c);
            }
            else if (c == ')')
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
