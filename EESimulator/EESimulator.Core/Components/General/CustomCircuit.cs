using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.General;

/// <summary>A reusable black-box component backed by a saved EEE subcircuit.</summary>
public sealed class CustomCircuitComponent : GeneralComponent
{
    public const string StableType = "general.customcircuit";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override string StableTypeId => StableType;
    public override string ComponentType => "CustomCircuit";
    public override string Description => DisplayName;

    public string DefinitionId => ReadString("DefinitionId");
    public string DisplayName => ReadString("DisplayName", Name);
    public string SvgIcon => ReadString("SvgIcon");
    public string DefinitionJson => ReadString("DefinitionJson");
    public IReadOnlyList<CustomCircuitPort> Ports => ReadPorts();

    public CustomCircuitComponent()
    {
        RestorePorts();
    }

    public void SetDefinition(CustomCircuitDefinition definition)
    {
        CustomProperties["DefinitionId"] = definition.Id;
        CustomProperties["DisplayName"] = definition.Name;
        CustomProperties["SvgIcon"] = definition.SvgIcon;
        CustomProperties["DefinitionJson"] = definition.CircuitJson;
        CustomProperties["PortsJson"] = JsonSerializer.Serialize(definition.Ports, JsonOptions);
        RestorePorts();
    }

    public void RestorePorts()
    {
        var ports = ReadPorts();
        Pins.Clear();
        foreach (var port in ports)
            Pins.Add(new ComponentPin(port.Id, port.Name, port.RelativeX, port.RelativeY));
        if (Pins.Count == 0)
        {
            Pins.Add(new ComponentPin("port1", "N1", -24, 0));
            Pins.Add(new ComponentPin("port2", "N2", 24, 0));
        }
    }

    private List<CustomCircuitPort> ReadPorts()
    {
        if (CustomProperties.TryGetValue("PortsJson", out var raw) && raw is string json && !string.IsNullOrWhiteSpace(json))
        {
            try { return JsonSerializer.Deserialize<List<CustomCircuitPort>>(json, JsonOptions) ?? new List<CustomCircuitPort>(); }
            catch (JsonException) { }
        }
        return new List<CustomCircuitPort>();
    }

    private string ReadString(string key, string fallback = "") =>
        CustomProperties.TryGetValue(key, out var value) ? value?.ToString() ?? fallback : fallback;
}
