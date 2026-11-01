using System;
using System.Globalization;
using EESimulator.Core.Models;

namespace EESimulator.Commands.Commands;

/// <summary>
/// Command to connect two component pins with an electrical connection/wire.
/// </summary>
public class ConnectNodesCommand : ICommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Description => $"Connect {FromComponentName}.{FromPin} to {ToComponentName}.{ToPin}";
    public CommandOrigin Origin { get; set; } = CommandOrigin.Schematic;

    public CircuitNode Circuit { get; }
    public string FromComponentName { get; }
    public string FromPin { get; }
    public string ToComponentName { get; }
    public string ToPin { get; }
    public string NetName { get; }

    public ConnectionNode? CreatedConnection { get; private set; }

    public ConnectNodesCommand(CircuitNode circuit, string fromComponent, string fromPin, string toComponent, string toPin, string netName = "")
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        FromComponentName = fromComponent;
        FromPin = fromPin;
        ToComponentName = toComponent;
        ToPin = toPin;
        NetName = netName;
    }

    public bool Execute()
    {
        var fromComp = Circuit.FindChild<ComponentNode>(FromComponentName);
        var toComp = Circuit.FindChild<ComponentNode>(ToComponentName);

        if (fromComp == null || toComp == null)
            return false;

        if (CreatedConnection == null)
        {
            CreatedConnection = new ConnectionNode(fromComp.Id, FromPin, toComp.Id, ToPin, NetName);
        }

        Circuit.AddChild(CreatedConnection);
        return true;
    }

    public bool Undo()
    {
        if (CreatedConnection != null)
        {
            return Circuit.RemoveChild(CreatedConnection);
        }
        return false;
    }

    public string ToPythonScript()
    {
        var fromVar = FromComponentName.ToLowerInvariant();
        var toVar = ToComponentName.ToLowerInvariant();
        return $"circuit.connect({fromVar}.{FromPin}, {toVar}.{ToPin})";
    }
}
