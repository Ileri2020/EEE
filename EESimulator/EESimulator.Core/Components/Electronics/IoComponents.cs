using System;
using System.Collections.Generic;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

public abstract class IoComponentBase : ElectronicComponent
{
    protected IoComponentBase() { }
    protected void SetPins(IEnumerable<ComponentPin> pins) => DigitalPinLayout.Replace(this, pins);
    protected static List<ComponentPin> BusPins(string prefix, int width, double x, double step = 1) {
        var pins = new List<ComponentPin>();
        for (int bit = 0; bit < width; bit++) pins.Add(new ComponentPin($"{prefix}{bit}", $"{prefix}{bit}", x, DigitalPinLayout.Position(bit, width) * step));
        return pins;
    }
}

public sealed class ButtonComponent : IoComponentBase
{
    public const string StableType = "io.button";
    public override string StableTypeId => StableType; public override string ComponentType => "Button"; public override string SpicePrefix => "BTN"; public override string Description => "Momentary digital push button";
    public bool IsPressed { get => CustomProperties.TryGetValue(nameof(IsPressed), out var value) && Convert.ToBoolean(value); set { CustomProperties[nameof(IsPressed)] = value; NotifyModified(nameof(IsPressed)); } }
    public ButtonComponent() { Pins.Add(new ComponentPin("out", "Q", 20, 0)); }
    public void Press() => IsPressed = true;
    public void Release() => IsPressed = false;
}

public sealed class DipSwitchComponent : IoComponentBase
{
    public const string StableType = "io.dipswitch";
    public override string StableTypeId => StableType; public override string ComponentType => "DIPSwitch"; public override string SpicePrefix => "DIP"; public override string Description => "Multi-pole DIP switch bank";
    public int PoleCount { get => ElectricalParameters.TryGetValue(nameof(PoleCount), out var value) ? Math.Clamp((int)value, 1, 32) : 8; set { ElectricalParameters[nameof(PoleCount)] = Math.Clamp(value, 1, 32); Rebuild(); NotifyModified(nameof(PoleCount)); } }
    public long Value { get => CustomProperties.TryGetValue(nameof(Value), out var value) && long.TryParse(value?.ToString(), out var parsed) ? parsed : 0; set { CustomProperties[nameof(Value)] = value; NotifyModified(nameof(Value)); } }
    public DipSwitchComponent() { PoleCount = 8; Value = 0; }
    private void Rebuild() => SetPins(BusPins("Q", PoleCount, 20));
}

public sealed class SevenSegmentComponent : IoComponentBase
{
    public const string StableType = "io.sevensegment";
    public override string StableTypeId => StableType; public override string ComponentType => "SevenSegment"; public override string SpicePrefix => "7SEG"; public override string Description => "Seven-segment display with decimal point";
    public bool ActiveLow { get => CustomProperties.TryGetValue(nameof(ActiveLow), out var value) && Convert.ToBoolean(value); set => CustomProperties[nameof(ActiveLow)] = value; }
    public SevenSegmentComponent() { for (int i = 0; i < 8; i++) Pins.Add(new ComponentPin($"seg{ i }", i == 7 ? "DP" : ((char)('a' + i)).ToString(), -20, DigitalPinLayout.Position(i, 8))); }
}

public sealed class HexDigitComponent : IoComponentBase
{
    public const string StableType = "io.hexdigit";
    public override string StableTypeId => StableType; public override string ComponentType => "HexDigit"; public override string SpicePrefix => "HEX"; public override string Description => "Four-bit hexadecimal digit display";
    public HexDigitComponent() { for (int i = 0; i < 4; i++) Pins.Add(new ComponentPin($"D{i}", $"D{i}", -20, DigitalPinLayout.Position(i, 4))); }
}

public sealed class LedBarComponent : IoComponentBase
{
    public const string StableType = "io.ledbar";
    public override string StableTypeId => StableType; public override string ComponentType => "LedBar"; public override string SpicePrefix => "LBA"; public override string Description => "Multi-segment LED bar display";
    public int LedCount { get => ElectricalParameters.TryGetValue(nameof(LedCount), out var value) ? Math.Clamp((int)value, 1, 32) : 8; set { ElectricalParameters[nameof(LedCount)] = Math.Clamp(value, 1, 32); Rebuild(); NotifyModified(nameof(LedCount)); } }
    public LedBarComponent() { LedCount = 8; }
    private void Rebuild() => SetPins(BusPins("D", LedCount, -20));
}

