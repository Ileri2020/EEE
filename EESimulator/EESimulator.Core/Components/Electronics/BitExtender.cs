using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

public enum BitExtensionType
{
    ZeroExtend,
    SignExtend,
    OneExtend,
    InputExtend
}

/// <summary>
/// Bit Extender Component.
/// Changes the bit-width of a digital signal or bus (e.g., extending 1-bit or 4-bit to 8-bit or 16-bit)
/// via zero-extension, sign-extension (replicating MSB), or one-extension.
/// </summary>
public class BitExtenderComponent : ElectronicComponent
{
    public const string StableType = "electronics.bitextender";

    public override string StableTypeId => StableType;
    public override string ComponentType => "BitExtender";
    public override string Description => "Bus Bit Extender (Sign / Zero / One Extend)";

    public int InputBitWidth
    {
        get => ElectricalParameters.TryGetValue("InputBitWidth", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["InputBitWidth"] = Math.Clamp(value, 1, 32);
            NotifyModified(nameof(InputBitWidth));
        }
    }

    public int OutputBitWidth
    {
        get => ElectricalParameters.TryGetValue("OutputBitWidth", out var val) && val >= 1 ? (int)val : 8;
        set
        {
            ElectricalParameters["OutputBitWidth"] = Math.Clamp(value, 1, 64);
            NotifyModified(nameof(OutputBitWidth));
        }
    }

    public BitExtensionType ExtensionType
    {
        get
        {
            if (CustomProperties.TryGetValue("ExtensionType", out var val) && val is BitExtensionType t)
                return t;
            if (CustomProperties.TryGetValue("ExtensionType", out var strVal) && Enum.TryParse<BitExtensionType>(strVal?.ToString(), true, out var parsed))
                return parsed;
            return BitExtensionType.ZeroExtend;
        }
        set
        {
            CustomProperties["ExtensionType"] = value;
            NotifyModified(nameof(ExtensionType));
        }
    }

    public BitExtenderComponent()
    {
        Pins.Add(new ComponentPin("in", "IN", -20, 0));
        Pins.Add(new ComponentPin("out", "OUT", 20, 0));

        InputBitWidth = 1;
        OutputBitWidth = 8;
        ExtensionType = BitExtensionType.ZeroExtend;
    }

    public BitExtenderComponent(string name, int inWidth = 1, int outWidth = 8, BitExtensionType type = BitExtensionType.ZeroExtend) : this()
    {
        Name = name;
        Reference = name;
        InputBitWidth = inWidth;
        OutputBitWidth = outWidth;
        ExtensionType = type;
    }
}
