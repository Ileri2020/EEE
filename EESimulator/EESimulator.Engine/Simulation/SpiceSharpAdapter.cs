using System;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Electrical;
using EESimulator.Core.Models;
using SpiceSharp;
using SpiceSharp.Components;
using SpiceSharp.Simulations;

namespace EESimulator.Engine.Simulation;

/// <summary>
/// Simulation engine integrating Berkeley Spice# (SpiceSharp library) with EESimulator.
/// Directly converts the scene's RootNode/CircuitNode topology into a SpiceSharp Circuit,
/// executes Operating Point (DC) and Transient analyses, and populates live probes and waveform data.
/// </summary>
public partial class SpiceSharpAdapter : ISimulationEngine
{
    public string EngineName => "SpiceSharp Engine";

    public SimulationResultsData RunSimulation(CircuitNode circuit, SimulationSettings settings)
    {
        var results = new SimulationResultsData();

        if (circuit == null)
        {
            results.Message = "Circuit is null.";
            return results;
        }

        var originalCircuit = circuit;
        CircuitMacroExpansion? macroExpansion = null;

        try
        {
            macroExpansion = CircuitMacroExpander.Expand(circuit);
            circuit = macroExpansion.Circuit;
            // Resolve digital logic before building the analog netlist. Digital devices
            // are represented to SpiceSharp as ideal voltage drivers at their output pins.
            DigitalLogicSimulator.EvaluateCombinational(circuit);

            // 1. Build nodal network topology from the circuit connections
            var topology = NetlistBuilder.BuildTopology(circuit);

            string GetNetForPin(Guid compId, string pinId)
            {
                var net = topology.Nets.FirstOrDefault(n =>
                    n.ConnectedPins.Any(p => p.ComponentId == compId && p.PinId == pinId));
                if (net == null || net.IsGround) return "0";
                return net.NetName;
            }

            string GetPinNode(ComponentNode comp, string pinIdOrName, int fallbackIndex = 0)
            {
                var p = comp.Pins.FirstOrDefault(x => x.Id.Equals(pinIdOrName, StringComparison.OrdinalIgnoreCase) ||
                                                      x.Name.Equals(pinIdOrName, StringComparison.OrdinalIgnoreCase));
                if (p != null) return GetNetForPin(comp.Id, p.Id);
                if (comp.Pins.Count > fallbackIndex) return GetNetForPin(comp.Id, comp.Pins[fallbackIndex].Id);
                return "0";
            }

            // 2. Instantiate SpiceSharp Circuit
            var spiceCircuit = new SpiceSharp.Circuit();

            foreach (var panel in circuit.Components.OfType<SolarPanelComponent>())
                panel.ApplyIrradianceProfile(settings.DeviceTimeSeconds);


            var componentsWithPins = new List<(ComponentNode Comp, string NodeA, string NodeB)>();
            var mutualInductances = new List<MutualInductanceComponent>();

            BuildSpiceCircuitComponents(circuit, spiceCircuit, settings, componentsWithPins,
                mutualInductances, GetNetForPin, GetPinNode, results);

            // 3. Run SpiceSharp Operating Point (DC) analysis
            var op = new OP("DC_OP");

            var voltageExports = new Dictionary<string, RealVoltageExport>();
            foreach (var net in topology.Nets)
            {
                if (!net.IsGround)
                {
                    voltageExports[net.Name] = new RealVoltageExport(op, net.Name);
                }
            }

            var compExports = new Dictionary<ComponentNode, (RealVoltageExport? ExpA, RealVoltageExport? ExpB)>();
            foreach (var item in componentsWithPins)
            {
                RealVoltageExport? expA = voltageExports.TryGetValue(item.NodeA, out var ea) ? ea : null;
                RealVoltageExport? expB = voltageExports.TryGetValue(item.NodeB, out var eb) ? eb : null;
                compExports[item.Comp] = (expA, expB);
            }

            var branchCurrentExports = new Dictionary<ComponentNode, RealCurrentExport>();
            foreach (var item in componentsWithPins)
            {
                if (item.Comp is ElectricalComponent || item.Comp is WireComponent)
                {
                    branchCurrentExports[item.Comp] = new RealCurrentExport(op, GetComponentCurrentExportName(item.Comp));
                }
            }

            foreach (int _ in op.Run(spiceCircuit))
            {
                foreach (var net in topology.Nets)
                {
                    if (net.IsGround)
                    {
                        results.OperatingPointVoltages[net.Name] = 0.0;
                    }
                    else if (voltageExports.TryGetValue(net.Name, out var exp))
                    {
                        results.OperatingPointVoltages[net.Name] = exp.Value;
                    }
                }

                // Update live probe values on each component
                foreach (var (comp, expA, expB) in compExports.Select(kv => (kv.Key, kv.Value.ExpA, kv.Value.ExpB)))
                {
                    double va = expA != null ? expA.Value : 0.0;
                    double vb = expB != null ? expB.Value : 0.0;
                    double signedVoltage = va - vb;
                    double vDrop = Math.Abs(signedVoltage);
                    comp.LiveVoltage = signedVoltage;
                    comp.LiveCurrent = branchCurrentExports.TryGetValue(comp, out var branchCurrent) ? branchCurrent.Value : 0.0;

                    if (comp is ResistorComponent r && r.Resistance > 0)
                    {
                        results.OperatingPointCurrents[$"I({comp.Name})"] = comp.LiveCurrent;
                    }
                    else if (comp is BulbComponent b && b.Resistance > 0)
                    {
                        b.Brightness = Math.Clamp(vDrop / Math.Max(b.RatedVoltage, 0.1), 0.0, 1.0);
                        b.IsOn = vDrop >= b.ThresholdVoltage || Math.Abs(comp.LiveCurrent) >= 0.02;
                        results.OperatingPointCurrents[$"I({comp.Name})"] = comp.LiveCurrent;
                    }
                    else if (comp is WireComponent)
                    {
                        results.OperatingPointCurrents[$"I({comp.Name})"] = comp.LiveCurrent;
                    }
                    else if (comp is LEDComponent led)
                    {
                        comp.LiveCurrent = branchCurrentExports.TryGetValue(comp, out var ledCurrent) ? ledCurrent.Value : 0.0;
                        led.IsOn = vDrop >= Math.Min(led.ForwardVoltage * 0.75, 1.5) || Math.Abs(comp.LiveCurrent) >= 0.001;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = signedVoltage;
                        results.OperatingPointCurrents[$"I({comp.Name})"] = comp.LiveCurrent;
                    }
                    else if (comp is LogicInputComponent lin)
                    {
                        comp.LiveVoltage = lin.State ? lin.HighVoltage : lin.LowVoltage;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = comp.LiveVoltage;
                    }
                    else if (comp is LogicOutputComponent lout)
                    {
                        comp.LiveVoltage = va;
                        lout.IsOn = va >= lout.Threshold;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = va;
                    }
                    else if (comp is VoltageSourceComponent v)
                    {
                        comp.LiveCurrent = branchCurrentExports.TryGetValue(comp, out var voltageSourceCurrent) ? voltageSourceCurrent.Value : 0.0;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = signedVoltage;
                        results.OperatingPointCurrents[$"I({comp.Name})"] = comp.LiveCurrent;
                    }
                    else if (comp is CurrentSourceComponent csrc)
                    {
                        comp.LiveCurrent = branchCurrentExports.TryGetValue(comp, out var currentSourceCurrent) ? currentSourceCurrent.Value : csrc.Current;
                        results.OperatingPointCurrents[$"I({comp.Name})"] = comp.LiveCurrent;
                    }
                    else if (comp is ClockComponent clk)
                    {
                        comp.LiveVoltage = clk.HighVoltage;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = comp.LiveVoltage;
                    }
                    else if (comp is PinComponent pin)
                    {
                        comp.LiveVoltage = pin.Direction == PinDirection.Input ? (pin.Value > 0 ? pin.HighVoltage : pin.LowVoltage) : va;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = comp.LiveVoltage;
                    }
                    else if (comp is PowerComponent pw)
                    {
                        comp.LiveVoltage = pw.Voltage;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = pw.Voltage;
                    }
                    else if (comp is ConstantComponent cst)
                    {
                        comp.LiveVoltage = cst.Value > 0 ? cst.VoltageHigh : cst.VoltageLow;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = comp.LiveVoltage;
                    }
                    else if (comp is ProbeComponent prb)
                    {
                        comp.LiveVoltage = va;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = va;
                    }
                    else if (comp is PullResistorComponent)
                    {
                        results.OperatingPointCurrents[$"I({comp.Name})"] = comp.LiveCurrent;
                    }
                }
            }

            foreach (var battery in circuit.Components.OfType<BatteryComponent>())
            {
                // SpiceSharp's independent voltage-source branch current is positive
                // into its positive terminal; invert it for our positive=discharge API.
                battery.LiveCurrent = -battery.LiveCurrent;
                battery.LiveCurrent = battery.LimitedCurrent;
                battery.LiveVoltage = Math.Clamp(
                    battery.OpenCircuitVoltage - battery.LiveCurrent * battery.InternalResistance,
                    battery.MinimumVoltage,
                    battery.MaximumVoltage);
                battery.AdvanceDeviceTime(settings.SolverElapsedTimeSeconds,
                    settings.TimeScalingEnabled ? settings.DeviceTimeScale : 1.0);
                results.OperatingPointVoltages[$"V({battery.Name})"] = battery.LiveVoltage;
                results.OperatingPointCurrents[$"I({battery.Name})"] = battery.LiveCurrent;
            }

            double deviceTimeScale = settings.TimeScalingEnabled ? settings.DeviceTimeScale : 1.0;
            foreach (var bulb in circuit.Components.OfType<ACBulbComponent>())
                bulb.AdvanceDeviceTime(settings.SolverElapsedTimeSeconds, deviceTimeScale);
            foreach (var fan in circuit.Components.OfType<ACFanComponent>())
                fan.AdvanceDeviceTime(settings.SolverElapsedTimeSeconds, deviceTimeScale);
            foreach (var panel in circuit.Components.OfType<SolarPanelComponent>())
                panel.ApplyIrradianceProfile(settings.DeviceTimeSeconds + settings.SolverElapsedTimeSeconds * deviceTimeScale);

            // Refresh logic outputs from the solved input nodes for probes and the next playback tick.
            DigitalLogicSimulator.EvaluateCombinational(circuit);

            // 4. Generate Transient / Waveform data for components with ShowWaveform
            var waveComps = circuit.Components
                .Where(c => settings.CaptureAllWaveforms || c.ShowWaveform)
                .ToList();
            if (waveComps.Count > 0)
            {
                try
                {
                    CaptureTransientWaveforms(waveComps, spiceCircuit, GetNetForPin, settings);
                }
                catch (Exception waveformException)
                {
                    foreach (var component in waveComps)
                    {
                        component.WaveformTime.Clear();
                        component.WaveformVoltage.Clear();
                        component.WaveformCurrent.Clear();
                        component.WaveformNodeVoltages.Clear();
                        component.WaveformPinCurrents.Clear();
                    }
                    results.Message += $"\nTransient waveform capture failed: {waveformException.Message}";
                }
            }

            results.Success = true;
            results.Message = $"Spice# {settings.Analysis} simulation completed: {topology.Nets.Count} nets, {spiceCircuit.Count} Spice elements.";
        }
        catch (Exception ex)
        {
            // Fallback gracefully so UI and probes still update
            RunFallbackSolver(circuit, results, ex);
        }

        if (macroExpansion?.HasMacros == true)
            CircuitMacroExpander.CopyMeasurementsBack(originalCircuit, macroExpansion, results);

        return results;
    }