public sealed class RgbLedComponent : IoComponentBase
{
    public const string StableType = "io.rgbled";
    public override string StableTypeId => StableType; public override string ComponentType => "RGBLED"; public override string SpicePrefix => "RGB"; public override string Description => "Red-green-blue LED indicator";
    public bool CommonAnode { get => CustomProperties.TryGetValue(nameof(CommonAnode), out var value) && Convert.ToBoolean(value); set => CustomProperties[nameof(CommonAnode)] = value; }
    public RgbLedComponent() { Pins.Add(new ComponentPin("R", "R", -20, -10)); Pins.Add(new ComponentPin("G", "G", -20, 0)); Pins.Add(new ComponentPin("B", "B", -20, 10)); }
}

public sealed class DotMatrixComponent : IoComponentBase
{
    public const string StableType = "io.dotmatrix";
    public override string StableTypeId => StableType; public override string ComponentType => "DotMatrix"; public override string SpicePrefix => "DMX"; public override string Description => "Scanned LED dot-matrix display";
    public int Rows { get => ElectricalParameters.TryGetValue(nameof(Rows), out var value) ? Math.Clamp((int)value, 1, 16) : 7; set { ElectricalParameters[nameof(Rows)] = Math.Clamp(value, 1, 16); Rebuild(); NotifyModified(nameof(Rows)); } }
    public int Columns { get => ElectricalParameters.TryGetValue(nameof(Columns), out var value) ? Math.Clamp((int)value, 1, 16) : 5; set { ElectricalParameters[nameof(Columns)] = Math.Clamp(value, 1, 16); Rebuild(); NotifyModified(nameof(Columns)); } }
    public DotMatrixComponent() { Rows = 7; Columns = 5; }
    private void Rebuild() { var pins = BusPins("row", Rows, -20); pins.AddRange(BusPins("col", Columns, 20)); SetPins(pins); }
}

public sealed class JoystickComponent : IoComponentBase
{
    public const string StableType = "io.joystick";
    public override string StableTypeId => StableType; public override string ComponentType => "Joystick"; public override string SpicePrefix => "JOY"; public override string Description => "Two-axis joystick with button input";
    public int XValue { get => CustomProperties.TryGetValue(nameof(XValue), out var value) && int.TryParse(value?.ToString(), out var parsed) ? Math.Clamp(parsed, 0, 255) : 128; set => CustomProperties[nameof(XValue)] = Math.Clamp(value, 0, 255); }
    public int YValue { get => CustomProperties.TryGetValue(nameof(YValue), out var value) && int.TryParse(value?.ToString(), out var parsed) ? Math.Clamp(parsed, 0, 255) : 128; set => CustomProperties[nameof(YValue)] = Math.Clamp(value, 0, 255); }
    public bool Pressed { get => CustomProperties.TryGetValue(nameof(Pressed), out var value) && Convert.ToBoolean(value); set => CustomProperties[nameof(Pressed)] = value; }
    public JoystickComponent() { for (int i = 0; i < 8; i++) Pins.Add(new ComponentPin($"x{i}", $"X{i}", -20, DigitalPinLayout.Position(i, 8))); for (int i = 0; i < 8; i++) Pins.Add(new ComponentPin($"y{i}", $"Y{i}", 20, DigitalPinLayout.Position(i, 8))); Pins.Add(new ComponentPin("button", "BTN", 0, 20)); }
}

