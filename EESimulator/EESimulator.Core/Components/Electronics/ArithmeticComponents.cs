using System;
using System.Collections.Generic;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>Common scalar-pin bus model used by Logisim-style arithmetic blocks.</summary>
public abstract class ArithmeticComponentBase : ElectronicComponent
{
    protected ArithmeticComponentBase() => RebuildPins();
    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue(nameof(BitWidth), out var value) ? Math.Clamp((int)value, 1, 32) : 8;
        set { ElectricalParameters[nameof(BitWidth)] = Math.Clamp(value, 1, 32); RebuildPins(); NotifyModified(nameof(BitWidth)); }
    }
    public int ShiftWidth => Math.Max(1, (int)Math.Ceiling(Math.Log2(BitWidth + 1)));
    protected virtual bool HasSecondInput => true;
    protected virtual bool HasCarryInput => false;
    protected virtual bool HasCarryOutput => false;
    protected virtual bool HasCompareOutputs => false;
    protected virtual string InputA => "A";
    protected virtual string InputB => "B";
    protected virtual string Output => "Y";
    public virtual string Mode { get => CustomProperties.TryGetValue(nameof(Mode), out var value) ? value?.ToString() ?? string.Empty : string.Empty; set => CustomProperties[nameof(Mode)] = value ?? string.Empty; }
    protected void RebuildPins()
    {
        var pins = new List<ComponentPin>();
        for (int bit = 0; bit < BitWidth; bit++) pins.Add(new ComponentPin($"{InputA}{bit}", $"{InputA}{bit}", -24, DigitalPinLayout.Position(bit, BitWidth)));
        if (HasSecondInput)
            for (int bit = 0; bit < BitWidth; bit++) pins.Add(new ComponentPin($"{InputB}{bit}", $"{InputB}{bit}", -24, DigitalPinLayout.Position(bit, BitWidth)));
        if (HasCarryInput) pins.Add(new ComponentPin("cin", "CI", -8, 24));
        if (this is ShifterComponent)
            for (int bit = 0; bit < ShiftWidth; bit++) pins.Add(new ComponentPin($"S{bit}", $"S{bit}", -24, DigitalPinLayout.Position(bit, ShiftWidth)));
        if (HasCompareOutputs)
        {
            pins.Add(new ComponentPin("gt", "A>B", 24, -12));
            pins.Add(new ComponentPin("eq", "A=B", 24, 0));
            pins.Add(new ComponentPin("lt", "A<B", 24, 12));
        }
        else
        {
            for (int bit = 0; bit < BitWidth; bit++) pins.Add(new ComponentPin($"Y{bit}", $"Y{bit}", 24, DigitalPinLayout.Position(bit, BitWidth)));
            if (HasCarryOutput) pins.Add(new ComponentPin("cout", "CO", 24, 24));
            if (this is BitFinderComponent) pins.Add(new ComponentPin("found", "FOUND", 24, -24));
            if (this is DividerComponent) pins.Add(new ComponentPin("divide_by_zero", "DIV0", 24, -24));
        }
        DigitalPinLayout.Replace(this, pins);
    }
}

public sealed class AdderComponent : ArithmeticComponentBase { public const string StableType = "arith.adder"; public override string StableTypeId => StableType; public override string ComponentType => "Adder"; public override string SpicePrefix => "ADD"; public override string Description => "N-bit binary adder"; protected override bool HasCarryInput => true; protected override bool HasCarryOutput => true; }
public sealed class SubtractorComponent : ArithmeticComponentBase { public const string StableType = "arith.subtractor"; public override string StableTypeId => StableType; public override string ComponentType => "Subtractor"; public override string SpicePrefix => "SUB"; public override string Description => "N-bit binary subtractor"; protected override bool HasCarryInput => true; protected override bool HasCarryOutput => true; }
public sealed class MultiplierComponent : ArithmeticComponentBase { public const string StableType = "arith.multiplier"; public override string StableTypeId => StableType; public override string ComponentType => "Multiplier"; public override string SpicePrefix => "MUL"; public override string Description => "N-bit binary multiplier"; }
public sealed class DividerComponent : ArithmeticComponentBase { public const string StableType = "arith.divider"; public override string StableTypeId => StableType; public override string ComponentType => "Divider"; public override string SpicePrefix => "DIV"; public override string Description => "N-bit unsigned binary divider"; }
public sealed class ComparatorComponent : ArithmeticComponentBase { public const string StableType = "arith.comparator"; public override string StableTypeId => StableType; public override string ComponentType => "Comparator"; public override string SpicePrefix => "CMP"; public override string Description => "N-bit magnitude comparator"; protected override bool HasCompareOutputs => true; }
public sealed class NegatorComponent : ArithmeticComponentBase { public const string StableType = "arith.negator"; public override string StableTypeId => StableType; public override string ComponentType => "Negator"; public override string SpicePrefix => "NEG"; public override string Description => "N-bit two's-complement negator"; protected override bool HasSecondInput => false; }
public sealed class ShifterComponent : ArithmeticComponentBase { public const string StableType = "arith.shifter"; public override string StableTypeId => StableType; public override string ComponentType => "Shifter"; public override string SpicePrefix => "SHF"; public override string Description => "Logical, arithmetic, and rotate barrel shifter"; protected override bool HasSecondInput => false; }
public sealed class BitAdderComponent : ArithmeticComponentBase { public const string StableType = "arith.bitadder"; public override string StableTypeId => StableType; public override string ComponentType => "BitAdder"; public override string SpicePrefix => "POPC"; public override string Description => "Population count of set bits"; protected override bool HasSecondInput => false; }
public sealed class BitFinderComponent : ArithmeticComponentBase { public const string StableType = "arith.bitfinder"; public override string StableTypeId => StableType; public override string ComponentType => "BitFinder"; public override string SpicePrefix => "BFND"; public override string Description => "Find first or last set bit"; protected override bool HasSecondInput => false; }
public sealed class SquareRootComponent : ArithmeticComponentBase { public const string StableType = "arith.sqrt"; public override string StableTypeId => StableType; public override string ComponentType => "SquareRoot"; public override string SpicePrefix => "SQRT"; public override string Description => "Integer square root"; protected override bool HasSecondInput => false; }
public sealed class AbsoluteValueComponent : ArithmeticComponentBase { public const string StableType = "arith.absolute"; public override string StableTypeId => StableType; public override string ComponentType => "AbsoluteValue"; public override string SpicePrefix => "ABS"; public override string Description => "N-bit absolute value"; protected override bool HasSecondInput => false; }
public sealed class MinMaxComponent : ArithmeticComponentBase { public const string StableType = "arith.minmax"; public override string StableTypeId => StableType; public override string ComponentType => "MinMax"; public override string SpicePrefix => "MM"; public override string Description => "N-bit minimum or maximum"; }
public sealed class ExponentiatorComponent : ArithmeticComponentBase { public const string StableType = "arith.exponentiator"; public override string StableTypeId => StableType; public override string ComponentType => "Exponentiator"; public override string SpicePrefix => "POW"; public override string Description => "Integer exponentiation A^B"; }
