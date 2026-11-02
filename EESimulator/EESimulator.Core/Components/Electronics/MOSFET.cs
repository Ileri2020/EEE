using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

public enum MosfetChannel
{
    NChannel,
    PChannel
}

/// <summary>
/// Metal-Oxide-Semiconductor Field-Effect Transistor (N-channel / P-channel).
/// </summary>
public class MOSFETComponent : ElectronicComponent
{
    public const string StableType = "electronics.mosfet";

    public override string StableTypeId => StableType;
    public override string ComponentType => "MOSFET";
    public override string Description => "MOSFET Field-Effect Transistor";

    private MosfetChannel _channel = MosfetChannel.NChannel;
    public MosfetChannel Channel
    {
        get => _channel;
        set
        {
            _channel = value;
            NotifyModified(nameof(Channel));
        }
    }

    public double ThresholdVoltage
    {
        get => ElectricalParameters.TryGetValue("Vto", out var val) ? val : 2.0;
        set
        {
            ElectricalParameters["Vto"] = value;
            NotifyModified(nameof(ThresholdVoltage));
        }
    }

    public double Transconductance
    {
        get => ElectricalParameters.TryGetValue("Kp", out var val) ? val : 2e-5;
        set
        {
            ElectricalParameters["Kp"] = Math.Max(value, 1e-12);
            NotifyModified(nameof(Transconductance));
        }
    }

    public double ChannelWidth
    {
        get => ElectricalParameters.TryGetValue("W", out var val) ? val : 1e-4;
        set
        {
            ElectricalParameters["W"] = Math.Max(value, 1e-9);
            NotifyModified(nameof(ChannelWidth));
        }
    }

    public double ChannelLength
    {
        get => ElectricalParameters.TryGetValue("L", out var val) ? val : 1e-6;
        set
        {
            ElectricalParameters["L"] = Math.Max(value, 1e-9);
            NotifyModified(nameof(ChannelLength));
        }
    }

    public double Lambda
    {
        get => ElectricalParameters.TryGetValue("Lambda", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["Lambda"] = Math.Max(value, 0.0);
            NotifyModified(nameof(Lambda));
        }
    }

    public MOSFETComponent()
    {
        // Pin layout: Gate (left), Drain (top-right), Source (bottom-right), Body (middle-right)
        Pins.Add(new ComponentPin("gate", "G", -20, 0));
        Pins.Add(new ComponentPin("drain", "D", 15, -20));
        Pins.Add(new ComponentPin("source", "S", 15, 20));
        Pins.Add(new ComponentPin("body", "B", 15, 0));
        ThresholdVoltage = 2.0;
        Transconductance = 2e-5;
        ChannelWidth = 1e-4;
        ChannelLength = 1e-6;
        Lambda = 0.0;
        PartNumber = "IRF540N";
    }

    public MOSFETComponent(string name, MosfetChannel channel = MosfetChannel.NChannel) : this()
    {
        Name = name;
        Reference = name;
        Channel = channel;
    }
}
