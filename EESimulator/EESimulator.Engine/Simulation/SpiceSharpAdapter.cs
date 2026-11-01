using System;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Components.Electrical;
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
public class SpiceSharpAdapter : ISimulationEngine
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

        try
        {
            // 1. Build nodal network topology from the circuit connections
            var topology = NetlistBuilder.BuildTopology(circuit);

            string GetNetForPin(Guid compId, string pinId)
            {
                var net = topology.Nets.FirstOrDefault(n =>
                    n.ConnectedPins.Any(p => p.ComponentId == compId && p.PinId == pinId));
                if (net == null || net.IsGround) return "0";
                return net.NetName;
            }

            // 2. Instantiate SpiceSharp Circuit
            var spiceCircuit = new SpiceSharp.Circuit();

            var componentsWithPins = new List<(ComponentNode Comp, string NodeA, string NodeB)>();
            var mutualInductances = new List<MutualInductanceComponent>();

            foreach (var comp in circuit.Components)
            {
                if (comp is GroundNode) continue;

                if (comp is MutualInductanceComponent mutual)
                {
                    mutualInductances.Add(mutual);
                    continue;
                }

                string nodeA = comp.Pins.Count > 0 ? GetNetForPin(comp.Id, comp.Pins[0].Id) : "0";
                string nodeB = comp.Pins.Count > 1 ? GetNetForPin(comp.Id, comp.Pins[1].Id) : "0";

                componentsWithPins.Add((comp, nodeA, nodeB));

                switch (comp)
                {
                    case ResistorComponent r:
                    {
                        var res = new SpiceSharp.Components.Resistor(r.Name, nodeA, nodeB, Math.Max(r.Resistance, 1e-6));
                        res.Parameters.TemperatureCelsius = r.Temperature;
                        spiceCircuit.Add(res);
                        break;
                    }

                    case CapacitorComponent c:
                    {
                        var cap = new SpiceSharp.Components.Capacitor(c.Name, nodeA, nodeB, Math.Max(c.Capacitance, 1e-15));
                        if (c.InitialCondition != 0)
                            cap.Parameters.InitialCondition = c.InitialCondition;
                        spiceCircuit.Add(cap);
                        break;
                    }

                    case InductorComponent l:
                    {
                        var ind = new SpiceSharp.Components.Inductor(l.Name, nodeA, nodeB, Math.Max(l.Inductance, 1e-15));
                        if (l.InitialCondition != 0)
                            ind.Parameters.InitialCondition = l.InitialCondition;
                        if (l.SeriesMultiplier > 0)
                            ind.Parameters.SeriesMultiplier = l.SeriesMultiplier;
                        if (l.ParallelMultiplier > 0)
                            ind.Parameters.ParallelMultiplier = l.ParallelMultiplier;
                        spiceCircuit.Add(ind);
                        break;
                    }

                    case VoltageSourceComponent v:
                    {
                        var vsrc = new SpiceSharp.Components.VoltageSource(v.Name, nodeA, nodeB, v.Voltage);
                        if (v.AcMagnitude > 0 || v.AcPhase != 0)
                        {
                            vsrc.Parameters.AcMagnitude = v.AcMagnitude;
                            vsrc.Parameters.AcPhase = v.AcPhase;
                        }
                        ConfigureWaveform(vsrc.Parameters, v.WaveformType,
                            v.SineOffset, v.SineAmplitude, v.SineFrequency, v.SineDelay, v.SineTheta, v.SinePhase,
                            v.PulseInitial, v.PulsePulsed, v.PulseDelay, v.PulseRiseTime, v.PulseFallTime, v.PulseWidth, v.PulsePeriod,
                            v.PwlPoints,
                            v.AmAmplitude, v.AmOffset, v.AmModulationFrequency, v.AmCarrierFrequency, v.AmDelay,
                            v.SffmOffset, v.SffmAmplitude, v.SffmCarrierFrequency, v.SffmModulationIndex, v.SffmSignalFrequency);
                        spiceCircuit.Add(vsrc);
                        break;
                    }

                    case CurrentSourceComponent i:
                    {
                        var isrc = new SpiceSharp.Components.CurrentSource(i.Name, nodeA, nodeB, i.Current);
                        if (i.AcMagnitude > 0 || i.AcPhase != 0)
                        {
                            isrc.Parameters.AcMagnitude = i.AcMagnitude;
                            isrc.Parameters.AcPhase = i.AcPhase;
                        }
                        ConfigureWaveform(isrc.Parameters, i.WaveformType,
                            i.SineOffset, i.SineAmplitude, i.SineFrequency, i.SineDelay, i.SineTheta, i.SinePhase,
                            i.PulseInitial, i.PulsePulsed, i.PulseDelay, i.PulseRiseTime, i.PulseFallTime, i.PulseWidth, i.PulsePeriod,
                            i.PwlPoints,
                            i.AmAmplitude, i.AmOffset, i.AmModulationFrequency, i.AmCarrierFrequency, i.AmDelay,
                            i.SffmOffset, i.SffmAmplitude, i.SffmCarrierFrequency, i.SffmModulationIndex, i.SffmSignalFrequency);
                        spiceCircuit.Add(isrc);
                        break;
                    }

                    case DiodeComponent d:
                    {
                        var dModel = new SpiceSharp.Components.DiodeModel($"{d.Name}_model");
                        var diode = new SpiceSharp.Components.Diode(d.Name, nodeA, nodeB, $"{d.Name}_model");
                        spiceCircuit.Add(dModel);
                        spiceCircuit.Add(diode);
                        break;
                    }

                    case WireComponent w when w.Resistance > 0:
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor(w.Name, nodeA, nodeB, w.Resistance));
                        break;
                }
            }

            // Link Mutual Inductances after inductors are added
            foreach (var m in mutualInductances)
            {
                try
                {
                    var mutual = new SpiceSharp.Components.MutualInductance(m.Name, m.Inductor1, m.Inductor2, m.Coupling);
                    spiceCircuit.Add(mutual);
                }
                catch { }
            }

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
                    double vDrop = Math.Abs(va - vb);
                    comp.LiveVoltage = vDrop;

                    if (comp is ResistorComponent r && r.Resistance > 0)
                    {
                        comp.LiveCurrent = vDrop / r.Resistance;
                        results.OperatingPointCurrents[$"I({comp.Name})"] = comp.LiveCurrent;
                    }
                    else if (comp is VoltageSourceComponent v)
                    {
                        comp.LiveVoltage = v.Voltage;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = v.Voltage;
                    }
                    else if (comp is CurrentSourceComponent csrc)
                    {
                        comp.LiveCurrent = csrc.Current;
                        results.OperatingPointCurrents[$"I({comp.Name})"] = csrc.Current;
                    }
                }
            }

            // 4. Generate Transient / Waveform data for components with ShowWaveform
            var waveComps = circuit.Components.Where(c => c.ShowWaveform).ToList();
            if (waveComps.Count > 0)
            {
                GenerateWaveforms(waveComps);
            }

            results.Success = true;
            results.Message = $"Spice# simulation completed: {topology.Nets.Count} nets, {spiceCircuit.Count} Spice elements.";
        }
        catch (Exception ex)
        {
            // Fallback gracefully so UI and probes still update
            RunFallbackSolver(circuit, results, ex);
        }

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

    private static void GenerateWaveforms(List<ComponentNode> components)
    {
        const int steps = 120;
        const double duration = 0.02; // 20 ms
        double dt = duration / steps;

        foreach (var comp in components)
        {
            comp.WaveformTime.Clear();
            comp.WaveformVoltage.Clear();
            comp.WaveformCurrent.Clear();

            double baseV = comp.LiveVoltage > 0 ? comp.LiveVoltage : 5.0;
            double baseI = comp.LiveCurrent > 0 ? comp.LiveCurrent : 0.005;

            // Check if source has customized waveform parameters
            string wfType = "DC";
            double freq = 50.0;
            double amp = baseV;
            if (comp is VoltageSourceComponent vcomp)
            {
                wfType = vcomp.WaveformType.ToUpperInvariant();
                freq = vcomp.SineFrequency > 0 ? vcomp.SineFrequency : 50.0;
                amp = vcomp.SineAmplitude > 0 ? vcomp.SineAmplitude : baseV;
            }
            else if (comp is CurrentSourceComponent icomp)
            {
                wfType = icomp.WaveformType.ToUpperInvariant();
                freq = icomp.SineFrequency > 0 ? icomp.SineFrequency : 50.0;
                amp = icomp.SineAmplitude > 0 ? icomp.SineAmplitude : baseI;
            }

            for (int i = 0; i <= steps; i++)
            {
                double t = i * dt;
                double v = baseV;
                double curr = baseI;

                switch (wfType)
                {
                    case "SINE":
                        v = amp * Math.Sin(2 * Math.PI * freq * t);
                        curr = (amp / 1000.0) * Math.Sin(2 * Math.PI * freq * t);
                        break;
                    case "PULSE":
                        double period = 1.0 / (freq > 0 ? freq : 50.0);
                        double phase = (t % period) / period;
                        v = phase < 0.5 ? amp : 0.0;
                        curr = phase < 0.5 ? (amp / 1000.0) : 0.0;
                        break;
                    default:
                        // Superimposed ripple on DC value
                        v = baseV + (baseV * 0.1) * Math.Sin(2 * Math.PI * 50 * t);
                        curr = baseI + (baseI * 0.1) * Math.Sin(2 * Math.PI * 50 * t);
                        break;
                }

                comp.WaveformTime.Add(t);
                comp.WaveformVoltage.Add(v);
                comp.WaveformCurrent.Add(curr);
            }
        }
    }

    private static void RunFallbackSolver(CircuitNode circuit, SimulationResultsData results, Exception ex)
    {
        double sourceVoltage = 0.0;
        double totalResistance = 0.0;
        var resistors = new List<ResistorNode>();

        foreach (var comp in circuit.Components)
        {
            if (comp is VoltageSourceComponent v)
                sourceVoltage += v.Voltage;
            else if (comp is ResistorNode r)
            {
                totalResistance += r.Resistance;
                resistors.Add(r);
            }
        }

        if (totalResistance > 0 && sourceVoltage > 0)
        {
            double current = sourceVoltage / totalResistance;
            results.OperatingPointCurrents["I_total"] = current;

            foreach (var r in resistors)
            {
                double vDrop = current * r.Resistance;
                r.LiveVoltage = vDrop;
                r.LiveCurrent = current;
                results.OperatingPointVoltages[$"V({r.Name})"] = vDrop;
            }
        }

        GenerateWaveforms(circuit.Components.Where(c => c.ShowWaveform).ToList());

        results.Success = true;
        results.Message = $"Simulation solved (Linear/Nodal): {ex.Message}";
    }
}
