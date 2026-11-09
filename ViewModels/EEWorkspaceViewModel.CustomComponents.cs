using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Storage;
using EESimulator.Commands;
using EESimulator.Commands.Commands;
using EESimulator.Core.Models;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.General;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.UI;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Engine;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using EEE.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EEE.ViewModels;
public partial class EEWorkspaceViewModel
{
    [RelayCommand]
    public void OpenCreateCircuitComponent()
    {
        if (SelectedComponent == null)
        {
            StatusText = "Select a component in the circuit you want to package first.";
            return;
        }

        var island = FindConnectedCircuitIsland(SelectedComponent);
        if (island.Count < 2)
        {
            StatusText = "A reusable circuit component needs a connected circuit with at least two components.";
            return;
        }
        if (island.Any(component => component is CustomCircuitComponent))
        {
            StatusText = "Nested reusable circuit components are not supported. Expand the existing custom component first.";
            return;
        }

        var islandIds = island.Select(component => component.Id).ToHashSet();
        var connectedPins = Circuit.Connections
            .Where(connection => islandIds.Contains(connection.FromComponentId) && islandIds.Contains(connection.ToComponentId))
            .SelectMany(connection => new[]
            {
                (connection.FromComponentId, connection.FromPin),
                (connection.ToComponentId, connection.ToPin)
            }).ToHashSet();
        foreach (var tunnelGroup in island.OfType<TunnelComponent>().GroupBy(tunnel => tunnel.TunnelLabel, StringComparer.OrdinalIgnoreCase))
        {
            if (tunnelGroup.Count() < 2) continue;
            foreach (var tunnel in tunnelGroup)
                if (tunnel.Pins.Count > 0) connectedPins.Add((tunnel.Id, tunnel.Pins[0].Id));
        }

        var openPins = island.SelectMany(component => component.Pins
                .Where(pin => !connectedPins.Contains((component.Id, pin.Id)))
                .Select(pin => (Component: component, Pin: pin)))
            .OrderBy(item => item.Component.Reference, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Pin.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (openPins.Count == 0)
        {
            StatusText = "This circuit has no open pins to expose. Leave at least one terminal open before packaging it.";
            return;
        }

        NewCircuitComponentPorts.Clear();
        for (int index = 0; index < openPins.Count; index++)
        {
            var item = openPins[index];
            bool leftSide = index < (openPins.Count + 1) / 2;
            int row = leftSide ? index : index - (openPins.Count + 1) / 2;
            NewCircuitComponentPorts.Add(new CustomCircuitPortDraft(
                item.Component.Id, item.Pin.Id, item.Component.Reference, item.Pin.Name,
                $"N{index + 1}", leftSide ? -28 : 28, (row - (openPins.Count / 4)) * 14));
        }

        NewCircuitComponentName = string.IsNullOrWhiteSpace(SelectedComponent.Reference)
            ? "CustomCircuit"
            : $"{SelectedComponent.Reference}Module";
        NewCircuitComponentSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 48\"><rect x=\"4\" y=\"4\" width=\"56\" height=\"40\" rx=\"6\" fill=\"#161B22\" stroke=\"#58A6FF\" stroke-width=\"3\"/><path d=\"M0 16h10M54 32h10\" stroke=\"#58A6FF\" stroke-width=\"3\"/></svg>";
        CreateCircuitComponentStatus = $"{island.Count} connected components; {openPins.Count} open pins will become named component nodes.";
        IsCreateCircuitComponentDialogVisible = true;
    }

    [RelayCommand]
    public void CancelCreateCircuitComponent() => IsCreateCircuitComponentDialogVisible = false;

    [RelayCommand]
    private Task CreateSelectedCircuitComponentLocallyAsync()
        => CreateSelectedCircuitComponentAsync(publishOnline: false);

    [RelayCommand]
    private Task CreateSelectedCircuitComponentAndPublishAsync()
        => CreateSelectedCircuitComponentAsync(publishOnline: true);

    public async Task CreateSelectedCircuitComponentAsync(bool publishOnline)
    {
        if (SelectedComponent == null) return;
        string name = NewCircuitComponentName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CreateCircuitComponentStatus = "Enter a component name.";
            return;
        }
        if (NewCircuitComponentPorts.Count == 0 || NewCircuitComponentPorts.Any(port => string.IsNullOrWhiteSpace(port.Name)))
        {
            CreateCircuitComponentStatus = "Every exposed terminal needs a name.";
            return;
        }
        if (NewCircuitComponentPorts.Select(port => port.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != NewCircuitComponentPorts.Count)
        {
            CreateCircuitComponentStatus = "Terminal names must be unique.";
            return;
        }

        try
        {
            var island = FindConnectedCircuitIsland(SelectedComponent);
            var islandIds = island.Select(component => component.Id).ToHashSet();
            var subCircuit = new CircuitNode { Title = name, Description = "Reusable EESimulator circuit component" };
            foreach (var component in island)
            {
                var clone = component.Clone();
                clone.Id = component.Id;
                subCircuit.AddChild(clone);
            }
            var internalConnections = Circuit.Connections
                .Where(connection => islandIds.Contains(connection.FromComponentId) && islandIds.Contains(connection.ToComponentId))
                .ToList();
            foreach (var connection in internalConnections)
            {
                var clone = new ConnectionNode(connection.FromComponentId, connection.FromPin,
                    connection.ToComponentId, connection.ToPin, connection.NetName)
                {
                    Id = connection.Id,
                    Resistance = connection.Resistance
                };
                foreach (var point in connection.RoutingPoints)
                    clone.RoutingPoints.Add(new Point2D(point.X, point.Y));
                subCircuit.AddChild(clone);
            }

            string definitionId = Guid.NewGuid().ToString("N");
            var ports = NewCircuitComponentPorts.Select(port => new CustomCircuitPort
            {
                Id = $"port_{Guid.NewGuid():N}",
                Name = port.Name.Trim(),
                InnerComponentId = port.InnerComponentId,
                InnerPinId = port.InnerPinId,
                RelativeX = port.RelativeX,
                RelativeY = port.RelativeY
            }).ToList();
            subCircuit.Description = CustomCircuitLibrary.AddMetadata(subCircuit.Description, definitionId, ports);
            var definition = new CustomCircuitDefinition
            {
                Id = definitionId,
                Name = name,
                SvgIcon = NewCircuitComponentSvg,
                CircuitJson = EeePlatformSerializer.ExportToEee(subCircuit),
                Ports = ports
            };

            await CustomCircuitLibrary.SaveAsync(definition);
            _customCircuitDefinitions[definition.Id] = definition;
            RefreshCustomCircuitPalette();

            string onlineStatus = string.Empty;
            if (publishOnline)
            {
                try
                {
                    if (!_remoteApi.IsConfigured)
                        throw new InvalidOperationException("Configure the HCVP HTTPS API URL in Settings before publishing.");
                    await _remoteApi.PublishExampleAsync(subCircuit, name, "Custom Components", definition.SvgIcon);
                    onlineStatus = " Published online.";
                }
                catch (Exception ex)
                {
                    onlineStatus = $" Saved locally; online publish failed: {ex.Message}";
                }
            }

            double x = island.Average(component => component.X);
            double y = island.Average(component => component.Y);
            foreach (var connection in Circuit.Connections.Where(connection =>
                         islandIds.Contains(connection.FromComponentId) || islandIds.Contains(connection.ToComponentId)).ToList())
                Circuit.RemoveChild(connection);
            foreach (var component in island)
                Circuit.RemoveChild(component);

            var macroReference = GetNextReference("CustomCircuit");
            var macro = new CustomCircuitComponent
            {
                Name = macroReference,
                Reference = macroReference,
                X = x,
                Y = y
            };
            macro.SetDefinition(definition);
            var addCommand = new AddComponentCommand(Circuit, macro) { Origin = CommandOrigin.Schematic };
            CommandManager.ExecuteCommand(addCommand);
            SelectedComponent = macro;
            IsCreateCircuitComponentDialogVisible = false;
            OnComponentSelected(macro);
            StatusText = $"Created reusable component '{name}'. Saved locally.{onlineStatus}";
            CircuitChanged?.Invoke();
        }
        catch (Exception ex)
        {
            CreateCircuitComponentStatus = $"Could not create component: {ex.Message}";
        }
    }



}
