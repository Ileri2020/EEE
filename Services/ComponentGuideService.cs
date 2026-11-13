using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using EESimulator.Core.Components.Registry;
using EESimulator.Core.Models;
using EESimulator.Scripting.API;

namespace EEE.Services;

public sealed class ComponentGuide
{
    public string Name { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Variant { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ConnectionText { get; init; } = string.Empty;
    public string EditablePropertiesText { get; init; } = string.Empty;
    public string PythonCallsText { get; init; } = string.Empty;
}

public static class ComponentGuideService
{
    private static readonly string[] SharedPythonCalls =
    {
        "eesim.add_component(type, name, x, y, param=0)",
        "eesim.add_component(type, name=name, row=row, col=column, param=0)",
        "eesim.connect(component.pin, component.pin)",
        "eesim.set_component_property(name, property_name, value)",
        "eesim.set_custom_property(name, property_name, value)",
        "eesim.set_position(name, x, y)",
        "eesim.set_description(name, description)",
        "eesim.set_breaking_current(name, amps)",
        "eesim.copy_component(name)",
        "eesim.rotate_component(name, degrees)",
        "eesim.stretch_component(name, scale_x, scale_y)",
        "eesim.delete_component(name)"
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
        string[] matchingTypeNames =
        {
            component.GetType().Name,
            definition?.TypeName ?? string.Empty,
            definition?.StableTypeId.Split('.').LastOrDefault() ?? string.Empty
        };
        var normalizedNames = matchingTypeNames
            .Select(NormalizeIdentifier)
            .Select(name => name.EndsWith("component", StringComparison.OrdinalIgnoreCase)
                ? name[..^"component".Length]
                : name.EndsWith("node", StringComparison.OrdinalIgnoreCase)
                    ? name[..^"node".Length]
                    : name)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var specificAddCalls = typeof(PythonApi)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.Name.StartsWith("Add", StringComparison.Ordinal)
                && normalizedNames.Contains(NormalizeIdentifier(method.Name[3..])))
            .Select(FormatPythonCall)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(call => call, StringComparer.Ordinal);

        calls.AddRange(specificAddCalls);
        return string.Join(Environment.NewLine, calls);
    }

    private static string FormatPythonCall(MethodInfo method)
    {
        string methodName = ToSnakeCase(method.Name);
        string parameters = string.Join(", ", method.GetParameters().Select(parameter => parameter.Name));
        return $"eesim.{methodName}({parameters})";
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

    private static string NormalizeIdentifier(string value)
    {
        var normalized = new StringBuilder(value.Length);
        foreach (char character in value)
            if (char.IsLetterOrDigit(character))
                normalized.Append(char.ToLowerInvariant(character));
        return normalized.ToString();
    }

    private static string ToSnakeCase(string value)
    {
        var result = new StringBuilder(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsUpper(character) && index > 0
                && (char.IsLower(value[index - 1])
                    || char.IsDigit(value[index - 1])
                    || (index + 1 < value.Length && char.IsLower(value[index + 1]))))
                result.Append('_');
            result.Append(char.ToLowerInvariant(character));
        }
        return result.ToString();
    }
}
