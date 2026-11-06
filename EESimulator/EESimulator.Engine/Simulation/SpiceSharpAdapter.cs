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
                    case SinglePhaseTransmissionLineComponent line:
                    {
                        string lineInput = nodeA;
                        if (line.SupplyMode == "FixedVoltageSource")
                        {
                            lineInput = $"{line.Name}_fixed_source_out";
                            AddFixedAcSource(spiceCircuit, $"{line.Name}_fixed_source", nodeA, lineInput,
                                line.FixedVoltageRms, line.Frequency, line.InitialPhaseAngle, settings.StartTime);
                        }
                        AddLineBranch(spiceCircuit, line.Name, lineInput, nodeB,
                            line.TotalResistance, line.InductancePerKm * line.LengthKm,
                            line.CapacitancePerKm * line.LengthKm, line.ConductancePerKm * line.LengthKm,
                            line.ModelType is "Medium Line" or "Long Line", line.Frequency,
                            line.ModelType == "Long Line");
                        break;
                    }

                    case TwoPhaseTransmissionLineComponent line:
                    {
                        string phaseAInput = nodeA;
                        if (line.SupplyMode == "FixedVoltageSource")
                        {
                            phaseAInput = $"{line.Name}_phaseA_source_out";
                            AddFixedAcSource(spiceCircuit, $"{line.Name}_phaseA_source", nodeA, phaseAInput,
                                line.FixedVoltageRms, line.Frequency, 0, settings.StartTime);
                        }
                        AddLineBranch(spiceCircuit, line.Name, phaseAInput, nodeB,
                            line.ResistancePerKm * line.LengthKm, line.InductancePerKm * line.LengthKm,
                            line.CapacitancePerKm * line.LengthKm, line.ConductancePerKm * line.LengthKm, true, line.Frequency);
                        if (line.Pins.Count >= 4)
                        {
                            string phaseBFrom = GetNetForPin(line.Id, line.Pins[2].Id);
                            string phaseBTo = GetNetForPin(line.Id, line.Pins[3].Id);
                            string phaseBInput = phaseBFrom;
                            if (line.SupplyMode == "FixedVoltageSource")
                            {
                                phaseBInput = $"{line.Name}_phaseB_source_out";
                                double phaseBOffset = line.ConductorConfiguration == "SplitPhaseThreeWire"
                                    ? 180
                                    : line.PhaseDisplacementDegrees;
                                AddFixedAcSource(spiceCircuit, $"{line.Name}_phaseB_source", phaseBFrom, phaseBInput,
                                    line.FixedVoltageRms, line.Frequency, phaseBOffset, settings.StartTime);
                            }
                            AddLineBranch(spiceCircuit, $"{line.Name}_phaseB", phaseBInput, phaseBTo,
                                line.ResistancePerKm * line.LengthKm, line.InductancePerKm * line.LengthKm,
                                line.CapacitancePerKm * line.LengthKm, line.ConductancePerKm * line.LengthKm, true, line.Frequency);
                        }
                            if (line.ConductorConfiguration == "SplitPhaseThreeWire" && line.Pins.Count >= 6)
                            {
                                string neutralFrom = GetNetForPin(line.Id, line.Pins[4].Id);
                                string neutralTo = GetNetForPin(line.Id, line.Pins[5].Id);
                                AddLineBranch(spiceCircuit, $"{line.Name}_neutral", neutralFrom, neutralTo,
                                line.ResistancePerKm * line.LengthKm, line.InductancePerKm * line.LengthKm,
                                line.CapacitancePerKm * line.LengthKm, line.ConductancePerKm * line.LengthKm, true, line.Frequency);
                            }
                        break;
                    }

                    case ThreePhaseTransmissionLineComponent line:
                    {
                        for (int phase = 0; phase < 3; phase++)
                        {
                            string from = GetNetForPin(line.Id, line.Pins[phase].Id);
                            string to = GetNetForPin(line.Id, line.Pins[phase + 3].Id);
                            string lineInput = from;
                            if (line.SupplyMode == "FixedVoltageSource")
                            {
                                lineInput = $"{line.Name}_phase{phase + 1}_source_out";
                                double phaseOrder = line.PhaseSequence == "ACB" ? -1 : 1;
                                AddFixedAcSource(spiceCircuit, $"{line.Name}_phase{phase + 1}_source", from, lineInput,
                                    line.FixedPhaseVoltageRms, line.Frequency, phaseOrder * phase * 120, settings.StartTime);
                            }
                            AddLineBranch(spiceCircuit, $"{line.Name}_phase{phase + 1}", lineInput, to,
                                line.ResistancePerKmPerPhase * line.LengthKm,
                                line.InductancePerKmPerPhase * line.LengthKm,
                                line.CapacitancePerKmPerPhase * line.LengthKm,
                                line.ConductancePerKmPerPhase * line.LengthKm, line.ModelType != "Short", line.Frequency,
                                line.ModelType == "Long");
                        }
                        if (line.Connection == "Four-wire" && line.Pins.Count >= 8)
                        {
                            string neutralFrom = GetNetForPin(line.Id, line.Pins[6].Id);
                            string neutralTo = GetNetForPin(line.Id, line.Pins[7].Id);
                            spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{line.Name}_neutral", neutralFrom, neutralTo,
                                Math.Max(line.NeutralImpedance, 1e-6)));
                        }
                        break;
                    }

                    case ACBulbComponent bulb:
                    {
                        double resistance = Math.Max(bulb.EquivalentResistance, 1e-6);
                        if (!bulb.InitiallyOn || !bulb.ScheduleActive) resistance = 1e12;
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor(bulb.Name, nodeA, nodeB, resistance));
                        break;
                    }

                    case ACFanComponent fan:
                    {
                        double activePower = Math.Max(fan.ElectricalInputPower, 1e-6);
                        double resistance = fan.RatedVoltage * fan.RatedVoltage / activePower;
                        if (!fan.InitiallyOn || !fan.ScheduleActive || fan.SelectedSpeed == 0) resistance = 1e12;
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor(fan.Name, nodeA, nodeB, Math.Max(resistance, 1e-6)));
                        break;
                    }

                    case InductionMotorComponent motor:
                    {
                        string neutral = motor.Pins.Count > 3 ? GetNetForPin(motor.Id, motor.Pins[3].Id) : "0";
                        void AddMotorPhase(string phaseName, string phaseNode)
                        {
                            var impedance = motor.PerPhaseInputImpedance;
                            string internalNode = $"{motor.Name}_{phaseName}_rl";
                            spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{motor.Name}_{phaseName}_R", phaseNode, internalNode,
                                Math.Max(impedance.Real, 1e-6)));
                            if (impedance.Imaginary > 1e-9)
                                spiceCircuit.Add(new SpiceSharp.Components.Inductor($"{motor.Name}_{phaseName}_L", internalNode, neutral,
                                    Math.Max(impedance.Imaginary / (2 * Math.PI * motor.Frequency), 1e-12)));
                            else
                                spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{motor.Name}_{phaseName}_link", internalNode, neutral, 1e-6));
                        }
                        AddMotorPhase("A", nodeA);
                        for (int phase = 1; phase < Math.Min(3, motor.Pins.Count - 1); phase++)
                            AddMotorPhase($"phase{phase + 1}", GetNetForPin(motor.Id, motor.Pins[phase].Id));
                        break;
                    }

                    case BatteryComponent battery:
                    {
                        battery.ValidateConfiguration();
                        bool batteryConnected = battery.InitialState == "Connected" &&
                            PowerSystemComponent.IsScheduleActive(battery.OperatingSchedule, settings.DeviceTimeSeconds);
                        string internalNode = $"{battery.Name}_cell_{battery.Id.ToString("N")[..8]}";
                        spiceCircuit.Add(new SpiceSharp.Components.VoltageSource(battery.Name, internalNode, nodeB,
                            batteryConnected ? Math.Clamp(battery.OpenCircuitVoltage, battery.MinimumVoltage, battery.MaximumVoltage) : 0));
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{battery.Name}_internalR", nodeA, internalNode,
                            batteryConnected ? Math.Max(battery.TemperatureAdjustedResistance, 1e-6) : 1e12));
                        break;
                    }

                    case SolarPanelComponent panel:
                    {
                        string internalNode = $"{panel.Name}_pv_{panel.Id.ToString("N")[..8]}";
                        double temperatureFactor = Math.Max(0.0, 1 + panel.VocTemperatureCoefficient * (panel.CellTemperature - 25));
                        double irradianceCurrent = Math.Max(0.0, panel.ShortCircuitCurrent * panel.SolarIrradiance / 1000.0 * panel.ParallelStrings);
                        var model = new SpiceSharp.Components.DiodeModel($"{panel.Name}_pvmodel");
                        model.Parameters.SaturationCurrent = 1e-10;
                        model.Parameters.EmissionCoefficient = 1.2;
                        spiceCircuit.Add(model);
                        spiceCircuit.Add(new SpiceSharp.Components.Diode($"{panel.Name}_diode", internalNode, nodeB, $"{panel.Name}_pvmodel"));
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{panel.Name}_series", nodeA, internalNode,
                            Math.Max(panel.SeriesResistance, 1e-5)));
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{panel.Name}_shunt", internalNode, nodeB,
                            Math.Max(panel.ShuntResistance, 1e-5)));
                        var photoSource = new SpiceSharp.Components.CurrentSource($"{panel.Name}_photocurrent", nodeB, internalNode,
                            irradianceCurrent * temperatureFactor);
                        var profileTimes = panel.IrradianceProfilePoints.Select(point => point.Time)
                            .Concat(panel.TemperatureProfilePoints.Select(point => point.Time))
                            .Where(deviceTime => deviceTime > settings.DeviceTimeSeconds &&
                                deviceTime < settings.DeviceTimeSeconds + Math.Max(0, settings.StopTime - settings.StartTime) *
                                    (settings.TimeScalingEnabled ? settings.DeviceTimeScale : 1.0))
                            .Select(deviceTime => settings.StartTime +
                                (deviceTime - settings.DeviceTimeSeconds) /
                                (settings.TimeScalingEnabled ? Math.Max(settings.DeviceTimeScale, 1e-12) : 1.0));
                        var solverTimes = new[] { settings.StartTime }
                            .Concat(profileTimes)
                            .Append(settings.StopTime)
                            .Distinct()
                            .OrderBy(time => time)
                            .ToArray();
                        if (solverTimes.Length >= 2 &&
                            (panel.IrradianceProfilePoints.Count > 0 || panel.TemperatureProfilePoints.Count > 0))
                        {
                            var pvWaveform = new SpiceSharp.Components.Pwl();
                            var points = new List<double>(solverTimes.Length * 2);
                            double profileScale = settings.TimeScalingEnabled ? settings.DeviceTimeScale : 1.0;
                            foreach (double solverTime in solverTimes)
                            {
                                double deviceTime = settings.DeviceTimeSeconds +
                                    (solverTime - settings.StartTime) * profileScale;
                                points.Add(solverTime);
                                points.Add(panel.PhotocurrentAtDeviceTime(deviceTime));
                            }
                            pvWaveform.SetPoints(points.ToArray());
                            photoSource.Parameters.Waveform = pvWaveform;
                        }
                        spiceCircuit.Add(photoSource);
                        break;
                    }

                    case ACTransformerComponent transformer:
                    {
                        transformer.ValidateConfiguration();
                        if (transformer.NumberOfPhases == 3)
                        {
                            string[] phases = { "A", "B", "C" };
                            string primaryNeutral = GetNetForPin(transformer.Id, transformer.Pins[6].Id);
                            string secondaryNeutral = GetNetForPin(transformer.Id, transformer.Pins[7].Id);
                            bool primaryDelta = transformer.WindingConnection is "Delta-Delta" or "Delta-Y";
                            bool secondaryDelta = transformer.WindingConnection is "Delta-Delta" or "Y-Delta";
                            string suffix3 = transformer.Id.ToString("N")[..8];
                            for (int phase = 0; phase < 3; phase++)
                            {
                                int next = (phase + 1) % 3;
                                string primaryStart = GetNetForPin(transformer.Id, transformer.Pins[phase].Id);
                                string primaryEnd = primaryDelta
                                    ? GetNetForPin(transformer.Id, transformer.Pins[next].Id)
                                    : primaryNeutral;
                                string secondaryStart = GetNetForPin(transformer.Id, transformer.Pins[phase + 3].Id);
                                string secondaryEnd = secondaryDelta
                                    ? GetNetForPin(transformer.Id, transformer.Pins[next + 3].Id)
                                    : secondaryNeutral;
                                string primaryPhaseInternal = $"{transformer.Name}_pri{phase}_{suffix3}";
                                string secondaryPhaseInternal = $"{transformer.Name}_sec{phase}_{suffix3}";
                                spiceCircuit.Add(new SpiceSharp.Components.Resistor(
                                    $"{transformer.Name}_primaryR{phase}", primaryStart, primaryPhaseInternal,
                                    Math.Max(transformer.PrimaryResistance, 1e-6)));
                                spiceCircuit.Add(new SpiceSharp.Components.Inductor(
                                    $"{transformer.Name}_mag{phase}", primaryPhaseInternal, primaryEnd,
                                    Math.Max(transformer.MagnetizingInductance, 1e-9)));
                                spiceCircuit.Add(new SpiceSharp.Components.Resistor(
                                    $"{transformer.Name}_core{phase}", primaryPhaseInternal, primaryEnd,
                                    Math.Max(transformer.CoreLossResistance, 1e-6)));
                                spiceCircuit.Add(new SpiceSharp.Components.VoltageControlledVoltageSource(
                                    $"{transformer.Name}_winding{phase}", secondaryPhaseInternal, secondaryEnd,
                                    primaryPhaseInternal, primaryEnd,
                                    (transformer.DotsAligned ? 1 : -1) / Math.Max(transformer.TurnsRatio, 1e-9)));
                                spiceCircuit.Add(new SpiceSharp.Components.Resistor(
                                    $"{transformer.Name}_secondaryR{phase}", secondaryPhaseInternal, secondaryStart,
                                    Math.Max(transformer.SecondaryResistance, 1e-6)));
                                _ = phases[phase];
                            }
                            break;
                        }

                        string suffix = transformer.Id.ToString("N")[..8];
                        string primaryInternal = $"{transformer.Name}_pri_{suffix}";
                        string secondaryInternal = $"{transformer.Name}_sec_{suffix}";
                        string secondaryReturnInternal = $"{transformer.Name}_sec_return_{suffix}";
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{transformer.Name}_primaryR", nodeA, primaryInternal,
                            Math.Max(transformer.PrimaryResistance, 1e-6)));
                        spiceCircuit.Add(new SpiceSharp.Components.Inductor($"{transformer.Name}_mag", primaryInternal, nodeB,
                            Math.Max(transformer.MagnetizingInductance, 1e-9)));
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{transformer.Name}_core", primaryInternal, nodeB,
                            Math.Max(transformer.CoreLossResistance, 1e-6)));
                        string secondaryPositive = GetNetForPin(transformer.Id, transformer.Pins[2].Id);
                        string secondaryNegative = GetNetForPin(transformer.Id, transformer.Pins[3].Id);
                        spiceCircuit.Add(new SpiceSharp.Components.VoltageControlledVoltageSource(
                            transformer.Name, secondaryInternal, secondaryReturnInternal, primaryInternal, nodeB,
                            (transformer.DotsAligned ? 1 : -1) / Math.Max(transformer.TurnsRatio, 1e-9)));
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{transformer.Name}_secondaryR", secondaryInternal, secondaryPositive,
                            Math.Max(transformer.SecondaryResistance, 1e-6)));
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{transformer.Name}_secondaryReturnR", secondaryReturnInternal, secondaryNegative,
                            Math.Max(transformer.SecondaryResistance, 1e-6)));
                        break;
                    }

                    case SynchronousGeneratorComponent generator:
                    {
                        generator.ValidateConfiguration();
                        string neutral = generator.Pins.Count > 3 ? GetNetForPin(generator.Id, generator.Pins[3].Id) : "0";
                        double peakVoltage = generator.GeneratedEmf * Math.Sqrt(2);
                        int phases = Math.Clamp(generator.NumberOfPhases, 1, 3);
                        for (int phase = 0; phase < phases; phase++)
                        {
                            string phaseNode = GetNetForPin(generator.Id, generator.Pins[phase].Id);
                            double angle = generator.InitialRotorAngle + (generator.PhaseSequence == "ACB" ? -1 : 1) * phase * 120;
                            string sourceName = phase == 0 ? generator.Name : $"{generator.Name}_phase{phase + 1}";
                            string internalNode = $"{sourceName}_emf_{generator.Id.ToString("N")[..8]}";
                            var source = new SpiceSharp.Components.VoltageSource(sourceName, internalNode, neutral,
                                generator.GeneratedEmf * Math.Cos(angle * Math.PI / 180));
                            source.Parameters.Waveform = new SpiceSharp.Components.Sine(0, peakVoltage,
                                generator.RatedFrequency, 0, 0, angle * Math.PI / 180.0);
                            spiceCircuit.Add(source);
                            string reactanceNode = $"{sourceName}_xd_{generator.Id.ToString("N")[..8]}";
                            spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{sourceName}_armatureR", internalNode, reactanceNode,
                                Math.Max(generator.ArmatureResistance, 1e-6)));
                            double synchronousReactance = Math.Max(generator.Xd, 0);
                            if (synchronousReactance > 1e-9)
                                spiceCircuit.Add(new SpiceSharp.Components.Inductor($"{sourceName}_armatureL", reactanceNode, phaseNode,
                                    synchronousReactance / (2 * Math.PI * generator.RatedFrequency)));
                            else
                                spiceCircuit.Add(new SpiceSharp.Components.Resistor($"{sourceName}_armatureLink", reactanceNode, phaseNode, 1e-6));
                        }
                        break;
                    }

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

                    case WireComponent wire:
                    {
                        // A schematic wire is an electrical branch too. Use a tiny
                        // resistance for an ideal wire so its branch current is measurable.
                        var res = new SpiceSharp.Components.Resistor(wire.Name, nodeA, nodeB, Math.Max(wire.Resistance, 1e-6));
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

                    case RelayComponent relay:
                    {
                        relay.ValidateConfiguration();
                        string coilPositive = GetPinNode(relay, "coil_positive", 0);
                        string coilNegative = GetPinNode(relay, "coil_negative", 1);
                        string common = GetPinNode(relay, "common", 2);
                        string normallyOpen = GetPinNode(relay, "normally_open", 3);
                        spiceCircuit.Add(new SpiceSharp.Components.Resistor(
                            $"{relay.Name}_coil", coilPositive, coilNegative, Math.Max(relay.CoilResistance, 1e-3)));

                        var relayModel = new SpiceSharp.Components.VoltageSwitchModel($"{relay.Name}_contact_model");
                        relayModel.SetParameter("ron", Math.Max(relay.ContactResistance, 1e-6));
                        relayModel.SetParameter("roff", Math.Max(relay.OpenResistance, relay.ContactResistance + 1));
                        relayModel.Parameters.Threshold = (relay.PullInVoltage + relay.DropoutVoltage) / 2;
                        relayModel.Parameters.Hysteresis = Math.Max(0, relay.PullInVoltage - relay.DropoutVoltage) / 2;
                        spiceCircuit.Add(relayModel);
                        spiceCircuit.Add(new SpiceSharp.Components.VoltageSwitch(
                            relay.Name, common, normallyOpen, coilPositive, coilNegative,
                            $"{relay.Name}_contact_model"));
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
