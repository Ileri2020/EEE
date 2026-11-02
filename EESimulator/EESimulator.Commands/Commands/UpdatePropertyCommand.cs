using System;
using System.Globalization;
using System.Reflection;
using EESimulator.Core.Models;

namespace EESimulator.Commands.Commands;

/// <summary>
/// Command to update an electrical or graphical property on a component.
/// </summary>
public class UpdatePropertyCommand : ICommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Description => $"Update {PropertyName} on {ComponentName} to {NewValue}";
    public CommandOrigin Origin { get; set; } = CommandOrigin.Schematic;

    public CircuitNode Circuit { get; }
    public string ComponentName { get; }
    public string PropertyName { get; }
    public double OldValue { get; private set; }
    public double NewValue { get; }
    private PropertyInfo? _targetProperty;
    private object? _oldPropertyValue;

    public UpdatePropertyCommand(CircuitNode circuit, string componentName, string propertyName, double newValue)
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        ComponentName = componentName;
        PropertyName = propertyName;
        NewValue = newValue;
    }

    public bool Execute()
    {
        var comp = Circuit.FindChild<ComponentNode>(ComponentName);
        if (comp == null) return false;

        _targetProperty = comp.GetType().GetProperty(PropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (_targetProperty?.CanWrite == true && _targetProperty.GetIndexParameters().Length == 0 && IsNumeric(_targetProperty.PropertyType))
        {
            _oldPropertyValue = _targetProperty.GetValue(comp);
            OldValue = Convert.ToDouble(_oldPropertyValue ?? 0, CultureInfo.InvariantCulture);
            _targetProperty.SetValue(comp, Convert.ChangeType(NewValue, Nullable.GetUnderlyingType(_targetProperty.PropertyType) ?? _targetProperty.PropertyType, CultureInfo.InvariantCulture));
        }
        else
        {
            _targetProperty = null;
            OldValue = comp.ElectricalParameters.TryGetValue(PropertyName, out var val) ? val : 0;
            comp.ElectricalParameters[PropertyName] = NewValue;
        }
        comp.NotifyModified(PropertyName);
        return true;
    }

    public bool Undo()
    {
        var comp = Circuit.FindChild<ComponentNode>(ComponentName);
        if (comp == null) return false;

        if (_targetProperty?.CanWrite == true)
            _targetProperty.SetValue(comp, _oldPropertyValue);
        else
            comp.ElectricalParameters[PropertyName] = OldValue;
        comp.NotifyModified(PropertyName);
        return true;
    }

    private static bool IsNumeric(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long) ||
               type == typeof(float) || type == typeof(double) || type == typeof(decimal);
    }

    public string ToPythonScript()
    {
        var invCulture = CultureInfo.InvariantCulture;
        var varName = ComponentName.ToLowerInvariant();
        var propLower = PropertyName.ToLowerInvariant();
        return $"{varName}.{propLower} = {NewValue.ToString(invCulture)}";
    }
}