public sealed class KeyboardComponent : IoComponentBase
{
    public const string StableType = "io.keyboard";
    public override string StableTypeId => StableType; public override string ComponentType => "Keyboard"; public override string SpicePrefix => "KEY"; public override string Description => "ASCII keyboard data and valid output";
    public int Data { get => CustomProperties.TryGetValue(nameof(Data), out var value) && int.TryParse(value?.ToString(), out var parsed) ? Math.Clamp(parsed, 0, 255) : 0; set => CustomProperties[nameof(Data)] = Math.Clamp(value, 0, 255); }
    public bool DataReady { get => CustomProperties.TryGetValue(nameof(DataReady), out var value) && Convert.ToBoolean(value); set => CustomProperties[nameof(DataReady)] = value; }
    public KeyboardComponent() { for (int bit = 0; bit < 8; bit++) Pins.Add(new ComponentPin($"D{bit}", $"D{bit}", 20, DigitalPinLayout.Position(bit, 8))); Pins.Add(new ComponentPin("valid", "VALID", 20, 20)); Pins.Add(new ComponentPin("ack", "ACK", -20, 20)); }
}

public sealed class TtyComponent : IoComponentBase
{
    public const string StableType = "io.tty";
    public override string StableTypeId => StableType; public override string ComponentType => "TTY"; public override string SpicePrefix => "TTY"; public override string Description => "Serial text terminal display";
    public string Text { get => CustomProperties.TryGetValue(nameof(Text), out var value) ? value?.ToString() ?? string.Empty : string.Empty; set => CustomProperties[nameof(Text)] = value ?? string.Empty; }
    public TtyComponent() { for (int bit = 0; bit < 8; bit++) Pins.Add(new ComponentPin($"D{bit}", $"D{bit}", -20, DigitalPinLayout.Position(bit, 8))); Pins.Add(new ComponentPin("strobe", "STR", -20, 20)); Pins.Add(new ComponentPin("clear", "CLR", -20, -20)); }
}

public sealed class VideoComponent : IoComponentBase
{
    public const string StableType = "io.video";
    public override string StableTypeId => StableType; public override string ComponentType => "Video"; public override string SpicePrefix => "VID"; public override string Description => "Digital pixel framebuffer and sync display";
    public int ResolutionWidth { get => ElectricalParameters.TryGetValue(nameof(ResolutionWidth), out var value) ? Math.Clamp((int)value, 1, 1024) : 256; set => ElectricalParameters[nameof(ResolutionWidth)] = Math.Clamp(value, 1, 1024); }
    public int ResolutionHeight { get => ElectricalParameters.TryGetValue(nameof(ResolutionHeight), out var value) ? Math.Clamp((int)value, 1, 1024) : 256; set => ElectricalParameters[nameof(ResolutionHeight)] = Math.Clamp(value, 1, 1024); }
    public VideoComponent() { for (int bit = 0; bit < 8; bit++) Pins.Add(new ComponentPin($"R{bit}", $"R{bit}", -20, DigitalPinLayout.Position(bit, 8))); for (int bit = 0; bit < 8; bit++) Pins.Add(new ComponentPin($"G{bit}", $"G{bit}", -20, DigitalPinLayout.Position(bit, 8))); for (int bit = 0; bit < 8; bit++) Pins.Add(new ComponentPin($"B{bit}", $"B{bit}", -20, DigitalPinLayout.Position(bit, 8))); Pins.Add(new ComponentPin("hsync", "HS", -20, 20)); Pins.Add(new ComponentPin("vsync", "VS", -20, 24)); Pins.Add(new ComponentPin("clk", "CLK", -20, 28)); }
}

public sealed class RealTimeClockComponent : IoComponentBase
{
    public const string StableType = "io.realtimeclock";
    public override string StableTypeId => StableType; public override string ComponentType => "RealTimeClock"; public override string SpicePrefix => "RTC"; public override string Description => "Wall-clock time digital source";
    public RealTimeClockComponent() { for (int bit = 0; bit < 32; bit++) Pins.Add(new ComponentPin($"seconds{bit}", $"S{bit}", 20, DigitalPinLayout.Position(bit, 32))); for (int bit = 0; bit < 6; bit++) Pins.Add(new ComponentPin($"minutes{bit}", $"M{bit}", 24, DigitalPinLayout.Position(bit, 6))); for (int bit = 0; bit < 5; bit++) Pins.Add(new ComponentPin($"hours{bit}", $"H{bit}", 28, DigitalPinLayout.Position(bit, 5))); }
}