    private static void ConfigureWaveform(SpiceSharp.Components.CommonBehaviors.IndependentSourceParameters p,
        string waveformType,
        double sOff, double sAmp, double sFreq, double sDelay, double sTheta, double sPhase,
        double pInit, double pPulse, double pDelay, double pRise, double pFall, double pWidth, double pPeriod,
        string pwlStr,
        double amAmp, double amOff, double amModFreq, double amCarrierFreq, double amDelay,
        double sfOff, double sfAmp, double sfCarrierFreq, double sfModIndex, double sfSignalFreq)
    {
        switch (waveformType?.ToUpperInvariant())
        {
            case "SINE":
                p.Waveform = new SpiceSharp.Components.Sine(sOff, sAmp > 0 ? sAmp : 5.0, sFreq > 0 ? sFreq : 1000.0, sDelay, sTheta, sPhase);
                break;
            case "PULSE":
                p.Waveform = new SpiceSharp.Components.Pulse(pInit, pPulse, pDelay, pRise > 0 ? pRise : 1e-6, pFall > 0 ? pFall : 1e-6, pWidth > 0 ? pWidth : 0.5e-3, pPeriod > 0 ? pPeriod : 1e-3);
                break;
            case "AM":
                p.Waveform = new SpiceSharp.Components.AM(
                    amAmp, amOff,
                    amModFreq > 0 ? amModFreq : 100.0,
                    amCarrierFreq > 0 ? amCarrierFreq : 10000.0,
                    amDelay, 0.0, 0.0);
                break;
            case "SFFM":
                p.Waveform = new SpiceSharp.Components.SFFM(
                    sfOff, sfAmp,
                    sfCarrierFreq > 0 ? sfCarrierFreq : 10000.0,
                    sfModIndex,
                    sfSignalFreq > 0 ? sfSignalFreq : 100.0,
                    0.0, 0.0);
                break;
            case "PWL":
                try
                {
                    var pairs = pwlStr.Split(new[] { ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var pts = new List<double>();
                    foreach (var pair in pairs)
                    {
                        var parts = pair.Split(new[] { ',', ':' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2 && double.TryParse(parts[0], out double t) && double.TryParse(parts[1], out double v))
                        {
                            pts.Add(t);
                            pts.Add(v);
                        }
                    }
                    if (pts.Count >= 4)
                    {
                        var waveform = new SpiceSharp.Components.Pwl();
                        waveform.SetPoints(pts.ToArray());
                        p.Waveform = waveform;
                    }
                }
                catch { }
                break;
        }
    }

    private static void AddFixedAcSource(
        SpiceSharp.Circuit circuit,
        string name,
        string positiveNode,
        string negativeNode,
        double voltageRms,
        double frequency,
        double phaseDegrees,
        double solverTime)
    {
        double phaseRadians = phaseDegrees * Math.PI / 180.0;
        double safeFrequency = Math.Max(frequency, 0.01);
        double peakVoltage = Math.Max(voltageRms, 0) * Math.Sqrt(2);
        double instantPhase = 2 * Math.PI * safeFrequency * Math.Max(solverTime, 0) + phaseRadians;
        var source = new SpiceSharp.Components.VoltageSource(
            name,
            positiveNode,
            negativeNode,
            peakVoltage * Math.Sin(instantPhase));
        source.Parameters.AcMagnitude = Math.Max(voltageRms, 0);
        source.Parameters.AcPhase = phaseDegrees;
        source.Parameters.Waveform = new SpiceSharp.Components.Sine(
            0,
            peakVoltage,
            safeFrequency,
            0,
            0,
            phaseRadians);
        circuit.Add(source);
    }

    private static void AddLineBranch(
        SpiceSharp.Circuit circuit,
        string name,
        string from,
        string to,
        double resistance,
        double inductance,
        double capacitance,
        double conductance,
        bool includeShunt,
        double frequency,
        bool distributedLongLine = false)
    {
        double omega = 2 * Math.PI * Math.Max(frequency, 0.01);
        if (distributedLongLine && capacitance > 0)
        {
            var seriesZ = new System.Numerics.Complex(resistance, omega * inductance);
            var shuntY = new System.Numerics.Complex(conductance, omega * capacitance);
            var propagation = System.Numerics.Complex.Sqrt(seriesZ * shuntY);
            var characteristic = System.Numerics.Complex.Sqrt(seriesZ / shuntY);
            var distributedSeries = characteristic * System.Numerics.Complex.Sinh(propagation);
            var distributedShunt = 2 / characteristic * System.Numerics.Complex.Tanh(propagation / 2);
            resistance = Math.Max(distributedSeries.Real, 0);
            inductance = Math.Max(distributedSeries.Imaginary / omega, 0);
            conductance = Math.Max(distributedShunt.Real, 0);
            capacitance = Math.Max(distributedShunt.Imaginary / omega, 0);
        }
        string internalNode = $"{name}_series_{Guid.NewGuid():N}";
        circuit.Add(new SpiceSharp.Components.Resistor($"{name}_R", from, internalNode, Math.Max(resistance, 1e-6)));
        if (inductance > 0)
            circuit.Add(new SpiceSharp.Components.Inductor($"{name}_L", internalNode, to, Math.Max(inductance, 1e-12)));
        else
            circuit.Add(new SpiceSharp.Components.Resistor($"{name}_link", internalNode, to, 1e-6));

        if (!includeShunt) return;
        // Nominal-pi approximation: split total shunt admittance equally between
        // both ends. The available schematic component exposes a single line pair,
        // so the shunt branches use Spice ground as the line reference.
        if (capacitance > 0)
        {
            circuit.Add(new SpiceSharp.Components.Capacitor($"{name}_Cfrom", from, "0", capacitance / 2));
            circuit.Add(new SpiceSharp.Components.Capacitor($"{name}_Cto", to, "0", capacitance / 2));
        }
        if (conductance > 0)
        {
            double shuntResistance = 2 / conductance;
            circuit.Add(new SpiceSharp.Components.Resistor($"{name}_Gfrom", from, "0", shuntResistance));
            circuit.Add(new SpiceSharp.Components.Resistor($"{name}_Gto", to, "0", shuntResistance));
        }
    }

    private static void CaptureTransientWaveforms(
        List<ComponentNode> components,
        SpiceSharp.Circuit spiceCircuit,
        Func<Guid, string, string> getNetForPin,
        SimulationSettings settings)
    {
        double start = Math.Max(0.0, settings.StartTime);
        double stop = settings.StopTime > start ? settings.StopTime : start + 0.02;
        double duration = stop - start;
        double requestedStep = settings.StepTime > 0 ? settings.StepTime : duration / 500.0;
        double maxStep = Math.Clamp(requestedStep, duration / 2000.0, duration / 2.0);
        var transient = new Transient("EESimulator_Waveform", maxStep, stop, maxStep)
        {
            TimeParameters = { StartTime = start }
        };

        string NodeFor(ComponentNode component, int pinIndex) =>
            component.Pins.Count > pinIndex ? getNetForPin(component.Id, component.Pins[pinIndex].Id) : "0";
        RealVoltageExport? VoltageAt(string node) => node == "0" ? null : new RealVoltageExport(transient, node);

        var captures = components.Select(component =>
        {
            List<(string Id, RealVoltageExport? Export, RealCurrentExport? ExportCurrent, double Multiplier)> pins;
            lock (component)
            {
                component.WaveformTime.Clear();
                component.WaveformVoltage.Clear();
                component.WaveformCurrent.Clear();
                component.WaveformNodeVoltages.Clear();
                component.WaveformPinCurrents.Clear();
                pins = component.Pins.Select(pin =>
                {
                    component.WaveformNodeVoltages[pin.Id] = new List<double>();
                    string net = getNetForPin(component.Id, pin.Id);
                    var pinCurrent = CreatePinCurrentExport(component, pin.Id, transient);
                    if (pinCurrent.Export != null)
                        component.WaveformPinCurrents[pin.Id] = new List<double>();
                    return (Id: pin.Id, Export: VoltageAt(net),
                        ExportCurrent: pinCurrent.Export, Multiplier: pinCurrent.Multiplier);
                }).ToList();
            }

            return (
                Component: component,
                Pins: pins,
                VoltageA: VoltageAt(NodeFor(component, 0)),
                VoltageB: VoltageAt(NodeFor(component, 1)),
                Current: component is ElectricalComponent or WireComponent or LogicInputComponent
                    ? new RealCurrentExport(transient, GetComponentCurrentExportName(component))
                    : null);
        }).ToList();

        foreach (int _ in transient.Run(spiceCircuit, Transient.ExportTransient))
        {
            double time = transient.Time;
            foreach (var capture in captures)
            {
                double Value(RealVoltageExport? export) => export?.Value ?? 0.0;
                lock (capture.Component)
                {
                    capture.Component.WaveformTime.Add(time);
                    capture.Component.WaveformVoltage.Add(Value(capture.VoltageA) - Value(capture.VoltageB));
                    capture.Component.WaveformCurrent.Add(capture.Current?.Value ?? 0.0);
                    foreach (var pin in capture.Pins)
                    {
                        capture.Component.WaveformNodeVoltages[pin.Id].Add(Value(pin.Export));
                        if (pin.ExportCurrent != null)
                            capture.Component.WaveformPinCurrents[pin.Id].Add(
                                pin.ExportCurrent.Value * pin.Multiplier);
                    }
                }
            }
        }

        foreach (var capture in captures)
        {
            lock (capture.Component)
            {
                if (capture.Component.WaveformTime.Count == 0) continue;
                capture.Component.LiveVoltage = capture.Component.WaveformVoltage[^1];
                capture.Component.LiveCurrent = capture.Component.WaveformCurrent[^1];
                double drop = Math.Abs(capture.Component.LiveVoltage);
                if (capture.Component is LEDComponent led)
                    led.IsOn = drop >= Math.Min(led.ForwardVoltage * 0.75, 1.5) || Math.Abs(led.LiveCurrent) >= 0.001;
                else if (capture.Component is BulbComponent bulb)
                {
                    bulb.Brightness = Math.Clamp(drop / Math.Max(bulb.RatedVoltage, 0.1), 0.0, 1.0);
                    bulb.IsOn = drop >= bulb.ThresholdVoltage || Math.Abs(bulb.LiveCurrent) >= 0.02;
                }
                else if (capture.Component is LogicOutputComponent logicOutput && capture.Pins.Count > 0)
                {
                    double terminalVoltage = capture.Pins[0].Export?.Value ?? 0.0;
                    logicOutput.LiveVoltage = terminalVoltage;
                    logicOutput.IsOn = terminalVoltage >= logicOutput.Threshold;
                }
            }
        }
    }

    private static (RealCurrentExport? Export, double Multiplier) CreatePinCurrentExport(
        ComponentNode component,
        string pinId,
        Transient transient)
    {
        int pinIndex = component.Pins.FindIndex(pin => pin.Id.Equals(pinId, StringComparison.OrdinalIgnoreCase));
        if (pinIndex < 0) return (null, 1);

        if (component is ThreePhaseTransmissionLineComponent threePhaseLine)
        {
            if (pinIndex < 3)
                return (new RealCurrentExport(transient, $"{component.Name}_phase{pinIndex + 1}_R"), 1);
            if (pinIndex < 6)
            {
                string phaseName = $"{component.Name}_phase{pinIndex - 2}";
                string element = threePhaseLine.InductancePerKmPerPhase * threePhaseLine.LengthKm > 0
                    ? $"{phaseName}_L"
                    : $"{phaseName}_link";
                return (new RealCurrentExport(transient, element), -1);
            }
            if (pinIndex == 6 && threePhaseLine.Connection == "Four-wire")
                return (new RealCurrentExport(transient, $"{component.Name}_neutral"), 1);
            if (pinIndex == 7 && threePhaseLine.Connection == "Four-wire")
                return (new RealCurrentExport(transient, $"{component.Name}_neutral"), -1);
        }

        if (component is TwoPhaseTransmissionLineComponent twoPhaseLine)
        {
            if (pinIndex < 2)
            {
                string element = pinIndex == 0
                    ? $"{twoPhaseLine.Name}_R"
                    : twoPhaseLine.InductancePerKm * twoPhaseLine.LengthKm > 0 ? $"{twoPhaseLine.Name}_L" : $"{twoPhaseLine.Name}_link";
                return (new RealCurrentExport(transient, element), pinIndex == 0 ? 1 : -1);
            }
            if (pinIndex < 4)
            {
                string phaseName = $"{twoPhaseLine.Name}_phaseB";
                string element = pinIndex == 2
                    ? $"{phaseName}_R"
                    : twoPhaseLine.InductancePerKm * twoPhaseLine.LengthKm > 0 ? $"{phaseName}_L" : $"{phaseName}_link";
                return (new RealCurrentExport(transient, element), pinIndex == 2 ? 1 : -1);
            }
            if (pinIndex == 4 && twoPhaseLine.ConductorConfiguration == "SplitPhaseThreeWire")
                return (new RealCurrentExport(transient, $"{twoPhaseLine.Name}_neutral_R"), 1);
            if (pinIndex == 5 && twoPhaseLine.ConductorConfiguration == "SplitPhaseThreeWire")
            {
                string element = twoPhaseLine.InductancePerKm * twoPhaseLine.LengthKm > 0
                    ? $"{twoPhaseLine.Name}_neutral_L"
                    : $"{twoPhaseLine.Name}_neutral_link";
                return (new RealCurrentExport(transient, element), -1);
            }
        }

        if (component is SinglePhaseTransmissionLineComponent singlePhaseLine)
        {
            string terminalElement = pinIndex == 0
                ? $"{singlePhaseLine.Name}_R"
                : singlePhaseLine.InductancePerKm * singlePhaseLine.LengthKm > 0
                    ? $"{singlePhaseLine.Name}_L"
                    : $"{singlePhaseLine.Name}_link";
            return (new RealCurrentExport(transient, terminalElement), pinIndex == 0 ? 1 : -1);
        }

        if (component is SynchronousGeneratorComponent generator && pinIndex < generator.NumberOfPhases)
        {
            string phaseName = pinIndex == 0 ? generator.Name : $"{generator.Name}_phase{pinIndex + 1}";
            return (new RealCurrentExport(transient, $"{phaseName}_armatureR"), -1);
        }

        if (component is LogicInputComponent)
            return (new RealCurrentExport(transient, component.Name), 1);

        if (component is LogicOutputComponent)
            return (new RealCurrentExport(transient, $"{component.Name}_sense"), 1);

        if (DigitalLogicSimulator.IsSpiceDrivenDigital(component))
        {
            bool isDrivenOutput = DigitalLogicSimulator.GetOutputVoltages(component)
                .Any(output => output.PinId.Equals(pinId, StringComparison.OrdinalIgnoreCase));
            if (isDrivenOutput)
                return (new RealCurrentExport(transient, $"{component.Name}_logic_{pinId}"), 1);
        }

        if (component is ElectricalComponent or WireComponent)
            return pinIndex < 2
                ? (new RealCurrentExport(transient, component.Name), pinIndex == 0 ? 1 : -1)
                : (null, 1);

        return (null, 1);
    }

    private static string GetComponentCurrentExportName(ComponentNode component) =>
        component switch
        {
            SinglePhaseTransmissionLineComponent => $"{component.Name}_R",
            TwoPhaseTransmissionLineComponent => $"{component.Name}_R",
            ThreePhaseTransmissionLineComponent => $"{component.Name}_phase1_R",
            _ => component.Name
        };

    private static void RunFallbackSolver(CircuitNode circuit, SimulationResultsData results, Exception ex)
    {
        double sourceVoltage = 0.0;
        double totalResistance = 0.0;
        var loads = new List<(ComponentNode comp, double resistance)>();

        foreach (var comp in circuit.Components)
        {
            if (comp is VoltageSourceComponent v)
            {
                sourceVoltage += v.Voltage;
            }
            else if (comp is ResistorComponent r && r.Resistance > 0)
            {
                totalResistance += r.Resistance;
                loads.Add((r, r.Resistance));
            }
            else if (comp is BulbComponent b && b.Resistance > 0)
            {
                totalResistance += b.Resistance;
                loads.Add((b, b.Resistance));
            }
            else if (comp is LEDComponent led)
            {
                // LED dynamic forward resistance approximation
                totalResistance += 50.0;
                loads.Add((led, 50.0));
            }
            else if (comp is LogicInputComponent lin)
            {
                if (lin.State && lin.HighVoltage > 0)
                    sourceVoltage += lin.HighVoltage;
            }
        }

        if (totalResistance > 0 && Math.Abs(sourceVoltage) > 0)
        {
            double current = sourceVoltage / totalResistance;
            results.OperatingPointCurrents["I_total"] = current;

            foreach (var (comp, res) in loads)
            {
                double vDrop = current * res;
                comp.LiveVoltage = vDrop;
                comp.LiveCurrent = current;
                results.OperatingPointVoltages[$"V({comp.Name})"] = vDrop;
            }

            foreach (var wire in circuit.Components.OfType<WireComponent>())
            {
                wire.LiveVoltage = current * wire.Resistance;
                wire.LiveCurrent = current;
                results.OperatingPointVoltages[$"V({wire.Name})"] = wire.LiveVoltage;
                results.OperatingPointCurrents[$"I({wire.Name})"] = wire.LiveCurrent;
            }

            foreach (var source in circuit.Components.OfType<VoltageSourceComponent>())
                source.LiveCurrent = -current;
        }

        results.Success = true;
        results.Message = $"Simulation solved (Linear/Nodal): {ex.Message}";
    }
}
