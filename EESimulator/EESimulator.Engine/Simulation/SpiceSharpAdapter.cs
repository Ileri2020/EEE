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
                        double temperatureDelta = r.Temperature - 27.0;
                        double resistanceScale = 1.0 + r.TemperatureCoefficient1 * temperatureDelta +
                            r.TemperatureCoefficient2 * temperatureDelta * temperatureDelta;
                        double effectiveResistance = Math.Max(r.Resistance * Math.Max(resistanceScale, 1e-6), 1e-6);
                        var res = new SpiceSharp.Components.Resistor(r.Name, nodeA, nodeB, effectiveResistance);
                        res.Parameters.TemperatureCelsius = r.Temperature;
                        spiceCircuit.Add(res);
                        break;
                    }

                    case CapacitorComponent c:
                    {
                        string capNodeA = nodeA;
                        if (c.EquivalentSeriesResistance > 0)
                        {
                            capNodeA = $"{c.Name}_esr_internal";
                            spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{c.Name}_esr", nodeA, capNodeA, c.EquivalentSeriesResistance));
                        }

                        double temperatureDelta = c.Temperature - 27.0;
                        double capacitanceScale = 1.0 + c.TemperatureCoefficient1 * temperatureDelta +
                            c.TemperatureCoefficient2 * temperatureDelta * temperatureDelta;
                        double effectiveCapacitance = Math.Max(c.Capacitance * Math.Max(capacitanceScale, 1e-12), 1e-15);
                        var cap = new SpiceSharp.Components.Capacitor(c.Name, capNodeA, nodeB, effectiveCapacitance);
                        if (c.InitialCondition != 0)
                            cap.Parameters.InitialCondition = c.InitialCondition;
                        spiceCircuit.Add(cap);
                        break;
                    }

                    case InductorComponent l:
                    {
                        string indNodeA = nodeA;
                        if (l.DcResistance > 0)
                        {
                            indNodeA = $"{l.Name}_dcr_internal";
                            spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{l.Name}_dcr", nodeA, indNodeA, l.DcResistance));
                        }

                        var ind = new SpiceSharp.Components.Inductor(l.Name, indNodeA, nodeB, Math.Max(l.Inductance, 1e-15));
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
                        dModel.Parameters.SaturationCurrent = Math.Max(d.SaturationCurrent, 1e-25);
                        if (d.EmissionCoefficient > 0) dModel.Parameters.EmissionCoefficient = d.EmissionCoefficient;
                        if (d.BreakdownVoltage > 0) dModel.Parameters.BreakdownVoltage = d.BreakdownVoltage;
                        if (d.SeriesResistance > 0) dModel.Parameters.Resistance = d.SeriesResistance;
                        if (d.JunctionCapacitance > 0) dModel.Parameters.JunctionCap = d.JunctionCapacitance;
                        if (d.TransitTime > 0) dModel.Parameters.TransitTime = d.TransitTime;
                        if (d.BreakdownCurrent > 0) dModel.Parameters.BreakdownCurrent = d.BreakdownCurrent;

                        string anode = GetPinNode(d, "anode", 0);
                        string cathode = GetPinNode(d, "cathode", 1);
                        var diode = new SpiceSharp.Components.Diode(d.Name, anode, cathode, $"{d.Name}_model");
                        spiceCircuit.Add(dModel);
                        spiceCircuit.Add(diode);
                        break;
                    }

                    case BulbComponent bulb:
                    {
                        var res = new SpiceSharp.Components.Resistor(bulb.Name, nodeA, nodeB, Math.Max(bulb.Resistance, 1e-6));
                        spiceCircuit.Add(res);
                        break;
                    }

                    case LEDComponent led:
                    {
                        var dModel = new SpiceSharp.Components.DiodeModel($"{led.Name}_model");
                        // Calculate realistic saturation current based on rated ForwardVoltage: Is = 0.02 * exp(-Vf / (n * Vt))
                        double vt = 0.02585; // thermal voltage at 300K
                        double n = 2.0;
                        double isat = Math.Max(0.02 * Math.Exp(-led.ForwardVoltage / (n * vt)), 1e-22);
                        dModel.Parameters.SaturationCurrent = isat;
                        dModel.Parameters.EmissionCoefficient = n;
                        string anode = GetPinNode(led, "anode", 0);
                        string cathode = GetPinNode(led, "cathode", 1);
                        var diode = new SpiceSharp.Components.Diode(led.Name, anode, cathode, $"{led.Name}_model");
                        spiceCircuit.Add(dModel);
                        spiceCircuit.Add(diode);
                        break;
                    }

                    case LogicInputComponent lin:
                    {
                        double vLevel = lin.State ? lin.HighVoltage : lin.LowVoltage;
                        string outNode = GetPinNode(lin, "out", 0);
                        var vsrc = new SpiceSharp.Components.VoltageSource(lin.Name, outNode, "0", vLevel);
                        spiceCircuit.Add(vsrc);
                        break;
                    }

                    case LogicOutputComponent lout:
                    {
                        string inNode = GetPinNode(lout, "in", 0);
                        var senseRes = new SpiceSharp.Components.Resistor($"{lout.Name}_sense", inNode, "0", 10e6);
                        spiceCircuit.Add(senseRes);
                        break;
                    }

                    case BJTComponent b:
                    {
                        var bModel = new SpiceSharp.Components.BipolarJunctionTransistorModel($"{b.Name}_model");
                        if (b.Polarity == BjtPolarity.PNP) bModel.Parameters.SetPnp(true);
                        else bModel.Parameters.SetNpn(true);

                        if (b.GainBeta > 0) bModel.Parameters.BetaF = b.GainBeta;
                        if (b.ReverseGainBeta > 0) bModel.Parameters.BetaR = b.ReverseGainBeta;
                        if (b.SaturationCurrent > 0) bModel.Parameters.SatCur = b.SaturationCurrent;
                        if (b.EarlyVoltage > 0) bModel.Parameters.EarlyVoltageForward = b.EarlyVoltage;

                        string cNode = GetPinNode(b, "collector", 1);
                        string bNode = GetPinNode(b, "base", 0);
                        string eNode = GetPinNode(b, "emitter", 2);

                        var bjt = new SpiceSharp.Components.BipolarJunctionTransistor(b.Name, cNode, bNode, eNode, eNode, $"{b.Name}_model");
                        spiceCircuit.Add(bModel);
                        spiceCircuit.Add(bjt);
                        break;
                    }

                    case JFETComponent j:
                    {
                        var jModel = new SpiceSharp.Components.JFETModel($"{j.Name}_model");
                        if (j.Channel == JfetChannel.PChannel) jModel.Parameters.SetPjf(true);
                        else jModel.Parameters.SetNjf(true);

                        jModel.Parameters.Threshold = j.ThresholdVoltage;
                        if (j.Transconductance > 0) jModel.Parameters.Beta = j.Transconductance;
                        if (j.Lambda > 0) jModel.Parameters.LModulation = j.Lambda;
                        if (j.DrainResistance > 0) jModel.Parameters.DrainResistance = j.DrainResistance;
                        if (j.SourceResistance > 0) jModel.Parameters.SourceResistance = j.SourceResistance;
                        if (j.CapGs > 0) jModel.Parameters.CapGs = j.CapGs;
                        if (j.CapGd > 0) jModel.Parameters.CapGd = j.CapGd;

                        string dNode = GetPinNode(j, "drain", 1);
                        string gNode = GetPinNode(j, "gate", 0);
                        string sNode = GetPinNode(j, "source", 2);

                        var jfet = new SpiceSharp.Components.JFET(j.Name, dNode, gNode, sNode, $"{j.Name}_model");
                        spiceCircuit.Add(jModel);
                        spiceCircuit.Add(jfet);
                        break;
                    }

                    case MOSFETComponent m:
                    {
                        var mModel = new SpiceSharp.Components.Mosfet1Model($"{m.Name}_model");
                        if (m.Channel == MosfetChannel.PChannel) mModel.Parameters.SetPmos(true);
                        else mModel.Parameters.SetNmos(true);

                        mModel.Parameters.Vt0 = m.ThresholdVoltage;
                        if (m.Transconductance > 0) mModel.Parameters.Transconductance = m.Transconductance;
                        if (m.Lambda > 0) mModel.Parameters.Lambda = m.Lambda;
                        if (m.ChannelWidth > 0) mModel.Parameters.Width = m.ChannelWidth;
                        if (m.ChannelLength > 0) mModel.Parameters.Length = m.ChannelLength;

                        string dNode = GetPinNode(m, "drain", 1);
                        string gNode = GetPinNode(m, "gate", 0);
                        string sNode = GetPinNode(m, "source", 2);
                        string bNode = GetPinNode(m, "body", 3);
                        if (bNode == "0" && sNode != "0")
                        {
                            // If bulk/body pin is not wired externally, tie to source
                            bNode = sNode;
                        }

                        var mosfet = new SpiceSharp.Components.Mosfet1(m.Name, dNode, gNode, sNode, bNode, $"{m.Name}_model");
                        spiceCircuit.Add(mModel);
                        spiceCircuit.Add(mosfet);
                        break;
                    }

                    case VCVSComponent vcvs:
                    {
                        string outPos = GetPinNode(vcvs, "out_pos", 0);
                        string outNeg = GetPinNode(vcvs, "out_neg", 1);
                        string ctrlPos = GetPinNode(vcvs, "ctrl_pos", 2);
                        string ctrlNeg = GetPinNode(vcvs, "ctrl_neg", 3);
                        var vsrc = new SpiceSharp.Components.VoltageControlledVoltageSource(vcvs.Name, outPos, outNeg, ctrlPos, ctrlNeg, vcvs.VoltageGain);
                        spiceCircuit.Add(vsrc);
                        break;
                    }

                    case VCCSComponent vccs:
                    {
                        string outPos = GetPinNode(vccs, "out_pos", 0);
                        string outNeg = GetPinNode(vccs, "out_neg", 1);
                        string ctrlPos = GetPinNode(vccs, "ctrl_pos", 2);
                        string ctrlNeg = GetPinNode(vccs, "ctrl_neg", 3);
                        var isrc = new SpiceSharp.Components.VoltageControlledCurrentSource(vccs.Name, outPos, outNeg, ctrlPos, ctrlNeg, vccs.Transconductance);
                        spiceCircuit.Add(isrc);
                        break;
                    }

                    case CCVSComponent ccvs:
                    {
                        string outPos = GetPinNode(ccvs, "out_pos", 0);
                        string outNeg = GetPinNode(ccvs, "out_neg", 1);
                        string ctrlSource = ccvs.ControllingSource;
                        string ctrlPos = GetPinNode(ccvs, "ctrl_pos", 2);
                        string ctrlNeg = GetPinNode(ccvs, "ctrl_neg", 3);
                        if (string.IsNullOrEmpty(ctrlSource) || (ctrlPos != "0" || ctrlNeg != "0"))
                        {
                            ctrlSource = $"V_sense_{ccvs.Name}_{ccvs.Id.ToString("N")[..6]}";
                            spiceCircuit.Add(new SpiceSharp.Components.VoltageSource(ctrlSource, ctrlPos, ctrlNeg, 0.0));
                        }
                        var hsrc = new SpiceSharp.Components.CurrentControlledVoltageSource(ccvs.Name, outPos, outNeg, ctrlSource, ccvs.Transresistance);
                        spiceCircuit.Add(hsrc);
                        break;
                    }

                    case CCCSComponent cccs:
                    {
                        string outPos = GetPinNode(cccs, "out_pos", 0);
                        string outNeg = GetPinNode(cccs, "out_neg", 1);
                        string ctrlSource = cccs.ControllingSource;
                        string ctrlPos = GetPinNode(cccs, "ctrl_pos", 2);
                        string ctrlNeg = GetPinNode(cccs, "ctrl_neg", 3);
                        if (string.IsNullOrEmpty(ctrlSource) || (ctrlPos != "0" || ctrlNeg != "0"))
                        {
                            ctrlSource = $"V_sense_{cccs.Name}_{cccs.Id.ToString("N")[..6]}";
                            spiceCircuit.Add(new SpiceSharp.Components.VoltageSource(ctrlSource, ctrlPos, ctrlNeg, 0.0));
                        }
                        var fsrc = new SpiceSharp.Components.CurrentControlledCurrentSource(cccs.Name, outPos, outNeg, ctrlSource, cccs.CurrentGain);
                        spiceCircuit.Add(fsrc);
                        break;
                    }

                    case VoltageSwitchComponent vsw:
                    {
                        var swModel = new SpiceSharp.Components.VoltageSwitchModel($"{vsw.Name}_model");
                        swModel.SetParameter("ron", Math.Max(vsw.ResistanceOn, 1e-9));
                        swModel.SetParameter("roff", Math.Max(vsw.ResistanceOff, 1e-3));
                        swModel.Parameters.Threshold = vsw.Threshold;
                        swModel.Parameters.Hysteresis = vsw.Hysteresis;

                        string swPos = GetPinNode(vsw, "sw_pos", 0);
                        string swNeg = GetPinNode(vsw, "sw_neg", 1);
                        string ctrlPos = GetPinNode(vsw, "ctrl_pos", 2);
                        string ctrlNeg = GetPinNode(vsw, "ctrl_neg", 3);

                        var sw = new SpiceSharp.Components.VoltageSwitch(vsw.Name, swPos, swNeg, ctrlPos, ctrlNeg, $"{vsw.Name}_model");
                        spiceCircuit.Add(swModel);
                        spiceCircuit.Add(sw);
                        break;
                    }

                    case CurrentSwitchComponent csw:
                    {
                        var swModel = new SpiceSharp.Components.CurrentSwitchModel($"{csw.Name}_model");
                        swModel.SetParameter("ron", Math.Max(csw.ResistanceOn, 1e-9));
                        swModel.SetParameter("roff", Math.Max(csw.ResistanceOff, 1e-3));
                        swModel.Parameters.Threshold = csw.Threshold;
                        swModel.Parameters.Hysteresis = csw.Hysteresis;

                        string swPos = GetPinNode(csw, "sw_pos", 0);
                        string swNeg = GetPinNode(csw, "sw_neg", 1);
                        string ctrlSource = csw.ControllingSource;
                        string ctrlPos = GetPinNode(csw, "ctrl_pos", 2);
                        string ctrlNeg = GetPinNode(csw, "ctrl_neg", 3);
                        if (string.IsNullOrEmpty(ctrlSource) || (ctrlPos != "0" || ctrlNeg != "0"))
                        {
                            ctrlSource = $"V_sense_{csw.Name}_{csw.Id.ToString("N")[..6]}";
                            spiceCircuit.Add(new SpiceSharp.Components.VoltageSource(ctrlSource, ctrlPos, ctrlNeg, 0.0));
                        }

                        var sw = new SpiceSharp.Components.CurrentSwitch(csw.Name, swPos, swNeg, ctrlSource);
                        sw.Model = $"{csw.Name}_model";
                        spiceCircuit.Add(swModel);
                        spiceCircuit.Add(sw);
                        break;
                    }

                    case TransmissionLineComponent tline:
                    {
                        string p1Pos = GetPinNode(tline, "pos1", 0);
                        string p1Neg = GetPinNode(tline, "neg1", 1);
                        string p2Pos = GetPinNode(tline, "pos2", 2);
                        string p2Neg = GetPinNode(tline, "neg2", 3);
                        double z0 = Math.Max(tline.CharacteristicImpedance, 1e-3);
                        double td = Math.Max(tline.Delay, 1e-15);

                        var t = new SpiceSharp.Components.LosslessTransmissionLine(tline.Name, p1Pos, p1Neg, p2Pos, p2Neg, z0, td);
                        if (tline.Frequency > 0) t.Parameters.Frequency = tline.Frequency;
                        if (tline.NormalizedLength > 0) t.Parameters.NormalizedLength = tline.NormalizedLength;
                        spiceCircuit.Add(t);
                        break;
                    }

                    case VoltageDelayComponent vdelay:
                    {
                        string outPos = GetPinNode(vdelay, "out_pos", 0);
                        string outNeg = GetPinNode(vdelay, "out_neg", 1);
                        string inPos = GetPinNode(vdelay, "in_pos", 2);
                        string inNeg = GetPinNode(vdelay, "in_neg", 3);
                        double dly = Math.Max(vdelay.Delay, 1e-15);

                        var vd = new SpiceSharp.Components.VoltageDelay(vdelay.Name, outPos, outNeg, inPos, inNeg, dly);
                        spiceCircuit.Add(vd);
                        break;
                    }

                    case WireComponent w when w.Resistance > 0:
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor(w.Name, nodeA, nodeB, w.Resistance));
                        break;

                    case ClockComponent clk:
                    {
                        string outNode = GetPinNode(clk, "out", 0);
                        var clkSrc = new SpiceSharp.Components.VoltageSource(clk.Name, outNode, "0", clk.HighVoltage);
                        double period = 1.0 / Math.Max(clk.Frequency, 0.01);
                        double width = period * Math.Clamp(clk.DutyCycle, 0.01, 0.99);
                        clkSrc.Parameters.Waveform = new SpiceSharp.Components.Pulse(
                            clk.LowVoltage, clk.HighVoltage, 0.0, 1e-9, 1e-9, width, period);
                        spiceCircuit.Add(clkSrc);
                        break;
                    }

                    case PinComponent pin:
                    {
                        string pinNode = GetPinNode(pin, "pin", 0);
                        if (pin.Direction == PinDirection.Input)
                        {
                            double vLevel = pin.Value > 0 ? pin.HighVoltage : pin.LowVoltage;
                            var vsrc = new SpiceSharp.Components.VoltageSource(pin.Name, pinNode, "0", vLevel);
                            spiceCircuit.Add(vsrc);
                        }
                        else
                        {
                            var sense = new SpiceSharp.Components.Resistor($"{pin.Name}_sense", pinNode, "0", 10e6);
                            spiceCircuit.Add(sense);
                        }
                        break;
                    }

                    case PowerComponent vcc:
                    {
                        string pNode = GetPinNode(vcc, "vcc", 0);
                        var vsrc = new SpiceSharp.Components.VoltageSource(vcc.Name, pNode, "0", vcc.Voltage);
                        spiceCircuit.Add(vsrc);
                        break;
                    }

                    case PullResistorComponent pr:
                    {
                        string targetNode = GetPinNode(pr, "pin", 0);
                        string customRailNode = GetPinNode(pr, "rail", 1);
                        double resVal = Math.Max(pr.Resistance, 1.0);

                        if (pr.Mode == PullResistorMode.PullDown)
                        {
                            string gndNode = customRailNode != "0" ? customRailNode : "0";
                            spiceCircuit.Add(new SpiceSharp.Components.Resistor(pr.Name, targetNode, gndNode, resVal));
                        }
                        else if (pr.Mode == PullResistorMode.PullUp)
                        {
                            string railNode = customRailNode;
                            if (railNode == "0")
                            {
                                railNode = $"V_rail_{pr.Name}";
                                spiceCircuit.Add(new SpiceSharp.Components.VoltageSource($"{pr.Name}_vrail", railNode, "0", pr.RailVoltage));
                            }
                            spiceCircuit.Add(new SpiceSharp.Components.Resistor(pr.Name, targetNode, railNode, resVal));
                        }
                        break;
                    }

                    case ProbeComponent probe:
                    {
                        string prbNode = GetPinNode(probe, "in", 0);
                        var sense = new SpiceSharp.Components.Resistor($"{probe.Name}_sense", prbNode, "0", Math.Max(probe.InputImpedance, 1e3));
                        spiceCircuit.Add(sense);
                        break;
                    }

                    case DigitalTransistorComponent dtr:
                    {
                        string dNode = GetPinNode(dtr, "drain", 0);
                        string sNode = GetPinNode(dtr, "source", 1);
                        string gNode = GetPinNode(dtr, "gate", 2);

                        var swModel = new SpiceSharp.Components.VoltageSwitchModel($"{dtr.Name}_model");
                        swModel.SetParameter("ron", Math.Max(dtr.ResistanceOn, 1e-4));
                        swModel.SetParameter("roff", Math.Max(dtr.ResistanceOff, 100.0));
                        swModel.Parameters.Threshold = dtr.Threshold;
                        swModel.Parameters.Hysteresis = Math.Max(dtr.Threshold * 0.05, 0.01);

                        var sw = dtr.TransistorType == DigitalTransistorType.NType
                            ? new SpiceSharp.Components.VoltageSwitch(dtr.Name, dNode, sNode, gNode, sNode, $"{dtr.Name}_model")
                            : new SpiceSharp.Components.VoltageSwitch(dtr.Name, dNode, sNode, sNode, gNode, $"{dtr.Name}_model");

                        spiceCircuit.Add(swModel);
                        spiceCircuit.Add(sw);
                        break;
                    }

                    case TransmissionGateComponent tg:
                    {
                        string inNode = GetPinNode(tg, "in", 0);
                        string outNode = GetPinNode(tg, "out", 1);
                        string ctrlNode = GetPinNode(tg, "ctrl", 2);
                        string ctrlInvNode = GetPinNode(tg, "ctrl_inv", 3);

                        // Complementary N-MOS and P-MOS switch pair in parallel
                        var swModelN = new SpiceSharp.Components.VoltageSwitchModel($"{tg.Name}_nmodel");
                        swModelN.SetParameter("ron", Math.Max(tg.ResistanceOn, 1e-4));
                        swModelN.SetParameter("roff", Math.Max(tg.ResistanceOff, 100.0));
                        swModelN.Parameters.Threshold = 2.5;
                        swModelN.Parameters.Hysteresis = 0.2;

                        var swN = new SpiceSharp.Components.VoltageSwitch($"{tg.Name}_n", inNode, outNode, ctrlNode, "0", $"{tg.Name}_nmodel");

                        var swModelP = new SpiceSharp.Components.VoltageSwitchModel($"{tg.Name}_pmodel");
                        swModelP.SetParameter("ron", Math.Max(tg.ResistanceOn, 1e-4));
                        swModelP.SetParameter("roff", Math.Max(tg.ResistanceOff, 100.0));
                        swModelP.Parameters.Threshold = -2.5;
                        swModelP.Parameters.Hysteresis = 0.2;

                        var swP = new SpiceSharp.Components.VoltageSwitch($"{tg.Name}_p", inNode, outNode, "0", ctrlInvNode, $"{tg.Name}_pmodel");

                        spiceCircuit.Add(swModelN);
                        spiceCircuit.Add(swN);
                        spiceCircuit.Add(swModelP);
                        spiceCircuit.Add(swP);
                        break;
                    }

                    case BitExtenderComponent be:
                    {
                        string inNode = GetPinNode(be, "in", 0);
                        string outNode = GetPinNode(be, "out", 1);
                        var vcvs = new SpiceSharp.Components.VoltageControlledVoltageSource(be.Name, outNode, "0", inNode, "0", 1.0);
                        spiceCircuit.Add(vcvs);
                        break;
                    }

                    case ConstantComponent cst:
                    {
                        string outNode = GetPinNode(cst, "out", 0);
                        double vLevel = cst.Value > 0 ? cst.VoltageHigh : cst.VoltageLow;
                        var vsrc = new SpiceSharp.Components.VoltageSource(cst.Name, outNode, "0", vLevel);
                        spiceCircuit.Add(vsrc);
                        break;
                    }

                    case DoNotConnectComponent dnc:
                    {
                        string pinNode = GetPinNode(dnc, "pin", 0);
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{dnc.Name}_term", pinNode, "0", 100e6));
                        break;
                    }

                    case LossyTransmissionLineComponent ltl:
                    {
                        string p1Pos = GetPinNode(ltl, "pos1", 0);
                        string p1Neg = GetPinNode(ltl, "neg1", 1);
                        string p2Pos = GetPinNode(ltl, "pos2", 2);
                        string p2Neg = GetPinNode(ltl, "neg2", 3);
                        double z0 = Math.Max(ltl.CharacteristicImpedance, 1e-3);
                        double td = Math.Max(ltl.TotalDelay, 1e-15);

                        var t = new SpiceSharp.Components.LosslessTransmissionLine(ltl.Name, p1Pos, p1Neg, p2Pos, p2Neg, z0, td);
                        spiceCircuit.Add(t);

                        double totalR = ltl.ResistancePerMeter * ltl.Length;
                        if (totalR > 1e-6)
                        {
                            spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{ltl.Name}_r_pos", p1Pos, p2Pos, totalR / 2.0));
                            spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{ltl.Name}_r_neg", p1Neg, p2Neg, totalR / 2.0));
                        }

                        double totalG = ltl.ConductancePerMeter * ltl.Length;
                        if (totalG > 1e-12)
                        {
                            spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{ltl.Name}_g_shunt", p2Pos, p2Neg, 1.0 / totalG));
                        }
                        break;
                    }

                    case CoaxialCableComponent coax:
                    {
                        string p1Pos = GetPinNode(coax, "core_in", 0);
                        string p1Neg = GetPinNode(coax, "shield_in", 1);
                        string p2Pos = GetPinNode(coax, "core_out", 2);
                        string p2Neg = GetPinNode(coax, "shield_out", 3);
                        double z0 = Math.Max(coax.CharacteristicImpedance, 1e-3);
                        double td = Math.Max(coax.Delay, 1e-15);

                        var t = new SpiceSharp.Components.LosslessTransmissionLine(coax.Name, p1Pos, p1Neg, p2Pos, p2Neg, z0, td);
                        spiceCircuit.Add(t);

                        double totalAttenDb = coax.Attenuation * coax.Length;
                        if (totalAttenDb > 1e-4)
                        {
                            double rLoss = 2.0 * (Math.Pow(10.0, totalAttenDb / 20.0) - 1.0) * z0;
                            if (rLoss > 1e-6)
                            {
                                spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{coax.Name}_loss", p1Pos, p2Pos, rLoss));
                            }
                        }
                        break;
                    }

                    case MicrostripLineComponent ustrip:
                    {
                        string p1Pos = GetPinNode(ustrip, "in", 0);
                        string p1Neg = GetPinNode(ustrip, "gnd_in", 1);
                        string p2Pos = GetPinNode(ustrip, "out", 2);
                        string p2Neg = GetPinNode(ustrip, "gnd_out", 3);
                        double z0 = Math.Max(ustrip.CharacteristicImpedance, 1e-3);
                        double td = Math.Max(ustrip.Delay, 1e-15);

                        var t = new SpiceSharp.Components.LosslessTransmissionLine(ustrip.Name, p1Pos, p1Neg, p2Pos, p2Neg, z0, td);
                        spiceCircuit.Add(t);
                        break;
                    }

                    case OpAmpComponent opamp:
                    {
                        string inPos = GetPinNode(opamp, "in_pos", 1);
                        string inNeg = GetPinNode(opamp, "in_neg", 0);
                        string outNode = GetPinNode(opamp, "out", 2);
                        string vPos = GetPinNode(opamp, "v_pos", 3);
                        string vNeg = GetPinNode(opamp, "v_neg", 4);

                        double aol = Math.Max(opamp.OpenLoopGain, 1.0);
                        var vcvs = new SpiceSharp.Components.VoltageControlledVoltageSource(opamp.Name, outNode, "0", inPos, inNeg, aol);
                        spiceCircuit.Add(vcvs);

                        if (vPos != "0")
                        {
                            spiceCircuit.Add(new SpiceSharp.Components.VoltageSource($"{opamp.Name}_vp", vPos, "0", opamp.SupplyPositive));
                        }
                        if (vNeg != "0")
                        {
                            spiceCircuit.Add(new SpiceSharp.Components.VoltageSource($"{opamp.Name}_vn", vNeg, "0", opamp.SupplyNegative));
                        }
                        break;
                    }
                }

                if (DigitalLogicSimulator.IsSpiceDrivenDigital(comp))
                {
                    foreach (var (pinId, voltage) in DigitalLogicSimulator.GetOutputVoltages(comp))
                    {
                        string outputNode = GetPinNode(comp, pinId);
                        if (outputNode != "0")
                        {
                            string driverName = $"{comp.Name}_logic_{pinId}";
                            spiceCircuit.Add(new SpiceSharp.Components.VoltageSource(driverName, outputNode, "0", voltage));
                        }
                    }
                }
            }

            // Link Mutual Inductances after inductors are added
            foreach (var m in mutualInductances)
            {
                string l1 = m.Inductor1;
                string l2 = m.Inductor2;

                // Resolve inductor names from connected pins if wired
                string pri1Net = GetPinNode(m, "pri1", 0);
                string pri2Net = GetPinNode(m, "pri2", 1);
                string sec1Net = GetPinNode(m, "sec1", 2);
                string sec2Net = GetPinNode(m, "sec2", 3);

                var allInductors = circuit.Components.OfType<InductorComponent>().ToList();
                var foundL1 = allInductors.FirstOrDefault(ind =>
                {
                    string nA = GetPinNode(ind, "pin1", 0);
                    string nB = GetPinNode(ind, "pin2", 1);
                    return (nA == pri1Net && nB == pri2Net) || (nA == pri2Net && nB == pri1Net);
                });
                if (foundL1 != null) l1 = foundL1.Name;

                var foundL2 = allInductors.FirstOrDefault(ind =>
                {
                    if (ind == foundL1) return false;
                    string nA = GetPinNode(ind, "pin1", 0);
                    string nB = GetPinNode(ind, "pin2", 1);
                    return (nA == sec1Net && nB == sec2Net) || (nA == sec2Net && nB == sec1Net);
                });
                if (foundL2 != null) l2 = foundL2.Name;

                try
                {
                    var mutual = new SpiceSharp.Components.MutualInductance(m.Name, l1, l2, m.Coupling);
                    spiceCircuit.Add(mutual);
                }
                catch (Exception ex)
                {
                    results.Message += $"\n[Warning] Mutual inductance {m.Name} ({l1}, {l2}, k={m.Coupling}) could not be linked: {ex.Message}";
                }
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
                    else if (comp is BulbComponent b && b.Resistance > 0)
                    {
                        comp.LiveCurrent = vDrop / b.Resistance;
                        b.Brightness = Math.Clamp(vDrop / Math.Max(b.RatedVoltage, 0.1), 0.0, 1.0);
                        b.IsOn = vDrop >= b.ThresholdVoltage || comp.LiveCurrent >= 0.02;
                        results.OperatingPointCurrents[$"I({comp.Name})"] = comp.LiveCurrent;
                    }
                    else if (comp is LEDComponent led)
                    {
                        comp.LiveVoltage = vDrop;
                        led.IsOn = vDrop >= Math.Min(led.ForwardVoltage * 0.75, 1.5) || comp.LiveCurrent >= 0.001;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = vDrop;
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
                        comp.LiveVoltage = v.Voltage;
                        results.OperatingPointVoltages[$"V({comp.Name})"] = v.Voltage;
                    }
                    else if (comp is CurrentSourceComponent csrc)
                    {
                        comp.LiveCurrent = csrc.Current;
                        results.OperatingPointCurrents[$"I({comp.Name})"] = csrc.Current;
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
                    else if (comp is PullResistorComponent pr)
                    {
                        comp.LiveVoltage = vDrop;
                        comp.LiveCurrent = pr.Resistance > 0 ? vDrop / pr.Resistance : 0.0;
                        results.OperatingPointCurrents[$"I({comp.Name})"] = comp.LiveCurrent;
                    }
                }
            }

            // Refresh logic outputs from the solved input nodes for probes and the next playback tick.
            DigitalLogicSimulator.EvaluateCombinational(circuit);

            // 4. Generate Transient / Waveform data for components with ShowWaveform
            var waveComps = circuit.Components.Where(c => c.ShowWaveform).ToList();
            if (waveComps.Count > 0)
            {
                GenerateWaveforms(waveComps, settings);
            }

            results.Success = true;
            results.Message = $"Spice# {settings.Analysis} simulation completed: {topology.Nets.Count} nets, {spiceCircuit.Count} Spice elements.";
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

    private static void GenerateWaveforms(List<ComponentNode> components, SimulationSettings? settings = null)
    {
        double duration = (settings != null && settings.StopTime > settings.StartTime) ? (settings.StopTime - settings.StartTime) : 0.02;
        int steps = 120;
        if (settings != null && settings.StepTime > 0)
        {
            steps = (int)Math.Clamp(duration / settings.StepTime, 50, 500);
        }
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
            else if (comp is ClockComponent clkComp)
            {
                wfType = "PULSE";
                freq = clkComp.Frequency > 0 ? clkComp.Frequency : 1000.0;
                amp = clkComp.HighVoltage;
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

        if (totalResistance > 0 && sourceVoltage > 0)
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
        }

        GenerateWaveforms(circuit.Components.Where(c => c.ShowWaveform).ToList());

        results.Success = true;
        results.Message = $"Simulation solved (Linear/Nodal): {ex.Message}";
    }
}
