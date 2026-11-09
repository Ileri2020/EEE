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
    private void RefreshPowerSystemProperties(ComponentNode component)
    {
        PowerSystemProperties.Clear();
        if (component is not PowerSystemComponent) return;

        var type = component.GetType();
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod?.IsPublic == true &&
                               property.Name is not ("ComponentType" or "StableTypeId" or "SpicePrefix" or "Category" or "ModelName" or "SymbolKey" or "IsSimulatable" or
                                   "Description" or "Pins" or "Children" or "Parent" or "ElectricalParameters" or "CustomProperties" or "EventTriggers" or
                                   "WaveformTime" or "WaveformVoltage" or "WaveformCurrent" or "WaveformNodeVoltages" or "Position" or "Id" or "Name" or "Reference" or
                                   "NodeType" or "Tags" or "UserData" or "X" or "Y" or "Rotation" or "ScaleX" or "ScaleY" or "Enabled" or "Visible" or
                                   "Locked" or "Selected" or "ShowLiveProbe" or "ShowWaveform" or "LiveVoltage" or "LiveCurrent" or "BreakingCurrent" or "IsBrokenDown"))
            .OrderBy(property => property.SetMethod?.IsPublic == true ? 0 : 1)
            .ThenBy(property => property.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var property in properties)
        {
            try
            {
                object? value = property.GetValue(component);
                var item = new PowerSystemPropertyItem
                {
                    Name = property.Name,
                    TypeName = property.PropertyType.Name,
                    Value = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    IsEditable = property.SetMethod?.IsPublic == true && property.PropertyType.IsPrimitive ||
                                 property.SetMethod?.IsPublic == true && property.PropertyType == typeof(string)
                };
                item.ReturnCommand = new RelayCommand(() => ApplyPowerSystemProperty(item));
                PowerSystemProperties.Add(item);
            }
            catch
            {
                // Skip a property that cannot currently be evaluated for the inspector.
            }
        }
    }

    [RelayCommand]
    public void ApplyPowerSystemProperty(PowerSystemPropertyItem item)
    {
        if (EditingComponent is not PowerSystemComponent component || item == null || !item.IsEditable) return;
        var property = component.GetType().GetProperty(item.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property?.SetMethod?.IsPublic != true) return;
        try
        {
            object converted = property.PropertyType == typeof(string)
                ? item.Value
                : Convert.ChangeType(item.Value, property.PropertyType, System.Globalization.CultureInfo.InvariantCulture);
            property.SetValue(component, converted);
            component.ValidateConfiguration();
            component.NotifyModified(item.Name);
            RefreshPowerSystemProperties(component);
            CircuitChanged?.Invoke();
            RequestSimulationRefresh();
            StatusText = $"Updated {component.Reference}.{item.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"Invalid {item.Name}: {ex.Message}";
            RefreshPowerSystemProperties(component);
        }
    }

    [RelayCommand]
    private void CloseEditDialog()
    {
        IsEditDialogVisible = false;
    }

    [RelayCommand]
    private void ToggleEditLiveProbe()
    {
        EditShowLiveProbe = !EditShowLiveProbe;
        if (EditingComponent != null)
        {
            EditingComponent.ShowLiveProbe = EditShowLiveProbe;
        }
    }

    [RelayCommand]
    private void ToggleEditWaveform()
    {
        EditShowWaveform = !EditShowWaveform;
        if (EditingComponent != null)
        {
            EditingComponent.ShowWaveform = EditShowWaveform;
            if (EditShowWaveform)
            {
                ConfigureWaveformForComponent(EditingComponent, showTerminalPicker: false);
            }
        }
    }

    [RelayCommand]
    private void ApplyEditDialog()
    {
        if (EditingComponent == null)
        {
            IsEditDialogVisible = false;
            return;
        }

        if (!string.IsNullOrWhiteSpace(EditName))
        {
            EditingComponent.Name = EditName.Trim();
            EditingComponent.Reference = EditName.Trim();
        }

        EditingComponent.CustomProperties["Description"] = EditDescription;
        EditingComponent.BreakingCurrent = Math.Max(EditBreakingCurrent, 0.0);

        if (EditingComponent is ResistorComponent r)
        {
            r.Resistance = Math.Max(EditParamValue, 1e-6);
            r.Temperature = EditTemperature;
            r.TemperatureCoefficient1 = EditTempCoeff1;
            r.TemperatureCoefficient2 = EditTempCoeff2;
        }
        else if (EditingComponent is CapacitorComponent c)
        {
            c.Capacitance = Math.Max(EditParamValue, 1e-15);
            c.InitialCondition = EditInitialCondition;
            c.EquivalentSeriesResistance = Math.Max(EditEquivalentSeriesResistance, 0.0);
            c.Temperature = EditTemperature;
            c.TemperatureCoefficient1 = EditTempCoeff1;
            c.TemperatureCoefficient2 = EditTempCoeff2;
        }
        else if (EditingComponent is InductorComponent l)
        {
            l.Inductance = Math.Max(EditParamValue, 1e-15);
            l.InitialCondition = EditInitialCondition;
            l.DcResistance = Math.Max(EditDcResistance, 0.0);
        }
        else if (EditingComponent is VoltageSourceComponent v)
        {
            v.Voltage = EditParamValue;
            v.AcMagnitude = EditAcMagnitude;
            v.AcPhase = EditAcPhase;
            v.WaveformType = string.IsNullOrWhiteSpace(EditWaveformType) ? "DC" : EditWaveformType;
            v.SineOffset = EditSineOffset;
            v.SineAmplitude = EditSineAmplitude;
            v.SineFrequency = Math.Max(EditSineFrequency, 0.1);
            v.SineDelay = EditSineDelay;
            v.SineTheta = EditSineTheta;
            v.SinePhase = EditSinePhase;
            v.PulseInitial = EditPulseInitial;
            v.PulsePulsed = EditPulsePulsed;
            v.PulseDelay = EditPulseDelay;
            v.PulseRiseTime = Math.Max(EditPulseRiseTime, 1e-12);
            v.PulseFallTime = Math.Max(EditPulseFallTime, 1e-12);
            v.PulseWidth = Math.Max(EditPulseWidth, 1e-12);
            v.PulsePeriod = Math.Max(EditPulsePeriod, 1e-12);
        }
        else if (EditingComponent is CurrentSourceComponent i)
        {
            i.Current = EditParamValue;
            i.AcMagnitude = EditAcMagnitude;
            i.AcPhase = EditAcPhase;
            i.WaveformType = string.IsNullOrWhiteSpace(EditWaveformType) ? "DC" : EditWaveformType;
            i.SineOffset = EditSineOffset;
            i.SineAmplitude = EditSineAmplitude;
            i.SineFrequency = Math.Max(EditSineFrequency, 0.1);
            i.SineDelay = EditSineDelay;
            i.PulseInitial = EditPulseInitial;
            i.PulsePulsed = EditPulsePulsed;
            i.PulseDelay = EditPulseDelay;
            i.PulseRiseTime = Math.Max(EditPulseRiseTime, 1e-12);
            i.PulseFallTime = Math.Max(EditPulseFallTime, 1e-12);
            i.PulseWidth = Math.Max(EditPulseWidth, 1e-12);
            i.PulsePeriod = Math.Max(EditPulsePeriod, 1e-12);
        }
        else if (EditingComponent is DiodeComponent d)
        {
            d.SaturationCurrent = EditSaturationCurrent > 0 ? EditSaturationCurrent : Math.Max(EditParamValue, 1e-16);
            d.EmissionCoefficient = Math.Max(EditEmissionCoefficient, 0.1);
            d.BreakdownVoltage = EditBreakdownVoltage;
            d.SeriesResistance = Math.Max(EditSeriesResistance, 0.0);
            d.JunctionCapacitance = Math.Max(EditJunctionCapacitance, 0.0);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.BJTComponent bjt)
        {
            bjt.GainBeta = EditGainBeta > 0 ? EditGainBeta : Math.Max(EditParamValue, 1.0);
            bjt.SaturationCurrent = Math.Max(EditSaturationCurrent, 1e-16);
            bjt.EarlyVoltage = EditEarlyVoltage;
            bjt.Polarity = EditBjtPolarity.Equals("PNP", StringComparison.OrdinalIgnoreCase)
                ? EESimulator.Core.Components.Electronics.BjtPolarity.PNP
                : EESimulator.Core.Components.Electronics.BjtPolarity.NPN;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.MOSFETComponent mos)
        {
            mos.ThresholdVoltage = EditThresholdVoltage != 0 ? EditThresholdVoltage : EditParamValue;
            mos.Transconductance = Math.Max(EditTransconductance, 1e-9);
            mos.ChannelWidth = Math.Max(EditChannelWidth, 1e-9);
            mos.ChannelLength = Math.Max(EditChannelLength, 1e-9);
            mos.Lambda = EditLambda;
            mos.Channel = EditMosfetChannel.Equals("PChannel", StringComparison.OrdinalIgnoreCase)
                ? EESimulator.Core.Components.Electronics.MosfetChannel.PChannel
                : EESimulator.Core.Components.Electronics.MosfetChannel.NChannel;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.OpAmpComponent op)
        {
            op.OpenLoopGain = EditOpenLoopGain > 0 ? EditOpenLoopGain : Math.Max(EditParamValue, 1.0);
            op.GainBandwidth = Math.Max(EditGainBandwidth, 1.0);
            op.SlewRate = Math.Max(EditSlewRate, 0.001);
            op.SupplyPositive = EditSupplyPositive;
            op.SupplyNegative = EditSupplyNegative;
        }
        else if (EditingComponent is MutualInductanceComponent m)
        {
            m.Coupling = Math.Clamp(EditCoupling > 0 ? EditCoupling : EditParamValue, 0.0, 1.0);
            if (!string.IsNullOrWhiteSpace(EditInductor1)) m.Inductor1 = EditInductor1.Trim();
            if (!string.IsNullOrWhiteSpace(EditInductor2)) m.Inductor2 = EditInductor2.Trim();
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.JFETComponent jfet)
        {
            jfet.ThresholdVoltage = EditThresholdVoltage != 0 ? EditThresholdVoltage : EditParamValue;
            jfet.Transconductance = Math.Max(EditTransconductance, 1e-9);
            jfet.Lambda = EditLambda;
            jfet.Channel = EditJfetChannel.Equals("PChannel", StringComparison.OrdinalIgnoreCase)
                ? EESimulator.Core.Components.Electronics.JfetChannel.PChannel
                : EESimulator.Core.Components.Electronics.JfetChannel.NChannel;
        }
        else if (EditingComponent is VCVSComponent vcvs)
        {
            vcvs.VoltageGain = EditParamValue;
        }
        else if (EditingComponent is VCCSComponent vccs)
        {
            vccs.Transconductance = EditParamValue;
        }
        else if (EditingComponent is CCVSComponent ccvs)
        {
            ccvs.Transresistance = EditParamValue;
            ccvs.ControllingSource = EditControllingSource;
        }
        else if (EditingComponent is CCCSComponent cccs)
        {
            cccs.CurrentGain = EditParamValue;
            cccs.ControllingSource = EditControllingSource;
        }
        else if (EditingComponent is VoltageSwitchComponent vsw)
        {
            vsw.ResistanceOn = Math.Max(EditParamValue, 1e-9);
            vsw.ResistanceOff = Math.Max(EditResistanceOff, 1e-3);
            vsw.Threshold = EditThreshold;
            vsw.Hysteresis = EditHysteresis;
        }
        else if (EditingComponent is CurrentSwitchComponent csw)
        {
            csw.ResistanceOn = Math.Max(EditParamValue, 1e-9);
            csw.ResistanceOff = Math.Max(EditResistanceOff, 1e-3);
            csw.Threshold = EditThreshold;
            csw.Hysteresis = EditHysteresis;
            csw.ControllingSource = EditControllingSource;
        }
        else if (EditingComponent is TransmissionLineComponent tline)
        {
            tline.CharacteristicImpedance = EditCharacteristicImpedance > 0 ? EditCharacteristicImpedance : Math.Max(EditParamValue, 1e-3);
            tline.Delay = Math.Max(EditDelayTime, 1e-15);
            tline.Frequency = Math.Max(EditLineFrequency, 1.0);
            tline.NormalizedLength = Math.Max(EditNormalizedLength, 0.0);
        }
        else if (EditingComponent is VoltageDelayComponent vdelay)
        {
            vdelay.Delay = Math.Max(EditParamValue > 0 ? EditParamValue : EditDelayTime, 1e-15);
        }
        else if (EditingComponent is WireComponent w)
        {
            w.Resistance = double.TryParse(EditWireResistance, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var wr) ? wr : Math.Max(EditParamValue, 0);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.BulbComponent bulb)
        {
            bulb.Resistance = Math.Max(EditParamValue, 1e-6);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.LEDComponent led)
        {
            led.ForwardVoltage = Math.Max(EditParamValue, 0.1);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.LogicInputComponent lin)
        {
            lin.HighVoltage = EditParamValue;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.LogicOutputComponent lout)
        {
            lout.Threshold = EditParamValue;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.ClockComponent clk)
        {
            clk.Frequency = EditClockFrequency > 0 ? EditClockFrequency : Math.Max(EditParamValue, 0.01);
            clk.DutyCycle = Math.Clamp(EditClockDutyCycle, 0.01, 0.99);
            clk.HighVoltage = EditClockHighVoltage;
            clk.LowVoltage = EditClockLowVoltage;
            clk.HighDuration = Math.Max(EditClockHighDuration, 1);
            clk.LowDuration = Math.Max(EditClockLowDuration, 1);
            clk.Phase = Math.Max(EditClockPhase, 0);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.PinComponent pin)
        {
            pin.Value = (long)EditParamValue;
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.PinDirection>(EditPinDirection, true, out var pdir))
                pin.Direction = pdir;
            pin.BitWidth = Math.Clamp(EditPinBitWidth, 1, 32);
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.PinBehavior>(EditPinBehavior, true, out var pbeh))
                pin.Behavior = pbeh;
            pin.InitialValue = EditPinInitialValue;
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.DisplayRadix>(EditPinRadix, true, out var prad))
                pin.Radix = prad;
            pin.HighVoltage = EditPinHighVoltage;
            pin.LowVoltage = EditPinLowVoltage;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.PowerComponent pw)
        {
            pw.Voltage = EditPowerVoltage != 0 ? EditPowerVoltage : EditParamValue;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.PullResistorComponent pr)
        {
            pr.Resistance = EditPullResistance > 0 ? EditPullResistance : Math.Max(EditParamValue, 1.0);
            pr.RailVoltage = EditPullRailVoltage;
            if (Enum.TryParse<EESimulator.Core.Components.Electrical.PullResistorMode>(EditPullMode, true, out var prm))
                pr.Mode = prm;
        }
        else if (EditingComponent is EESimulator.Core.Components.General.TunnelComponent tun)
        {
            if (!string.IsNullOrWhiteSpace(EditTunnelLabel))
                tun.TunnelLabel = EditTunnelLabel.Trim();
            tun.BitWidth = Math.Clamp(EditTunnelBitWidth, 1, 32);
        }
        else if (EditingComponent is EESimulator.Core.Components.General.ProbeComponent prb)
        {
            prb.InputImpedance = EditProbeInputImpedance > 0 ? EditProbeInputImpedance : Math.Max(EditParamValue, 1e3);
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.DisplayRadix>(EditProbeRadix, true, out var prad))
                prb.Radix = prad;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.DigitalTransistorComponent dtr)
        {
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.DigitalTransistorType>(EditTransistorType, true, out var dtt))
                dtr.TransistorType = dtt;
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.GateLocation>(EditTransistorGateLocation, true, out var gloc))
                dtr.GateLocation = gloc;
            dtr.BitWidth = Math.Clamp(EditTransistorBitWidth, 1, 32);
            dtr.ResistanceOn = EditTransistorRon > 0 ? EditTransistorRon : Math.Max(EditParamValue, 1e-4);
            dtr.ResistanceOff = Math.Max(EditTransistorRoff, 100.0);
            dtr.Threshold = EditTransistorThreshold;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.TransmissionGateComponent tg)
        {
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.GateLocation>(EditTransmissionGateSelectLoc, true, out var gloc))
                tg.SelectLocation = gloc;
            tg.BitWidth = Math.Clamp(EditTransmissionGateBitWidth, 1, 32);
            tg.ResistanceOn = EditTransmissionGateRon > 0 ? EditTransmissionGateRon : Math.Max(EditParamValue, 1e-4);
            tg.ResistanceOff = Math.Max(EditTransmissionGateRoff, 100.0);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.BitExtenderComponent be)
        {
            be.InputBitWidth = Math.Clamp(EditBitExtenderInWidth, 1, 32);
            be.OutputBitWidth = Math.Clamp(EditBitExtenderOutWidth, 1, 64);
            if (Enum.TryParse<EESimulator.Core.Components.Electronics.BitExtensionType>(EditBitExtenderType, true, out var bet))
                be.ExtensionType = bet;
        }
        else if (EditingComponent is EESimulator.Core.Components.Electronics.ConstantComponent cst)
        {
            cst.Value = EditConstantValue != 0 ? EditConstantValue : (long)EditParamValue;
            cst.BitWidth = Math.Clamp(EditConstantBitWidth, 1, 32);
            cst.VoltageHigh = EditConstantVoltageHigh;
            cst.VoltageLow = EditConstantVoltageLow;
        }
        else if (EditingComponent is TtlPackageComponent ttl)
        {
            ttl.ExposeSupplyPins = EditTtlExposeSupplyPins;
            ttl.ShowInternalStructure = EditTtlShowInternalStructure;
        }
        else if (EditingComponent is EESimulator.Core.Components.General.DoNotConnectComponent dnc)
        {
            dnc.BitWidth = Math.Clamp(EditDncBitWidth > 0 ? EditDncBitWidth : (int)EditParamValue, 1, 32);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.LossyTransmissionLineComponent ltl)
        {
            ltl.Length = EditLossyLength > 0 ? EditLossyLength : Math.Max(EditParamValue, 0.001);
            ltl.ResistancePerMeter = Math.Max(EditLossyResistance, 0.0);
            ltl.InductancePerMeter = Math.Max(EditLossyInductance, 1e-12);
            ltl.ConductancePerMeter = Math.Max(EditLossyConductance, 0.0);
            ltl.CapacitancePerMeter = Math.Max(EditLossyCapacitance, 1e-15);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.CoaxialCableComponent coax)
        {
            coax.CharacteristicImpedance = EditCoaxZ0 > 0 ? EditCoaxZ0 : Math.Max(EditParamValue, 1.0);
            coax.VelocityFactor = Math.Clamp(EditCoaxVF, 0.1, 1.0);
            coax.Length = Math.Max(EditCoaxLength, 0.01);
            coax.Attenuation = Math.Max(EditCoaxAttenuation, 0.0);
        }
        else if (EditingComponent is EESimulator.Core.Components.Electrical.MicrostripLineComponent ustrip)
        {
            ustrip.TraceWidth = EditMicrostripWidth > 0 ? EditMicrostripWidth : Math.Max(EditParamValue, 1e-6);
            ustrip.SubstrateHeight = Math.Max(EditMicrostripHeight, 1e-6);
            ustrip.DielectricConstant = Math.Max(EditMicrostripEr, 1.0);
            ustrip.Length = Math.Max(EditMicrostripLength, 1e-4);
        }
        else if (!string.IsNullOrEmpty(EditParamName))
        {
            EditingComponent.ElectricalParameters[EditParamName] = EditParamValue;
        }

        // Apply node / pin name changes
        foreach (var pItem in EditPins)
        {
            var targetPin = EditingComponent.Pins.FirstOrDefault(p => p.Id == pItem.Id);
            if (targetPin != null && !string.IsNullOrWhiteSpace(pItem.Name))
            {
                targetPin.Name = pItem.Name.Trim();
            }
        }

        EditingComponent.ShowLiveProbe = EditShowLiveProbe;
        EditingComponent.ShowWaveform = EditShowWaveform;

        if (EditShowWaveform)
        {
            ConfigureWaveformForComponent(EditingComponent, showTerminalPicker: false);
        }

        EditingComponent.NotifyModified(EditingComponent.Reference);
        IsEditDialogVisible = false;
        ApplyLogicalSchematicLayout();
        StatusText = $"Updated {EditingComponent.Reference}";
        CircuitChanged?.Invoke();

        // Trigger simulation to update live probe values
        _ = SimulateAsync();
    }

    [RelayCommand]
    private void DeleteEditingComponent()
    {
        if (EditingComponent != null)
        {
            Circuit.RemoveChild(EditingComponent);
            if (WaveformComponent == EditingComponent)
            {
                WaveformComponent = null;
                IsWaveformDrawerOpen = false;
            }
            StatusText = $"Deleted {EditingComponent.Reference}";
            EditingComponent = null;
        }
        IsEditDialogVisible = false;
    }

    [RelayCommand]
    private void ToggleWaveformDrawer() => IsWaveformDrawerOpen = !IsWaveformDrawerOpen;

    [RelayCommand]
    private void CloseWaveformDrawer() => IsWaveformDrawerOpen = false;

    // ── Helpers ───────────────────────────────────────────────────────────────
    private string GetNextReference(string type)
    {
        string prefix = type switch
        {
            "Resistor"       => "R",
            "Capacitor"      => "C",
            "Inductor"       => "L",
            "VoltageSource"  => "V",
            "CurrentSource"  => "I",
            "Ground"         => "GND",
            "Diode"          => "D",
            "LED"            => "LED",
            "Bulb" or "Lamp" => "LAMP",
            "LogicInput"     => "IN",
            "LogicOutput"    => "OUT",
            "BJT" or "NPN" or "PNP" => "Q",
            "JFET"           => "J",
            "MOSFET" or "NMOS" or "PMOS" => "M",
            "OpAmp"          => "U",
            "VCVS"           => "E",
            "VCCS"           => "G",
            "CCVS"           => "H",
            "CCCS"           => "F",
            "VoltageSwitch"  => "S",
            "CurrentSwitch"  => "W",
            "TransmissionLine" => "T",
            "VoltageDelay"   => "VD",
            "LogicGate"      => "U",
            "AND"            => "U",
            "OR"             => "U",
            "NOT"            => "U",
            "NAND"           => "U",
            "NOR"            => "U",
            "XOR"            => "U",
            "XNOR"           => "U",
            "IC"             => "U",
            "Wire"           => "W",
            "Junction"       => "J",
            "Label"          => "LBL",
            "TextAnnotation" => "TXT",
            "Connector"      => "J",
            _                => EESimulator.Core.Components.Registry.ComponentRegistry.TryGetDefinition(type, out var def) ? def.ReferencePrefix : "X"
        };

        int n = 1;
        while (Circuit.FindChildren<ComponentNode>().Any(c => c.Reference == $"{prefix}{n}"))
            n++;
        return $"{prefix}{n}";
    }


}
