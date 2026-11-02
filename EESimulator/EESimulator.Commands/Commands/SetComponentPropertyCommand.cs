using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using EESimulator.Core.Models;

namespace EESimulator.Commands.Commands;

/// <summary>Undoable update for a public scalar component property.</summary>
public sealed class SetComponentPropertyCommand : ICommand
{
    private static readonly string[] ProtectedProperties =
    {
        "Id", "Parent", "Children", "Pins", "Name", "Reference", "Description",
        "Position", "X", "Y", "Rotation", "ScaleX", "ScaleY", "CustomProperties",
        "ElectricalParameters", "WaveformTime", "WaveformVoltage", "WaveformCurrent"
    };

    private PropertyInfo? _property;
    private object? _oldValue;

    public Guid Id { get; } = Guid.NewGuid();
    public string Description => $"Set {PropertyName} on {ComponentName}";
    public CommandOrigin Origin { get; set; } = CommandOrigin.Python;
    public CircuitNode Circuit { get; }
    public string ComponentName { get; }
    public string PropertyName { get; }
    public object? NewValue { get; }

    public SetComponentPropertyCommand(CircuitNode circuit, string componentName, string propertyName, object? newValue)
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        ComponentName = componentName ?? throw new ArgumentNullException(nameof(componentName));
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        NewValue = newValue;
    }

    public bool Execute()
    {
        var component = Circuit.FindChild<ComponentNode>(ComponentName);
        if (component == null) return false;

        var property = component.GetType().GetProperty(PropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property?.SetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0 ||
            ProtectedProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase) || !IsSupportedType(property.PropertyType))
        {
            throw new InvalidOperationException($"'{PropertyName}' is not an editable scalar property on {component.ComponentType}.");
        }

        _property = property;
        _oldValue = property.GetValue(component);
        object? converted = ConvertValue(NewValue, property.PropertyType);
        property.SetValue(component, converted);
        component.NotifyModified(property.Name);
        return true;
    }

    public bool Undo()
    {
        var component = Circuit.FindChild<ComponentNode>(ComponentName);
        if (component == null || _property == null) return false;
        _property.SetValue(component, _oldValue);
        component.NotifyModified(_property.Name);
        return true;
    }

    public string ToPythonScript()
    {
        var value = NewValue switch
        {
            null => "None",
            bool boolean => boolean ? "True" : "False",
            string text => JsonSerializer.Serialize(text),
            Enum enumValue => JsonSerializer.Serialize(enumValue.ToString()),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => JsonSerializer.Serialize(NewValue)
        };
        return $"eesim.set_component_property({JsonSerializer.Serialize(ComponentName)}, {JsonSerializer.Serialize(PropertyName)}, {value})";
    }

    private static bool IsSupportedType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
    }

    private static object? ConvertValue(object? value, Type destination)
    {
        bool isNullable = Nullable.GetUnderlyingType(destination) != null;
        Type target = Nullable.GetUnderlyingType(destination) ?? destination;
        if (value == null)
        {
            if (!target.IsValueType || isNullable) return null;
            throw new InvalidCastException($"A null value cannot be assigned to {target.Name}.");
        }

        if (target.IsInstanceOfType(value)) return value;
        if (target.IsEnum)
        {
            if (value is string enumText && Enum.TryParse(target, enumText, true, out var parsed)) return parsed;
            return Enum.ToObject(target, Convert.ChangeType(value, Enum.GetUnderlyingType(target), CultureInfo.InvariantCulture)!);
        }
        if (target == typeof(bool))
        {
            if (value is string booleanText && bool.TryParse(booleanText, out var parsed)) return parsed;
            return Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0;
        }
        if (target == typeof(string)) return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }
}
