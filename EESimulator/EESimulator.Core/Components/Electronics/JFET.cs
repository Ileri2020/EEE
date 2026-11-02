using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

public enum JfetChannel
{
    NChannel,
    PChannel
}

/// <summary>
/// Junction Field-Effect Transistor (JFET - N-channel / P-channel).
/// SPICE Prefix: J.
/// Shichman-Hodges JFET model.
/// </summary>
public class JFETComponent : ElectronicComponent
{
    public const string StableType = "electronics.jfet";

    public override string StableTypeId => StableType;
    public override string ComponentType => "JFET";
    public override string SpicePrefix => "J";
    public override string Description => "Junction Field-Effect Transistor (JFET)";

    public JfetChannel Channel { get; set; } = JfetChannel.NChannel;

    public double ThresholdVoltage
    {
        get => ElectricalParameters.TryGetValue("Vto", out var val) ? val : (Channel == JfetChannel.NChannel ? -2.0 : 2.0);
        set => ElectricalParameters["Vto"] = value;
    }

    public double Transconductance
    {
        get => ElectricalParameters.TryGetValue("Beta", out var val) ? val : 1e-4;
        set => ElectricalParameters["Beta"] = value;
    }

    public double Lambda
    {
        get => ElectricalParameters.TryGetValue("Lambda", out var val) ? val : 0.0;
        set => ElectricalParameters["Lambda"] = value;
    }

    public double DrainResistance
    {
        get => ElectricalParameters.TryGetValue("Rd", out var val) ? val : 0.0;
        set => ElectricalParameters["Rd"] = value;
    }

    public double SourceResistance
    {
        get => ElectricalParameters.TryGetValue("Rs", out var val) ? val : 0.0;
        set => ElectricalParameters["Rs"] = value;
    }

    public double CapGs
    {
        get => ElectricalParameters.TryGetValue("Cgs", out var val) ? val : 0.0;
        set => ElectricalParameters["Cgs"] = value;
    }

    public double CapGd
    {
        get => ElectricalParameters.TryGetValue("Cgd", out var val) ? val : 0.0;
        set => ElectricalParameters["Cgd"] = value;
    }

    public JFETComponent()
    {
        // Pin layout: Gate (left), Drain (top-right), Source (bottom-right)
        Pins.Add(new ComponentPin("gate", "G", -20, 0));
        Pins.Add(new ComponentPin("drain", "D", 15, -20));
        Pins.Add(new ComponentPin("source", "S", 15, 20));

        ThresholdVoltage = -2.0;
        Transconductance = 1e-4;
        Lambda = 0.0;
        DrainResistance = 0.0;
        SourceResistance = 0.0;
        CapGs = 0.0;
        CapGd = 0.0;
        PartNumber = "2N5457";
    }

    public JFETComponent(string name, JfetChannel channel = JfetChannel.NChannel) : this()
    {
        Name = name;
        Reference = name;
        Channel = channel;
        if (channel == JfetChannel.PChannel)
        {
            ThresholdVoltage = 2.0;
            PartNumber = "2N5460";
        }
    }
}
