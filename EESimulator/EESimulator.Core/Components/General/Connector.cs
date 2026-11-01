using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.General;

/// <summary>
/// Multi-pin header or external terminal connector.
/// </summary>
public class ConnectorComponent : GeneralComponent
{
    public const string StableType = "general.connector";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Connector";
    public override string Description => "Multi-pin external connector header";

    public int PinCount { get; set; } = 4;

    public ConnectorComponent()
    {
        InitializePins(4);
    }

    public ConnectorComponent(string name, int pinCount = 4)
    {
        Name = name;
        Reference = name;
        PinCount = pinCount;
        InitializePins(pinCount);
    }

    private void InitializePins(int pinCount)
    {
        Pins.Clear();
        double spacing = 15.0;
        double startY = -((pinCount - 1) * spacing) / 2.0;

        for (int i = 0; i < pinCount; i++)
        {
            int pinNum = i + 1;
            Pins.Add(new ComponentPin($"pin{pinNum}", $"{pinNum}", 0, startY + i * spacing));
        }
    }
}
