using System;
using System.Globalization;
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

        OldValue = comp.ElectricalParameters.TryGetValue(PropertyName, out var val) ? val : 0;
        comp.ElectricalParameters[PropertyName] = NewValue;
        comp.NotifyModified(PropertyName);
        return true;
    }

    public bool Undo()
    {
        var comp = Circuit.FindChild<ComponentNode>(ComponentName);
        if (comp == null) return false;

        comp.ElectricalParameters[PropertyName] = OldValue;
        comp.NotifyModified(PropertyName);
        return true;
    }

    public string ToPythonScript()
    {
        var invCulture = CultureInfo.InvariantCulture;
        var varName = ComponentName.ToLowerInvariant();
        var propLower = PropertyName.ToLowerInvariant();
        return $"{varName}.{propLower} = {NewValue.ToString(invCulture)}";
    }
}
