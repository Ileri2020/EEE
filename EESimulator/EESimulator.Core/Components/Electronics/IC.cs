using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Generic Integrated Circuit package (DIP / SOIC).
/// </summary>
public class ICComponent : ElectronicComponent
{
    public const string StableType = "electronics.ic";

    public override string StableTypeId => StableType;
    public override string ComponentType => "IC";
    public override string Description => "Integrated Circuit Package";

    public int PinCount { get; set; } = 8;

    public ICComponent()
    {
        InitializePins(8);
    }

    public ICComponent(string name, int pinCount = 8, string partNumber = "NE555")
    {
        Name = name;
        Reference = name;
        PinCount = pinCount;
        PartNumber = partNumber;
        InitializePins(pinCount);
    }

    private void InitializePins(int pinCount)
    {
        Pins.Clear();
        int half = pinCount / 2;
        double spacing = 15.0;
        double startY = -((half - 1) * spacing) / 2.0;

        // Left side pins (1 to half)
        for (int i = 0; i < half; i++)
        {
            int pinNum = i + 1;
            Pins.Add(new ComponentPin($"pin{pinNum}", $"{pinNum}", -25, startY + i * spacing));
        }

        // Right side pins (half + 1 to pinCount, bottom to top)
        for (int i = 0; i < half; i++)
        {
            int pinNum = half + 1 + i;
            Pins.Add(new ComponentPin($"pin{pinNum}", $"{pinNum}", 25, startY + (half - 1 - i) * spacing));
        }
    }
}
