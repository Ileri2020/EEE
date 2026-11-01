using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Digital Logic Input component.
/// Provides a toggleable digital logic level (0 or 1) with customizable ON/OFF voltages.
/// Tapping the component or calling Toggle() flips the logic state.
/// </summary>
public class LogicInputComponent : ElectronicComponent
{
    public const string StableType = "electronics.logicinput";

    public override string StableTypeId => StableType;
    public override string ComponentType => "LogicInput";
    public override string Description => "Digital Logic Toggle Input (0/1)";

    /// <summary>
    /// Current digital logic state: false = Logic 0 (Low), true = Logic 1 (High).
    /// </summary>
    public bool State
    {
        get
        {
            if (CustomProperties.TryGetValue("State", out var val))
            {
                if (val is bool bVal) return bVal;
                if (val is int iVal) return iVal != 0;
                if (val is double dVal) return dVal > 0.5;
            }
            return ElectricalParameters.TryGetValue("State", out var pVal) && pVal > 0.5;
        }
        set
        {
            CustomProperties["State"] = value;
            ElectricalParameters["State"] = value ? 1.0 : 0.0;
            LiveVoltage = value ? HighVoltage : LowVoltage;
            NotifyModified(nameof(State));
        }
    }

    /// <summary>
    /// Voltage level representing Logic 1 (ON). Default is 5.0 V (user-configurable, e.g. 3.3V, 5V, 12V).
    /// </summary>
    public double HighVoltage
    {
        get => ElectricalParameters.TryGetValue("HighVoltage", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["HighVoltage"] = value;
            if (State) LiveVoltage = value;
            NotifyModified(nameof(HighVoltage));
        }
    }

    /// <summary>
    /// Voltage level representing Logic 0 (OFF). Default is 0.0 V.
    /// </summary>
    public double LowVoltage
    {
        get => ElectricalParameters.TryGetValue("LowVoltage", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["LowVoltage"] = value;
            if (!State) LiveVoltage = value;
            NotifyModified(nameof(LowVoltage));
        }
    }

    public LogicInputComponent()
    {
        Pins.Add(new ComponentPin("out", "Q", 20, 0));
        HighVoltage = 5.0;
        LowVoltage = 0.0;
        State = false;
        LiveVoltage = 0.0;
    }

    public LogicInputComponent(string name, bool initialState = false, double highVoltage = 5.0, double lowVoltage = 0.0) : this()
    {
        Name = name;
        Reference = name;
        HighVoltage = highVoltage;
        LowVoltage = lowVoltage;
        State = initialState;
    }

    /// <summary>
    /// Toggles the logic state between 0 and 1.
    /// </summary>
    public void Toggle()
    {
        State = !State;
    }
}
