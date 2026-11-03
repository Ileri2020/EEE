using System;
using System.Globalization;
using EESimulator.Core.Models;

namespace EESimulator.Commands.Commands;

/// <summary>
/// Command to add an electrical component to the circuit hierarchy.
/// </summary>
public class AddComponentCommand : ICommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Description => $"Add component {ComponentName} ({ComponentType})";
    public CommandOrigin Origin { get; set; } = CommandOrigin.Schematic;

    public CircuitNode Circuit { get; }
    public string ComponentType { get; }
    public string ComponentName { get; }
    public double X { get; }
    public double Y { get; }
    public double ParameterValue { get; }

    public ComponentNode? CreatedComponent { get; private set; }

    public AddComponentCommand(CircuitNode circuit, string componentType, string componentName, double x, double y, double parameterValue = 0)
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        ComponentType = componentType;
        ComponentName = componentName;
        X = x;
        Y = y;
        ParameterValue = parameterValue;
    }

    public AddComponentCommand(CircuitNode circuit, ComponentNode component)
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        CreatedComponent = component ?? throw new ArgumentNullException(nameof(component));
        ComponentType = component.ComponentType;
        ComponentName = component.Name;
        X = component.X;
        Y = component.Y;
    }

    public bool Execute()
    {
        if (CreatedComponent == null)
        {
            CreatedComponent = ComponentType.ToLowerInvariant() switch
            {
                "resistor" => new ResistorNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Resistance = ParameterValue > 0 ? ParameterValue : 1000.0
                },
                "capacitor" => new CapacitorNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Capacitance = ParameterValue > 0 ? ParameterValue : 1e-6
                },
                "inductor" => new InductorNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Inductance = ParameterValue > 0 ? ParameterValue : 1e-3
                },
                "voltagesource" or "vsource" => new VoltageSourceNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Voltage = ParameterValue != 0 ? ParameterValue : 5.0
                },
                "currentsource" or "isource" => new CurrentSourceNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Current = ParameterValue != 0 ? ParameterValue : 0.001
                },
                "ground" or "gnd" => new GroundNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y
                },
                "mutualinductance" or "mutual" or "k" => new MutualInductanceNode
                {
                    Name = ComponentName,
                    Reference = ComponentName,
                    X = X,
                    Y = Y,
                    Coupling = ParameterValue > 0 ? Math.Clamp(ParameterValue, 0.0, 1.0) : 0.99
                },
                _ => EESimulator.Core.Components.Registry.ComponentRegistry.TryGetDefinition(ComponentType, out _)
                    ? EESimulator.Core.Components.Registry.ComponentRegistry.CreateInstance(ComponentType, ComponentName, X, Y)
                    : throw new ArgumentException($"Unknown component type: {ComponentType}")
            };

            if (ParameterValue != 0)
            {
                if (CreatedComponent is EESimulator.Core.Components.Electrical.VCVSComponent vcvs) vcvs.VoltageGain = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.VCCSComponent vccs) vccs.Transconductance = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.CCVSComponent ccvs) ccvs.Transresistance = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.CCCSComponent cccs) cccs.CurrentGain = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.VoltageSwitchComponent vsw) vsw.ResistanceOn = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.CurrentSwitchComponent csw) csw.ResistanceOn = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electronics.JFETComponent jfet) jfet.ThresholdVoltage = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.DiodeComponent diode && ParameterValue > 0) diode.SaturationCurrent = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electronics.BJTComponent bjt && ParameterValue > 0) bjt.GainBeta = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electronics.MOSFETComponent mos) mos.ThresholdVoltage = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electronics.OpAmpComponent op && ParameterValue > 0) op.OpenLoopGain = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.TransmissionLineComponent tline && ParameterValue > 0) tline.CharacteristicImpedance = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.VoltageDelayComponent vdelay && ParameterValue > 0) vdelay.Delay = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electronics.ClockComponent clk && ParameterValue > 0) clk.Frequency = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electronics.PinComponent pin) pin.Value = (long)ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.PowerComponent pw) pw.Voltage = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.PullResistorComponent pr && ParameterValue > 0) pr.Resistance = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.LossyTransmissionLineComponent ltl && ParameterValue > 0) ltl.Length = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.CoaxialCableComponent coax && ParameterValue > 0) coax.CharacteristicImpedance = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.MicrostripLineComponent ustrip && ParameterValue > 0) ustrip.TraceWidth = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electrical.BulbComponent bulb && ParameterValue > 0) bulb.Resistance = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electronics.LEDComponent led && ParameterValue > 0) led.ForwardVoltage = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.General.WireComponent wire && ParameterValue > 0) wire.Resistance = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.General.ProbeComponent prb && ParameterValue > 0) prb.InputImpedance = ParameterValue;
                else if (CreatedComponent is EESimulator.Core.Components.Electronics.LogicInputComponent lin) lin.State = ParameterValue > 0.5;
                else if (CreatedComponent is EESimulator.Core.Components.Electronics.LogicOutputComponent lout && ParameterValue > 0) lout.Threshold = ParameterValue;
            }
        }

        if (CreatedComponent is EESimulator.Core.Components.Electrical.PowerSystemComponent powerSystemComponent)
            powerSystemComponent.ValidateConfiguration();

        Circuit.AddChild(CreatedComponent);
        return true;
    }

    public bool Undo()
    {
        if (CreatedComponent != null)
        {
            return Circuit.RemoveChild(CreatedComponent);
        }
        return false;
    }

    public string ToPythonScript()
    {
        var invCulture = CultureInfo.InvariantCulture;
        var paramArg = ComponentType.ToLowerInvariant() switch
        {
            "resistor" => $", resistance={ParameterValue.ToString(invCulture)}",
            "capacitor" => $", capacitance={ParameterValue.ToString(invCulture)}",
            "inductor" => $", inductance={ParameterValue.ToString(invCulture)}",
            "voltagesource" or "vsource" => $", voltage={ParameterValue.ToString(invCulture)}",
            "currentsource" or "isource" => $", current={ParameterValue.ToString(invCulture)}",
            "mutualinductance" or "mutual" or "k" => $", coupling={ParameterValue.ToString(invCulture)}",
            "vcvs" or "electrical.vcvs" => $", gain={ParameterValue.ToString(invCulture)}",
            "vccs" or "electrical.vccs" => $", gm={ParameterValue.ToString(invCulture)}",
            "ccvs" or "electrical.ccvs" => $", rm={ParameterValue.ToString(invCulture)}",
            "cccs" or "electrical.cccs" => $", gain={ParameterValue.ToString(invCulture)}",
            "voltageswitch" or "electrical.voltageswitch" => $", ron={ParameterValue.ToString(invCulture)}",
            "currentswitch" or "electrical.currentswitch" => $", ron={ParameterValue.ToString(invCulture)}",
            "transmissionline" or "tline" or "electrical.transmissionline" => $", z0={ParameterValue.ToString(invCulture)}",
            "voltagedelay" or "vdelay" or "electrical.voltagedelay" => $", delay={ParameterValue.ToString(invCulture)}",
            "jfet" or "electronics.jfet" => $", vto={ParameterValue.ToString(invCulture)}",
            "bjt" or "electronics.bjt" or "npn" or "pnp" => $", bf={ParameterValue.ToString(invCulture)}",
            "mosfet" or "electronics.mosfet" or "nmos" or "pmos" => $", vto={ParameterValue.ToString(invCulture)}",
            "diode" or "electrical.diode" => $", isat={ParameterValue.ToString(invCulture)}",
            "opamp" or "electronics.opamp" => $", aol={ParameterValue.ToString(invCulture)}",
            _ => ParameterValue != 0 ? $", param={ParameterValue.ToString(invCulture)}" : ""
        };

        var varName = ComponentName.ToLowerInvariant();
        return $"{varName} = circuit.add_component(\"{ComponentType}\", name=\"{ComponentName}\", position=({X.ToString(invCulture)}, {Y.ToString(invCulture)}){paramArg})";
    }
}
