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
    public void ConnectPins(ComponentNode fromComponent, ComponentPin fromPin, ComponentNode toComponent, ComponentPin toPin)
    {
        try
        {
            bool connected = CommandManager.ExecuteCommand(new ConnectNodesCommand(
                Circuit, fromComponent.Name, fromPin.Id, toComponent.Name, toPin.Id)
            {
                Origin = CommandOrigin.Schematic
            });
            if (!connected)
            {
                StatusText = "Could not connect those pins.";
                return;
            }

            StatusText = $"Connected {fromComponent.Reference}:{fromPin.Name} → {toComponent.Reference}:{toPin.Name}";
            CircuitChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"Wire error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void AddComponentEventTrigger()
    {
        if (SelectedComponent == null) return;
        var trigger = new SimulationEventTrigger
        {
            Name = $"Event {SelectedComponentEventTriggers.Count + 1}",
            Measurement = "Voltage",
            Comparison = ">=",
            Threshold = 1,
            Target = "Chat bubble",
            Message = "Voltage threshold reached",
            Mode = "Latched"
        };
        SelectedComponentEventTriggers.Add(trigger);
        SaveComponentEventTriggers();
    }

    [RelayCommand]
    public void RemoveComponentEventTrigger(SimulationEventTrigger trigger)
    {
        if (trigger != null && SelectedComponentEventTriggers.Remove(trigger))
            SaveComponentEventTriggers();
    }

    [RelayCommand]
    public void SaveComponentEventTriggers()
    {
        if (SelectedComponent == null) return;
        SelectedComponent.EventTriggers = SelectedComponentEventTriggers.ToList();
        SelectedComponent.PersistEventTriggers();
        CircuitChanged?.Invoke();
        StatusText = $"Saved {SelectedComponentEventTriggers.Count} UI event trigger(s) for {SelectedComponent.Reference}";
    }

    [RelayCommand]
    public void CloseSimulationEventDialog() => IsSimulationEventDialogVisible = false;

    [RelayCommand]
    public void DismissSimulationEventChat() => IsSimulationEventChatVisible = false;

    public void FireNamedUiEvent(string eventName, string? message = null)
    {
        var match = Circuit.Components.SelectMany(component => component.EventTriggers
                .Select(trigger => (Component: component, Trigger: trigger)))
            .FirstOrDefault(item => item.Trigger.Name.Equals(eventName, StringComparison.OrdinalIgnoreCase) ||
                                    item.Trigger.Id.Equals(eventName, StringComparison.OrdinalIgnoreCase));
        if (match.Trigger == null)
        {
            ShowSimulationEventChat(message ?? eventName);
            return;
        }

        match.Trigger.IsFired = true;
        DispatchUiEvent(match.Component, match.Trigger.Name, match.Trigger.Target, message ?? match.Trigger.Message, match.Trigger.Id);
    }

    private void ToggleNamedUiEvent(string eventName)
    {
        var match = Circuit.Components.SelectMany(component => component.EventTriggers
                .Select(trigger => (Component: component, Trigger: trigger)))
            .FirstOrDefault(item => item.Trigger.Name.Equals(eventName, StringComparison.OrdinalIgnoreCase) ||
                                    item.Trigger.Id.Equals(eventName, StringComparison.OrdinalIgnoreCase));
        if (match.Trigger == null)
        {
            FireNamedUiEvent(eventName);
            return;
        }

        if (!match.Trigger.IsFired)
        {
            FireNamedUiEvent(eventName);
            return;
        }

        match.Trigger.IsFired = false;
        if (_simulationEventOwnersByTarget.TryGetValue(match.Trigger.Target, out var owner) &&
            owner.Equals(match.Trigger.Id, StringComparison.OrdinalIgnoreCase))
        {
            _simulationEventOwnersByTarget.Remove(match.Trigger.Target);
            switch (match.Trigger.Target)
            {
                case "Dialog": IsSimulationEventDialogVisible = false; break;
                case "Waveform drawer": IsWaveformDrawerOpen = false; break;
                case "Description sidebar": IsDescriptionSidebarVisible = false; break;
                case "Control sidebar": IsControlSidebarVisible = false; break;
                case "Chat bubble": IsSimulationEventChatVisible = false; break;
            }
        }
        _activeSimulationEventIds.Remove(match.Trigger.Id);
        StatusText = $"UI event '{match.Trigger.Name}' toggled off";
    }

    private ComponentNode? FindComponentTarget(string target)
    {
        string componentName = target.Split(':', 2)[0].Trim();
        return Circuit.Components.FirstOrDefault(component =>
            component.Name.Equals(componentName, StringComparison.OrdinalIgnoreCase) ||
            component.Reference.Equals(componentName, StringComparison.OrdinalIgnoreCase));
    }

    private void SetWaveformTarget(string target, bool enabled)
    {
        var component = FindComponentTarget(target);
        if (component == null)
            throw new InvalidOperationException($"Component or node '{target}' was not found.");

        string[] targetParts = target.Split(':', 2);
        if (targetParts.Length == 2 && component.GetPin(targetParts[1]) == null)
            throw new InvalidOperationException($"Node '{targetParts[1]}' does not exist on {component.Reference}.");

        if (enabled)
        {
            ConfigureWaveformForComponent(component, showTerminalPicker: false);
            if (targetParts.Length == 2)
            {
                var pin = component.GetPin(targetParts[1]);
                WaveformSignals.Clear();
                WaveformSignals.Add(new WaveformSignal(component, pin!.Id,
                    $"{component.Reference}:{pin.Name} voltage", WaveformSignalKind.PinVoltage));
            }
        }
        else
        {
            PythonApi.SetWaveform(component.Name, false);
            if (WaveformComponent == component)
                IsWaveformDrawerOpen = false;
        }
    }

    private void DispatchTriggeredUiEvents()
    {
        var activeEvents = ComponentEventEvaluator.Evaluate(Circuit);
        var activeIds = activeEvents.Select(item => item.TriggerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in activeEvents)
        {
            if (!_activeSimulationEventIds.Add(item.TriggerId)) continue;
            var component = Circuit.Components.FirstOrDefault(candidate => candidate.Id == item.ComponentId);
            if (component != null)
                DispatchUiEvent(component, item.Name, item.Target, item.Message, item.TriggerId);
        }

        foreach (var inactiveId in _activeSimulationEventIds.Where(id => !activeIds.Contains(id)).ToList())
        {
            _activeSimulationEventIds.Remove(inactiveId);
            foreach (var target in _simulationEventOwnersByTarget.Where(pair => pair.Value == inactiveId).Select(pair => pair.Key).ToList())
            {
                _simulationEventOwnersByTarget.Remove(target);
                if (target == "Dialog") IsSimulationEventDialogVisible = false;
                if (target == "Chat bubble") IsSimulationEventChatVisible = false;
            }
        }
    }

    private void DispatchUiEvent(ComponentNode source, string eventName, string target, string message, string triggerId)
    {
        _simulationEventOwnersByTarget[target] = triggerId;
        switch (target)
        {
            case "Dialog":
                SimulationEventDialogTitle = $"{source.Reference}: {eventName}";
                SimulationEventDialogMessage = message;
                IsSimulationEventDialogVisible = true;
                break;
            case "Waveform drawer":
                var targetComponent = WaveformComponent ?? SelectedComponent;
                if (targetComponent != null)
                    ConfigureWaveformForComponent(targetComponent, showTerminalPicker: false);
                else
                    IsWaveformDrawerOpen = true;
                break;
            case "Description sidebar":
                IsDescriptionSidebarVisible = true;
                break;
            case "Control sidebar":
                IsControlSidebarVisible = true;
                IsSidebarVisible = false;
                break;
            default:
                ShowSimulationEventChat(message);
                break;
        }
        StatusText = $"UI event '{eventName}' fired by {source.Reference}";
    }

    private void ShowSimulationEventChat(string message)
    {
        SimulationEventChatMessage = message;
        IsSimulationEventChatVisible = true;
    }

    // Called by the canvas when user clicks a specific node/pin on a component
    public void OnPinSelected(ComponentNode comp, ComponentPin pin)
    {
        ComponentPinPosition local = AdaptiveComponentLayout.Get(comp).GetPinPosition(pin);
        double radians = comp.Rotation * Math.PI / 180.0;
        double worldX = comp.X + local.X * Math.Cos(radians) - local.Y * Math.Sin(radians);
        double worldY = comp.Y + local.X * Math.Sin(radians) + local.Y * Math.Cos(radians);
        ActiveComponentInfo = $"{comp.Reference}:{pin.Name} (Pin {pin.Id}) @ ({worldX:F0}, {worldY:F0})";
        StatusText = $"Selected node {comp.Reference}:{pin.Name}";
    }

    public void OpenEditDialog(ComponentNode comp)
    {
        EditingComponent = comp;
        RefreshPowerSystemProperties(comp);
        EditName = comp.Reference;
        EditType = comp.ComponentType;
        EditShowLiveProbe = comp.ShowLiveProbe;
        EditShowWaveform = comp.ShowWaveform;
        EditDescription = comp.CustomProperties.TryGetValue("Description", out var dVal) ? dVal?.ToString() ?? comp.Description : comp.Description;
        EditBreakingCurrent = comp.BreakingCurrent;
        SelectedEditTab = "General";

        // Reset component type flags
        IsSourceComponent = comp is VoltageSourceComponent || comp is CurrentSourceComponent;
        IsBjtComponent = comp is EESimulator.Core.Components.Electronics.BJTComponent;
        IsJfetComponent = comp is EESimulator.Core.Components.Electronics.JFETComponent;
        IsMosfetComponent = comp is EESimulator.Core.Components.Electronics.MOSFETComponent;
        IsDiodeComponent = comp is DiodeComponent;
        IsOpAmpComponent = comp is EESimulator.Core.Components.Electronics.OpAmpComponent;
        IsMutualInductanceComponent = comp is MutualInductanceComponent;
        IsResistorComponent = comp is ResistorComponent;
        IsCapacitorComponent = comp is CapacitorComponent;
        IsInductorComponent = comp is InductorComponent;
        IsWireComponent = comp is WireComponent;
        IsSwitchComponent = comp is VoltageSwitchComponent || comp is CurrentSwitchComponent;
        IsControlledSourceComponent = comp is VCVSComponent || comp is VCCSComponent || comp is CCVSComponent || comp is CCCSComponent;
        IsTransmissionLineComponent = comp is TransmissionLineComponent;
        IsVoltageDelayComponent = comp is VoltageDelayComponent;
        IsClockComponent = comp is EESimulator.Core.Components.Electronics.ClockComponent;
        IsPinComponent = comp is EESimulator.Core.Components.Electronics.PinComponent;
        IsPowerComponent = comp is EESimulator.Core.Components.Electrical.PowerComponent;
        IsPullResistorComponent = comp is EESimulator.Core.Components.Electrical.PullResistorComponent;
        IsTunnelComponent = comp is EESimulator.Core.Components.General.TunnelComponent;
        IsProbeComponent = comp is EESimulator.Core.Components.General.ProbeComponent;
        IsDigitalTransistorComponent = comp is EESimulator.Core.Components.Electronics.DigitalTransistorComponent;
        IsTransmissionGateComponent = comp is EESimulator.Core.Components.Electronics.TransmissionGateComponent;
        IsBitExtenderComponent = comp is EESimulator.Core.Components.Electronics.BitExtenderComponent;
        IsConstantComponent = comp is EESimulator.Core.Components.Electronics.ConstantComponent;
        IsDoNotConnectComponent = comp is EESimulator.Core.Components.General.DoNotConnectComponent;
        IsTtlPackageComponent = comp is TtlPackageComponent;
        IsLossyTransmissionLineComponent = comp is EESimulator.Core.Components.Electrical.LossyTransmissionLineComponent;
        IsCoaxialCableComponent = comp is EESimulator.Core.Components.Electrical.CoaxialCableComponent;
        IsMicrostripLineComponent = comp is EESimulator.Core.Components.Electrical.MicrostripLineComponent;

        if (comp is ResistorComponent r)
        {
            EditParamName = "Resistance (Ω)";
            EditParamValue = r.Resistance;
            EditTemperature = r.Temperature;
            EditTempCoeff1 = r.TemperatureCoefficient1;
            EditTempCoeff2 = r.TemperatureCoefficient2;
        }
        else if (comp is CapacitorComponent c)
        {
            EditParamName = "Capacitance (F)";
            EditParamValue = c.Capacitance;
            EditInitialCondition = c.InitialCondition;
            EditEquivalentSeriesResistance = c.EquivalentSeriesResistance;
            EditTemperature = c.Temperature;
            EditTempCoeff1 = c.TemperatureCoefficient1;
            EditTempCoeff2 = c.TemperatureCoefficient2;
        }
        else if (comp is InductorComponent l)
        {
            EditParamName = "Inductance (H)";
            EditParamValue = l.Inductance;
            EditInitialCondition = l.InitialCondition;
            EditDcResistance = l.DcResistance;
        }
        else if (comp is VoltageSourceComponent v)
        {
            EditParamName = "DC Voltage (V)";
            EditParamValue = v.Voltage;
            EditAcMagnitude = v.AcMagnitude;
            EditAcPhase = v.AcPhase;
            EditWaveformType = string.IsNullOrWhiteSpace(v.WaveformType) ? "DC" : v.WaveformType;
            EditSineOffset = v.SineOffset;
            EditSineAmplitude = v.SineAmplitude;
            EditSineFrequency = v.SineFrequency;
            EditSineDelay = v.SineDelay;
            EditSineTheta = v.SineTheta;
            EditSinePhase = v.SinePhase;
            EditPulseInitial = v.PulseInitial;
            EditPulsePulsed = v.PulsePulsed;
            EditPulseDelay = v.PulseDelay;
            EditPulseRiseTime = v.PulseRiseTime;
            EditPulseFallTime = v.PulseFallTime;
            EditPulseWidth = v.PulseWidth;
            EditPulsePeriod = v.PulsePeriod;
        }
        else if (comp is CurrentSourceComponent i)
        {
            EditParamName = "DC Current (A)";
            EditParamValue = i.Current;
            EditAcMagnitude = i.AcMagnitude;
            EditAcPhase = i.AcPhase;
            EditWaveformType = string.IsNullOrWhiteSpace(i.WaveformType) ? "DC" : i.WaveformType;
            EditSineOffset = i.SineOffset;
            EditSineAmplitude = i.SineAmplitude;
            EditSineFrequency = i.SineFrequency;
            EditSineDelay = i.SineDelay;
            EditPulseInitial = i.PulseInitial;
            EditPulsePulsed = i.PulsePulsed;
            EditPulseDelay = i.PulseDelay;
            EditPulseRiseTime = i.PulseRiseTime;
            EditPulseFallTime = i.PulseFallTime;
            EditPulseWidth = i.PulseWidth;
            EditPulsePeriod = i.PulsePeriod;
        }
        else if (comp is DiodeComponent d)
        {
            EditParamName = "Saturation Current Is (A)";
            EditParamValue = d.SaturationCurrent;
            EditSaturationCurrent = d.SaturationCurrent;
            EditEmissionCoefficient = d.EmissionCoefficient;
            EditBreakdownVoltage = d.BreakdownVoltage;
            EditSeriesResistance = d.SeriesResistance;
            EditJunctionCapacitance = d.JunctionCapacitance;
        }
        else if (comp is EESimulator.Core.Components.Electronics.BJTComponent bjt)
        {
            EditParamName = "Gain Beta (Bf)";
            EditParamValue = bjt.GainBeta;
            EditGainBeta = bjt.GainBeta;
            EditSaturationCurrent = bjt.SaturationCurrent;
            EditEarlyVoltage = bjt.EarlyVoltage;
            EditBjtPolarity = bjt.Polarity == EESimulator.Core.Components.Electronics.BjtPolarity.PNP ? "PNP" : "NPN";
        }
        else if (comp is EESimulator.Core.Components.Electronics.MOSFETComponent mos)
        {
            EditParamName = "Threshold Voltage Vto (V)";
            EditParamValue = mos.ThresholdVoltage;
            EditThresholdVoltage = mos.ThresholdVoltage;
            EditTransconductance = mos.Transconductance;
            EditChannelWidth = mos.ChannelWidth;
            EditChannelLength = mos.ChannelLength;
            EditLambda = mos.Lambda;
            EditMosfetChannel = mos.Channel == EESimulator.Core.Components.Electronics.MosfetChannel.PChannel ? "PChannel" : "NChannel";
        }
        else if (comp is EESimulator.Core.Components.Electronics.OpAmpComponent op)
        {
            EditParamName = "Open-Loop Gain Aol";
            EditParamValue = op.OpenLoopGain;
            EditOpenLoopGain = op.OpenLoopGain;
            EditGainBandwidth = op.GainBandwidth;
            EditSlewRate = op.SlewRate;
            EditSupplyPositive = op.SupplyPositive;
            EditSupplyNegative = op.SupplyNegative;
        }
        else if (comp is MutualInductanceComponent m)
        {
            EditParamName = "Coupling Factor k";
            EditParamValue = m.Coupling;
            EditCoupling = m.Coupling;
            EditInductor1 = m.Inductor1;
            EditInductor2 = m.Inductor2;
        }
        else if (comp is EESimulator.Core.Components.Electronics.JFETComponent jfet)
        {
            EditParamName = "Pinch-off Voltage Vto (V)";
            EditParamValue = jfet.ThresholdVoltage;
            EditThresholdVoltage = jfet.ThresholdVoltage;
            EditTransconductance = jfet.Transconductance;
            EditLambda = jfet.Lambda;
            EditJfetChannel = jfet.Channel == EESimulator.Core.Components.Electronics.JfetChannel.PChannel ? "PChannel" : "NChannel";
        }
        else if (comp is VCVSComponent vcvs)
        {
            EditParamName = "Voltage Gain (μ)";
            EditParamValue = vcvs.VoltageGain;
        }
        else if (comp is VCCSComponent vccs)
        {
            EditParamName = "Transconductance Gm (A/V)";
            EditParamValue = vccs.Transconductance;
        }
        else if (comp is CCVSComponent ccvs)
        {
            EditParamName = "Transresistance Rm (V/A)";
            EditParamValue = ccvs.Transresistance;
            EditControllingSource = ccvs.ControllingSource;
        }
        else if (comp is CCCSComponent cccs)
        {
            EditParamName = "Current Gain (β)";
            EditParamValue = cccs.CurrentGain;
            EditControllingSource = cccs.ControllingSource;
        }
        else if (comp is VoltageSwitchComponent vsw)
        {
            EditParamName = "ON Resistance Ron (Ω)";
            EditParamValue = vsw.ResistanceOn;
            EditResistanceOff = vsw.ResistanceOff;
            EditThreshold = vsw.Threshold;
            EditHysteresis = vsw.Hysteresis;
        }
        else if (comp is CurrentSwitchComponent csw)
        {
            EditParamName = "ON Resistance Ron (Ω)";
            EditParamValue = csw.ResistanceOn;
            EditResistanceOff = csw.ResistanceOff;
            EditThreshold = csw.Threshold;
            EditHysteresis = csw.Hysteresis;
            EditControllingSource = csw.ControllingSource;
        }
        else if (comp is TransmissionLineComponent tline)
        {
            EditParamName = "Impedance Z0 (Ω)";
            EditParamValue = tline.CharacteristicImpedance;
            EditCharacteristicImpedance = tline.CharacteristicImpedance;
            EditDelayTime = tline.Delay;
            EditLineFrequency = tline.Frequency;
            EditNormalizedLength = tline.NormalizedLength;
        }
        else if (comp is VoltageDelayComponent vdelay)
        {
            EditParamName = "Delay Time (s)";
            EditParamValue = vdelay.Delay;
            EditDelayTime = vdelay.Delay;
        }
        else if (comp is EESimulator.Core.Components.Electronics.ClockComponent clk)
        {
            EditParamName = "Frequency (Hz)";
            EditParamValue = clk.Frequency;
            EditClockFrequency = clk.Frequency;
            EditClockDutyCycle = clk.DutyCycle;
            EditClockHighVoltage = clk.HighVoltage;
            EditClockLowVoltage = clk.LowVoltage;
            EditClockHighDuration = clk.HighDuration;
            EditClockLowDuration = clk.LowDuration;
            EditClockPhase = clk.Phase;
        }
        else if (comp is EESimulator.Core.Components.Electronics.PinComponent pin)
        {
            EditParamName = "Value / Logic Level";
            EditParamValue = pin.Value;
            EditPinDirection = pin.Direction.ToString();
            EditPinBitWidth = pin.BitWidth;
            EditPinBehavior = pin.Behavior.ToString();
            EditPinInitialValue = pin.InitialValue;
            EditPinRadix = pin.Radix.ToString();
            EditPinHighVoltage = pin.HighVoltage;
            EditPinLowVoltage = pin.LowVoltage;
        }
        else if (comp is EESimulator.Core.Components.Electrical.PowerComponent pw)
        {
            EditParamName = "Rail Voltage (V)";
            EditParamValue = pw.Voltage;
            EditPowerVoltage = pw.Voltage;
        }
        else if (comp is EESimulator.Core.Components.Electrical.PullResistorComponent pr)
        {
            EditParamName = "Resistance (Ω)";
            EditParamValue = pr.Resistance;
            EditPullMode = pr.Mode.ToString();
            EditPullResistance = pr.Resistance;
            EditPullRailVoltage = pr.RailVoltage;
        }
        else if (comp is EESimulator.Core.Components.General.TunnelComponent tun)
        {
            EditParamName = "Tunnel Label";
            EditTunnelLabel = tun.TunnelLabel;
            EditTunnelBitWidth = tun.BitWidth;
        }
        else if (comp is EESimulator.Core.Components.General.ProbeComponent prb)
        {
            EditParamName = "Input Impedance (Ω)";
            EditParamValue = prb.InputImpedance;
            EditProbeRadix = prb.Radix.ToString();
            EditProbeInputImpedance = prb.InputImpedance;
        }
        else if (comp is EESimulator.Core.Components.Electronics.DigitalTransistorComponent dtr)
        {
            EditParamName = "On Resistance Ron (Ω)";
            EditParamValue = dtr.ResistanceOn;
            EditTransistorType = dtr.TransistorType.ToString();
            EditTransistorGateLocation = dtr.GateLocation.ToString();
            EditTransistorBitWidth = dtr.BitWidth;
            EditTransistorRon = dtr.ResistanceOn;
            EditTransistorRoff = dtr.ResistanceOff;
            EditTransistorThreshold = dtr.Threshold;
        }
        else if (comp is EESimulator.Core.Components.Electronics.TransmissionGateComponent tg)
        {
            EditParamName = "On Resistance Ron (Ω)";
            EditParamValue = tg.ResistanceOn;
            EditTransmissionGateSelectLoc = tg.SelectLocation.ToString();
            EditTransmissionGateBitWidth = tg.BitWidth;
            EditTransmissionGateRon = tg.ResistanceOn;
            EditTransmissionGateRoff = tg.ResistanceOff;
        }
        else if (comp is EESimulator.Core.Components.Electronics.BitExtenderComponent be)
        {
            EditParamName = "Output Bit Width";
            EditParamValue = be.OutputBitWidth;
            EditBitExtenderInWidth = be.InputBitWidth;
            EditBitExtenderOutWidth = be.OutputBitWidth;
            EditBitExtenderType = be.ExtensionType.ToString();
        }
        else if (comp is EESimulator.Core.Components.Electronics.ConstantComponent cst)
        {
            EditParamName = "Constant Value";
            EditParamValue = cst.Value;
            EditConstantValue = cst.Value;
            EditConstantBitWidth = cst.BitWidth;
            EditConstantVoltageHigh = cst.VoltageHigh;
            EditConstantVoltageLow = cst.VoltageLow;
        }
        else if (comp is TtlPackageComponent ttl)
        {
            EditParamName = "DIP Pin Count";
            EditParamValue = ttl.PackagePinCount;
            EditTtlExposeSupplyPins = ttl.ExposeSupplyPins;
            EditTtlShowInternalStructure = ttl.ShowInternalStructure;
        }
        else if (comp is EESimulator.Core.Components.General.DoNotConnectComponent dnc)
        {
            EditParamName = "Bit Width";
            EditParamValue = dnc.BitWidth;
            EditDncBitWidth = dnc.BitWidth;
        }
        else if (comp is EESimulator.Core.Components.Electrical.LossyTransmissionLineComponent ltl)
        {
            EditParamName = "Length (m)";
            EditParamValue = ltl.Length;
            EditLossyResistance = ltl.ResistancePerMeter;
            EditLossyInductance = ltl.InductancePerMeter;
            EditLossyConductance = ltl.ConductancePerMeter;
            EditLossyCapacitance = ltl.CapacitancePerMeter;
            EditLossyLength = ltl.Length;
        }
        else if (comp is EESimulator.Core.Components.Electrical.CoaxialCableComponent coax)
        {
            EditParamName = "Impedance Z0 (Ω)";
            EditParamValue = coax.CharacteristicImpedance;
            EditCoaxZ0 = coax.CharacteristicImpedance;
            EditCoaxVF = coax.VelocityFactor;
            EditCoaxLength = coax.Length;
            EditCoaxAttenuation = coax.Attenuation;
        }
        else if (comp is EESimulator.Core.Components.Electrical.MicrostripLineComponent ustrip)
        {
            EditParamName = "Trace Width (m)";
            EditParamValue = ustrip.TraceWidth;
            EditMicrostripWidth = ustrip.TraceWidth;
            EditMicrostripHeight = ustrip.SubstrateHeight;
            EditMicrostripEr = ustrip.DielectricConstant;
            EditMicrostripLength = ustrip.Length;
        }
        else if (comp is WireComponent w)
        {
            EditParamName = "Parasitic Resistance (Ω)";
            EditParamValue = w.Resistance;
            EditWireResistance = w.Resistance.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (comp is EESimulator.Core.Components.Electrical.BulbComponent bulb)
        {
            EditParamName = "Filament Resistance (Ω)";
            EditParamValue = bulb.Resistance;
        }
        else if (comp is EESimulator.Core.Components.Electronics.LEDComponent led)
        {
            EditParamName = "Forward Voltage Vf (V)";
            EditParamValue = led.ForwardVoltage;
        }
        else if (comp is EESimulator.Core.Components.Electronics.LogicInputComponent lin)
        {
            EditParamName = "Logic 1 High Voltage (V)";
            EditParamValue = lin.HighVoltage;
        }
        else if (comp is EESimulator.Core.Components.Electronics.LogicOutputComponent lout)
        {
            EditParamName = "Logic Switching Threshold (V)";
            EditParamValue = lout.Threshold;
        }
        else if (comp.ElectricalParameters.Count > 0)
        {
            var first = comp.ElectricalParameters.First();
            EditParamName = first.Key;
            EditParamValue = first.Value;
        }
        else
        {
            EditParamName = "Parameter";
            EditParamValue = 0;
        }

        EditPins.Clear();
        foreach (var pin in comp.Pins)
        {
            EditPins.Add(new PinEditItem(pin.Id, pin.Name));
        }

        IsEditDialogVisible = true;
    }


}
