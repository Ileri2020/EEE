using System;
using System.Globalization;
using EESimulator.Core.Models;

namespace EESimulator.Commands.Commands;

/// <summary>
/// Command to move a component's schematic position.
/// </summary>
public class MoveComponentCommand : ICommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Description => $"Move component {ComponentName} to ({NewX}, {NewY})";
    public CommandOrigin Origin { get; set; } = CommandOrigin.Schematic;

    public CircuitNode Circuit { get; }
    public string ComponentName { get; }
    public double OldX { get; private set; }
    public double OldY { get; private set; }
    public double NewX { get; }
    public double NewY { get; }

    public MoveComponentCommand(CircuitNode circuit, string componentName, double newX, double newY)
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        ComponentName = componentName;
        NewX = newX;
        NewY = newY;
    }

    public bool Execute()
    {
        var comp = Circuit.FindChild<ComponentNode>(ComponentName);
        if (comp == null) return false;

        OldX = comp.X;
        OldY = comp.Y;
        comp.X = NewX;
        comp.Y = NewY;
        return true;
    }

    public bool Undo()
    {
        var comp = Circuit.FindChild<ComponentNode>(ComponentName);
        if (comp == null) return false;

        comp.X = OldX;
        comp.Y = OldY;
        return true;
    }

    public string ToPythonScript()
    {
        var invCulture = CultureInfo.InvariantCulture;
        var varName = ComponentName.ToLowerInvariant();
        return $"{varName}.position = ({NewX.ToString(invCulture)}, {NewY.ToString(invCulture)})";
    }
}
