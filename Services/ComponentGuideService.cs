using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Components.Registry;
using EESimulator.Core.Models;

namespace EEE.Services;

public sealed class ComponentGuide
{
    public string Name { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Variant { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string OperatingNotesText { get; init; } = string.Empty;
    public string ConnectionText { get; init; } = string.Empty;
    public string EditablePropertiesText { get; init; } = string.Empty;
    public string PythonCallsText { get; init; } = string.Empty;
}

public static class ComponentGuideService
{
    private static readonly string[] SharedPythonCalls =
    {
        "eesim.add_component(type, name=\"name\", row=1, col=1)",
        "eesim.connect(component.pin, component.pin)",
        "eesim.set_component_property(\"name\", \"property\", value)",
        "eesim.set_logic(\"input\", True)",
        "eesim.set_memory_word(\"memory\", 0, 0)",
        "eesim.evaluate_combinational()",
        "eesim.advance_simulation(0.25)",
        "eesim.simulate(\"OperatingPoint\")",
        "eesim.read_digital_output_state(\"component\", \"pin\")",
        "eesim.read_pin_voltage(\"component\", \"pin\")"
    };

    public static ComponentGuide? Create(ComponentNode? component)
    {
        if (component == null)
            return null;

        ComponentDefinition? definition = ComponentRegistry.GetAllDefinitions()
            .FirstOrDefault(candidate => candidate.NodeType == component.GetType());

        return new ComponentGuide
        {
            Name = definition?.DisplayName ?? component.ComponentType,
            Reference = component.Reference,
            Variant = GetVariant(component),
            Category = definition?.Category ?? "Custom",
            Description = string.IsNullOrWhiteSpace(definition?.Description)
                ? component.Description
                : definition.Description,
            OperatingNotesText = GetOperatingNotesText(component),
            ConnectionText = GetConnectionText(component),
            EditablePropertiesText = GetEditablePropertiesText(component, definition),
            PythonCallsText = GetPythonCallsText(component, definition)
        };
    }

    public static IReadOnlyList<ComponentGuide> CreateAll()
    {
        return ComponentRegistry.GetAllDefinitions()
            .OrderBy(definition => definition.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(definition =>
            {
                var component = ComponentRegistry.CreateInstance(
                    definition.StableTypeId,
                    $"{definition.ReferencePrefix}1");
                return Create(component)
                    ?? throw new InvalidOperationException($"Could not create a guide for {definition.DisplayName}.");
            })
            .ToList();
    }

    public static string Format(ComponentGuide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);
        return string.Join(Environment.NewLine, new[]
        {
            guide.Name,
            $"Reference: {guide.Reference}",
            $"Category: {guide.Category}",
            $"Variant: {guide.Variant}",
            $"Description: {guide.Description}",
            "Operating requirements:",
            guide.OperatingNotesText,
            "Connections:",
            guide.ConnectionText,
            "Editable properties:",
            guide.EditablePropertiesText,
            "Python API calls:",
            guide.PythonCallsText
        });
    }

    public static string FormatAll(IEnumerable<ComponentGuide> guides)
    {
        ArgumentNullException.ThrowIfNull(guides);
        return string.Join(
            $"{Environment.NewLine}{Environment.NewLine}{new string('=', 72)}{Environment.NewLine}{Environment.NewLine}",
            guides.Select(Format));
    }

    private static string GetVariant(ComponentNode component)
    {
        var variants = GetSpecificProperties(component)
            .Where(property => property.CanRead
                && IsSimple(property.PropertyType)
                && (property.Name.Contains("Variant", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("Polarity", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("Channel", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("PartNumber", StringComparison.OrdinalIgnoreCase)))
            .Select(property => $"{property.Name}: {FormatValue(property.GetValue(component))}")
            .ToList();

        return variants.Count == 0 ? component.ComponentType : string.Join(Environment.NewLine, variants);
    }

    private static string GetConnectionText(ComponentNode component)
    {
        if (component.Pins.Count == 0)
            return "This component has no electrical pins.";

        return string.Join(Environment.NewLine, component.Pins.Select(pin =>
            $"{pin.Name} (ID: {pin.Id}) - {GetPinSide(pin.RelativeX, pin.RelativeY)}"));
    }

    private static string GetEditablePropertiesText(ComponentNode component, ComponentDefinition? definition)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (definition != null)
        {
            foreach (var parameter in definition.DefaultParameters)
            {
                double current = component.ElectricalParameters.TryGetValue(parameter.Key, out var value)
                    ? value
                    : parameter.Value;
                properties[parameter.Key] = FormatValue(current);
            }
        }

        foreach (var pair in component.ElectricalParameters)
            properties[pair.Key] = FormatValue(pair.Value);

        foreach (var property in GetSpecificProperties(component))
        {
            if (!property.CanRead
                || property.SetMethod?.IsPublic != true
                || property.GetIndexParameters().Length != 0
                || !IsSimple(property.PropertyType))
                continue;

            if (!properties.ContainsKey(property.Name))
                properties[property.Name] = FormatValue(property.GetValue(component));
        }

        properties["BreakingCurrent"] = FormatValue(component.BreakingCurrent);
        foreach (var customProperty in component.CustomProperties)
            properties[customProperty.Key] = FormatValue(customProperty.Value);

        return properties.Count == 0
            ? "No editable electrical parameters are defined."
            : string.Join(Environment.NewLine, properties.Select(pair => $"{pair.Key} = {pair.Value}"));
    }

    private static string GetPythonCallsText(ComponentNode component, ComponentDefinition? definition)
    {
        var calls = new List<string>(SharedPythonCalls);
        string typeId = definition?.StableTypeId ?? component.ComponentType;
        string name = JsonSerializer.Serialize(component.Name);
        calls.Insert(0,
            $"eesim.add_component({JsonSerializer.Serialize(typeId)}, name={name}, row=1, col=1)");

        if (component is LogicInputComponent)
            calls.Add($"eesim.set_logic({name}, True)");
        if (component is RAMComponent or ROMComponent)
            calls.Add($"eesim.set_memory_word({name}, 0, 0)");

        return string.Join(Environment.NewLine, calls);
    }

    private static string GetOperatingNotesText(ComponentNode component)
    {
        return component switch
        {
            RAMComponent ram =>
                "Write: drive we HIGH and keep address/data stable through the rising clock edge. " +
                (ram.HasOutputEnable
                    ? "Read: drive oe HIGH; oe is active-high. The default synchronous read latches the address on the clock edge, so enable oe and clock the address before expecting dout values."
                    : "Output enable is disabled, so dout is always driven. The default synchronous read latches the address on the clock edge."),
            ROMComponent rom => rom.HasChipSelect
                ? "Drive sel HIGH to enable the output, then set addr and evaluate the circuit. A LOW sel disables the output."
                : "Set addr and evaluate the circuit; chip select is disabled by default.",
            ClockComponent clock =>
                $"Connect out to the clock inputs. The default frequency is {clock.Frequency:G} Hz with {clock.DutyCycle:P0} duty cycle; sequential components normally trigger on a rising edge.",
            DFlipFlopComponent flipFlop =>
                $"D is sampled on the {flipFlop.Trigger}. The asynchronous reset and set inputs are active-high; leave both LOW during normal clocked operation.",
            JKFlipFlopComponent flipFlop =>
                $"J and K are sampled on the {flipFlop.Trigger}. J=1,K=0 sets Q; J=0,K=1 resets Q; J=K=1 toggles. Asynchronous reset and set are active-high.",
            SRFlipFlopComponent flipFlop =>
                $"S and R are sampled on the {flipFlop.Trigger}. Use S=1,R=0 to set and S=0,R=1 to reset; S=R=1 is invalid. Asynchronous reset and set are active-high.",
            TFlipFlopComponent flipFlop =>
                $"T is sampled on the {flipFlop.Trigger}; T=1 toggles Q. The asynchronous reset and set inputs are active-high.",
            RegisterComponent register =>
                $"Drive en HIGH to load D on the {register.Trigger}. clr is active-high and clears the stored word.",
            CounterComponent counter =>
                $"Drive en HIGH to count on the {counter.Trigger}; up_down HIGH counts up and LOW counts down. clr and ld are active-high controls.",
            ShiftRegisterComponent shiftRegister =>
                $"Drive en HIGH to shift sin into the register on the {shiftRegister.Trigger}. clr is active-high; dout bits update after the edge.",
            LogicInputComponent => "Set or toggle State to drive out HIGH/LOW; use eesim.set_logic(name, True/False) from scripts.",
            LogicOutputComponent => "Connect in to the signal being observed; the output reflects the connected logic level.",
            MultiplexerComponent mux =>
                $"Set sel to the desired input index. {(mux.HasEnable ? "Drive en HIGH to enable the output." : "No enable pin is configured.")}",
            DemultiplexerComponent demux =>
                $"Set sel to the desired output index. {(demux.HasEnable ? "Drive en HIGH to enable routing." : "No enable pin is configured.")}",
            DecoderComponent decoder =>
                $"Set sel to the desired address. {(decoder.HasEnable ? "Drive en HIGH to activate the selected output." : "No enable pin is configured.")}",
            PriorityEncoderComponent => "Drive en HIGH and assert the desired input; when multiple inputs are HIGH, the highest-numbered active input has priority.",
            ControlledBufferComponent buffer =>
                $"The output is enabled when ctrl is {(buffer.ControlActiveLow ? "LOW" : "HIGH")}; otherwise it is high-impedance.",
            DiodeComponent or LEDComponent =>
                "Use the correct anode/cathode polarity and include a current-limiting resistor when driven from a voltage source.",
            TtlPackageComponent => "Connect signals to the documented package pin numbers and use the matching part-number pinout.",
            _ => "Connect the listed input and output pins according to their roles. Configure required control pins before advancing or evaluating the simulation."
        };
    }

    private static string GetPinSide(double x, double y)
    {
        if (Math.Abs(x) >= Math.Abs(y))
            return x < 0 ? "left side" : "right side";
        return y < 0 ? "top side" : "bottom side";
    }

    private static IEnumerable<PropertyInfo> GetSpecificProperties(ComponentNode component)
    {
        for (Type? type = component.GetType(); type != null && type != typeof(ComponentNode) && type != typeof(object); type = type.BaseType)
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                yield return property;
    }

    private static bool IsSimple(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "-",
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "-"
        };
    }

}
