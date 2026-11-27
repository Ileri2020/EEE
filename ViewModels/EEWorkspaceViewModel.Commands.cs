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
    private void SelectTool(string tool)
    {
        ActiveTool = tool;
        StatusText = tool switch
        {
            "Wire" => "Wire tool selected: tap a source pin, then a destination pin",
            "Pan" => "Pan tool selected: drag the viewport",
            "Move" => "Move tool selected: drag the selected component",
            "Cut" => "Cut tool selected: tap a wire",
            "Label" => "Label tool selected",
            _ => "Select tool selected"
        };
    }

    [RelayCommand]
    private void RecenterViewport()
    {
        if (!Circuit.FindChildren<ComponentNode>().Any(component => component is not WireComponent))
        {
            StatusText = "No circuit components to center";
            return;
        }

        ViewportRecenterRequested?.Invoke();
        StatusText = "Centered on the circuit";
    }

    [RelayCommand]
    private void ZoomIn() => ViewportZoomRequested?.Invoke(1.25f);

    [RelayCommand]
    private void ZoomOut() => ViewportZoomRequested?.Invoke(1f / 1.25f);

    [RelayCommand]
    private void FitToScreen()
    {
        if (!Circuit.FindChildren<ComponentNode>().Any(component => component is not WireComponent))
        {
            StatusText = "No circuit components to fit";
            return;
        }

        ViewportFitToScreenRequested?.Invoke();
        StatusText = "Fitted the circuit to the screen";
    }

    public void CommitComponentMove(ComponentNode component, double oldX, double oldY, double newX, double newY)
    {
        if (Math.Abs(oldX - newX) < 0.01 && Math.Abs(oldY - newY) < 0.01) return;

        component.X = oldX;
        component.Y = oldY;
        CommandManager.ExecuteCommand(new MoveComponentCommand(Circuit, component.Name, newX, newY)
        {
            Origin = CommandOrigin.Schematic
        });
        if (component.HasLogicalPlacement)
        {
            PythonApi.SetComponentProperty(component.Name, nameof(ComponentNode.LogicalRow), null);
            PythonApi.SetComponentProperty(component.Name, nameof(ComponentNode.LogicalColumn), null);
        }
        ApplyLogicalSchematicLayout();
        CircuitChanged?.Invoke();
    }

    [RelayCommand]
    private void ToggleHelp() => IsHelpVisible = !IsHelpVisible;

    [RelayCommand]
    private async Task CopyScriptTextAsync()
    {
        try
        {
            await Clipboard.Default.SetTextAsync(ScriptText ?? string.Empty);
            StatusText = "Script copied to clipboard ✓";
        }
        catch (Exception ex)
        {
            StatusText = $"Copy failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CopyConsoleOutputAsync()
    {
        try
        {
            await Clipboard.Default.SetTextAsync(ConsoleOutput ?? string.Empty);
            StatusText = "Console output copied to clipboard ✓";
        }
        catch (Exception ex)
        {
            StatusText = $"Copy failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleAiPrompt() => IsAiPromptExpanded = !IsAiPromptExpanded;

    [RelayCommand]
    private void ToggleSchematicAiSidebar()
    {
        IsSchematicAiSidebarVisible = !IsSchematicAiSidebarVisible;
        StatusText = IsSchematicAiSidebarVisible ? "AI assistant opened in the sidebar" : "AI assistant closed";
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        try
        {
            await Shell.Current.GoToAsync("//settings");
            StatusText = "Settings opened";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open Settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenAccount()
    {
        try
        {
            await Shell.Current.GoToAsync("account-detail");
            StatusText = "Account opened";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open Account: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task OpenExamplesPage()
    {
        try
        {
            await Shell.Current.GoToAsync("//examples");
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open examples: {ex.Message}";
        }
    }

    public void LoadCircuit(
        CircuitNode newCircuit,
        string projectName,
        string? filePath = null,
        string? generatedScript = null)
    {
        if (newCircuit == null) return;

        // Clear existing circuit nodes
        while (Circuit.Children.Count > 0)
        {
            Circuit.RemoveChild(Circuit.Children[0]);
        }

        // Add children from loaded circuit
        foreach (var child in newCircuit.Children.ToList())
        {
            Circuit.AddChild(child);
        }

        Circuit.Title = newCircuit.Title;
        Circuit.Description = newCircuit.Description;
        Circuit.LogicalGridRowSpacing = newCircuit.LogicalGridRowSpacing;
        Circuit.LogicalGridColumnSpacing = newCircuit.LogicalGridColumnSpacing;
        LogicalGridLayoutResult layout = LogicalGridSchematicLayout.Calculate(Circuit);
        foreach (LogicalGridPlacement placement in layout.Placements)
        {
            placement.Component.X = placement.X;
            placement.Component.Y = placement.Y;
        }
        RefreshGroundGroupOptions();
        ProjectName = projectName;
        CurrentFilePath = filePath;

        if (newCircuit.SimulationSettings is EeeSimulationSettingsDto simSettings)
        {
            SelectedAnalysisType = simSettings.AnalysisType;
            SimTransientStartTime = simSettings.TransientStartTime;
            SimTransientStopTime = simSettings.TransientStopTime > 0 ? simSettings.TransientStopTime : 0.01;
            SimTransientStepTime = simSettings.TransientStepTime > 0 ? simSettings.TransientStepTime : 1e-5;
            SimAcSweepType = simSettings.AcSweepType;
            SimAcPoints = simSettings.AcPoints > 0 ? simSettings.AcPoints : 10;
            SimAcStartFrequency = simSettings.AcStartFrequency > 0 ? simSettings.AcStartFrequency : 1.0;
            SimAcStopFrequency = simSettings.AcStopFrequency > 0 ? simSettings.AcStopFrequency : 1e5;
            SimDcSweepSource = string.IsNullOrEmpty(simSettings.DcSweepSource) ? "V1" : simSettings.DcSweepSource;
            SimDcStart = simSettings.DcStart;
            SimDcStop = simSettings.DcStop;
            SimDcStep = simSettings.DcStep > 0 ? simSettings.DcStep : 0.1;
        }

        // Notify the script synchronizer so the Python script view reflects the loaded circuit (§5.A)
        _sync.OnCircuitReplaced(Circuit, generatedScript);
        ScriptText = _sync.CurrentScript;

        StatusText = $"Loaded circuit: {projectName}";
        CircuitChanged?.Invoke();
    }

    public void LoadScriptIntoWorkspace(string script, string scriptName)
    {
        ScriptText = script ?? string.Empty;
        IsAnalyticsViewActive = false;
        IsScriptViewActive = true;
        UI.Workspace.SetViewport("Script");
        StatusText = $"Loaded recommended script: {scriptName}. Review it before running.";
    }


    private void SetPanTool()
    {
        ActiveTool = "Pan";
        StatusText = "Pan tool selected";
    }

    private void SetSelectTool()
    {
        ActiveTool = "Select";
        StatusText = "Select tool selected";
    }

    [RelayCommand]
    private void Undo()
    {
        CommandManager.Undo();
        RefreshGroundGroupOptions();
        StatusText = "Undo";
    }

    [RelayCommand]
    private void Redo()
    {
        CommandManager.Redo();
        RefreshGroundGroupOptions();
        StatusText = "Redo";
    }

    [RelayCommand]
    private void ExpandLogicalGridRows() =>
        AdjustLogicalGridSpacing(rows: true, delta: CircuitNode.LogicalGridSpacingStep);

    [RelayCommand]
    private void ContractLogicalGridRows() =>
        AdjustLogicalGridSpacing(rows: true, delta: -CircuitNode.LogicalGridSpacingStep);

    [RelayCommand]
    private void ExpandLogicalGridColumns() =>
        AdjustLogicalGridSpacing(rows: false, delta: CircuitNode.LogicalGridSpacingStep);

    [RelayCommand]
    private void ContractLogicalGridColumns() =>
        AdjustLogicalGridSpacing(rows: false, delta: -CircuitNode.LogicalGridSpacingStep);

    private void AdjustLogicalGridSpacing(bool rows, double delta)
    {
        double rowSpacing = Circuit.LogicalGridRowSpacing;
        double columnSpacing = Circuit.LogicalGridColumnSpacing;
        if (rows)
            rowSpacing = Math.Max(CircuitNode.MinimumLogicalGridSpacing, rowSpacing + delta);
        else
            columnSpacing = Math.Max(CircuitNode.MinimumLogicalGridSpacing, columnSpacing + delta);

        CommandManager.ExecuteCommand(new SetLogicalGridSpacingCommand(Circuit, rowSpacing, columnSpacing));
    }

    [RelayCommand]
    public void SaveProject()
    {
        IsFileMenuOpen = false;
#if ANDROID
        // Android exports route through Save As and MediaStore into Downloads/EEEngine.
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            SaveProjectAs();
            return;
        }
#endif
        if (string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            SaveProjectAs();
            return;
        }

        try
        {
            SaveToPath(CurrentFilePath);
            StatusText = $"Saved: {System.IO.Path.GetFileName(CurrentFilePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        IsFileMenuOpen = false;
        try
        {
            var file = await FilePicker.Default.PickAsync();
            if (file == null) return;

            string extension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            await using var stream = await file.OpenReadAsync();
            using var reader = new System.IO.StreamReader(stream);
            string content = await reader.ReadToEndAsync();

            CircuitNode loaded = extension switch
            {
                ".eee" => EeePlatformSerializer.LoadFromEeeJson(content),
                ".eesim" => EesimSerializer.LoadFromJson(content)
                    ?? throw new System.IO.InvalidDataException("The selected EESIM file is empty or invalid."),
                ".json" => IsEeeProjectJson(content)
                    ? EeePlatformSerializer.LoadFromEeeJson(content)
                    : EesimSerializer.LoadFromJson(content)
                        ?? throw new System.IO.InvalidDataException("The selected JSON project is empty or invalid."),
                ".kicad_sch" => KiCadImporter.ImportFromKiCadSch(content, file.FileName),
                ".cir" or ".sp" => SpiceNetlistImporter.ImportFromSpice(content, file.FileName),
                _ => throw new NotSupportedException(
                    $"'{extension}' is not a supported project format. Open .eee, .eesim, .json, .kicad_sch, .cir, or .sp files.")
            };

            foreach (var child in Circuit.Children.ToList())
                Circuit.RemoveChild(child);
            foreach (var child in loaded.Children.ToList())
                Circuit.AddChild(child);
            Circuit.Title = loaded.Title;
            Circuit.Description = loaded.Description;
            RefreshGroundGroupOptions();
            CommandManager.Clear();
            _sync.OnCircuitReplaced(Circuit);
            ScriptText = _sync.CurrentScript;

#if WINDOWS
            CurrentFilePath = file.FullPath;
#else
            CurrentFilePath = null;
#endif
            ProjectName = file.FileName;
            StatusText = $"Opened: {file.FileName}";
            CircuitChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"Open project failed: {ex.Message}";
        }
    }

    private static bool IsEeeProjectJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Name.Equals("FileType", StringComparison.OrdinalIgnoreCase))
                return property.Value.GetString() == "EEE_PLATFORM_SCHEMATIC";
        }
        return false;
    }

    [RelayCommand]
    private void ToggleFileMenu() => IsFileMenuOpen = !IsFileMenuOpen;

    [RelayCommand]
    private async Task SaveProjectToServerAsync()
    {
        IsFileMenuOpen = false;
        try
        {
            StatusText = "Saving project to the server…";
            var result = await _remoteApi.SaveProjectAsync(Circuit, System.IO.Path.GetFileNameWithoutExtension(ProjectName));
            StatusText = string.IsNullOrWhiteSpace(result.Id)
                ? "Project saved to the server."
                : $"Project saved to the server (ID: {result.Id}).";
        }
        catch (Exception ex)
        {
            StatusText = $"Server save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveProjectAsOnlineAsync()
    {
        string title = System.IO.Path.GetFileNameWithoutExtension(SaveFileName).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            StatusText = "Filename cannot be empty.";
            return;
        }

        try
        {
            StatusText = "Saving project online…";
            var result = await _remoteApi.SaveProjectAsync(Circuit, title);
            ProjectName = title + ".eee";
            IsSaveAsModalVisible = false;
            StatusText = string.IsNullOrWhiteSpace(result.Id)
                ? "Project saved online."
                : $"Project saved online (ID: {result.Id}).";
        }
        catch (Exception ex)
        {
            StatusText = $"Online save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void SaveProjectAs()
    {
        IsFileMenuOpen = false;
#if ANDROID
        if (DeviceInfo.Platform == DevicePlatform.Android)
            SaveDestinationFolder = "Downloads/EEEngine";
        else
#endif
        if (string.IsNullOrWhiteSpace(SaveDestinationFolder))
            SaveDestinationFolder = GetDefaultSaveDirectory();

        string currentName = !string.IsNullOrWhiteSpace(CurrentFilePath)
            ? System.IO.Path.GetFileNameWithoutExtension(CurrentFilePath)
            : (!string.IsNullOrWhiteSpace(ProjectName) ? System.IO.Path.GetFileNameWithoutExtension(ProjectName) : "Circuit1");

        SaveFileName = currentName;
        IsSaveAsModalVisible = true;
    }

    [RelayCommand]
    public void CloseSaveAsDialog()
    {
        IsSaveAsModalVisible = false;
    }

    [RelayCommand]
    public void SelectSaveFormat(string format)
    {
        SelectedSaveFormat = format;
    }

    [RelayCommand]
    public async Task ConfirmSaveAs()
    {
        if (string.IsNullOrWhiteSpace(SaveFileName))
        {
            StatusText = "Filename cannot be empty.";
            return;
        }

        string cleanName = System.IO.Path.GetFileNameWithoutExtension(SaveFileName).Trim();
        string ext = SelectedSaveFormat switch
        {
            "eee" => ".eee",
            "eesim" => ".eesim",
            "kicad" => ".kicad_sch",
            "spice" => ".cir",
            "python" => ".py",
            _ => ".eee"
        };

        if (string.IsNullOrWhiteSpace(SaveDestinationFolder))
            SaveDestinationFolder = GetDefaultSaveDirectory();

        string fullPath = System.IO.Path.Combine(SaveDestinationFolder, cleanName + ext);

        try
        {
#if ANDROID
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                string temporaryPath = System.IO.Path.Combine(
                    FileSystem.CacheDirectory,
                    $"{Guid.NewGuid():N}{ext}");
                try
                {
                    SaveToPath(temporaryPath);
                    if (OperatingSystem.IsAndroidVersionAtLeast(29))
                    {
                        await EEE.Platforms.Android.AndroidDownloadSaver.SaveAsync(temporaryPath, cleanName + ext);
                        StatusText = $"Saved to Downloads/EEEngine/{cleanName + ext}";
                    }
                    else
                    {
                        await using var stream = System.IO.File.OpenRead(temporaryPath);
                        var result = await FileSaver.Default.SaveAsync(
                            cleanName + ext,
                            stream,
                            System.Threading.CancellationToken.None);
                        if (!result.IsSuccessful)
                        {
                            StatusText = result.Exception?.Message ?? "Save cancelled.";
                            return;
                        }
                        StatusText = $"Saved locally: {result.FilePath}";
                    }

                    CurrentFilePath = null;
                    ProjectName = cleanName + ext;
                    IsSaveAsModalVisible = false;
                    return;
                }
                finally
                {
                    if (System.IO.File.Exists(temporaryPath))
                        System.IO.File.Delete(temporaryPath);
                }
            }
#endif
            SaveToPath(fullPath);
            CurrentFilePath = fullPath;
            ProjectName = System.IO.Path.GetFileName(fullPath);
            IsSaveAsModalVisible = false;
            StatusText = $"Saved: {System.IO.Path.GetFileName(fullPath)} ({SelectedSaveFormat.ToUpperInvariant()})";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
    }

    private string GetDefaultSaveDirectory()
    {
        try
        {
            // Use an app-owned location that is writable on Windows regardless of
            // Documents redirection, OneDrive policies, or the current working folder.
            string path = System.IO.Path.Combine(FileSystem.AppDataDirectory, "Projects");
            System.IO.Directory.CreateDirectory(path);
            return path;
        }
        catch
        {
            string fallback = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EESimulator", "Projects");
            System.IO.Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private void SaveToPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Choose a valid project file path.", nameof(filePath));

        string fullPath = System.IO.Path.GetFullPath(filePath);
        string? dir = System.IO.Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(dir))
            throw new IOException("The selected path has no parent directory.");
        System.IO.Directory.CreateDirectory(dir);

        string ext = System.IO.Path.GetExtension(fullPath).ToLowerInvariant();
        string tempPath = System.IO.Path.Combine(dir,
            $".{System.IO.Path.GetFileNameWithoutExtension(fullPath)}.{Guid.NewGuid():N}.tmp{ext}");
        try
        {
            Circuit.SimulationSettings = new EeeSimulationSettingsDto
            {
                AnalysisType = SelectedAnalysisType,
                TransientStartTime = SimTransientStartTime,
                TransientStopTime = SimTransientStopTime,
                TransientStepTime = SimTransientStepTime,
                AcSweepType = SimAcSweepType,
                AcPoints = SimAcPoints,
                AcStartFrequency = SimAcStartFrequency,
                AcStopFrequency = SimAcStopFrequency,
                DcSweepSource = SimDcSweepSource,
                DcStart = SimDcStart,
                DcStop = SimDcStop,
                DcStep = SimDcStep
            };

            switch (ext)
            {
                case ".eee":
                    EeePlatformSerializer.SaveToFile(Circuit, tempPath);
                    break;
                case ".eesim":
                case ".json":
                    EesimSerializer.SaveToFile(Circuit, tempPath);
                    break;
                case ".kicad_sch":
                    KiCadExporter.SaveToFile(Circuit, tempPath);
                    break;
                case ".cir":
                case ".sp":
                    System.IO.File.WriteAllText(tempPath, SpiceNetlistExporter.ExportToSpice(Circuit, BuildSimulationSettings()));
                    break;
                case ".py":
                    System.IO.File.WriteAllText(tempPath, PythonCodeGenerator.GenerateFullScript(Circuit));
                    break;
                default:
                    EeePlatformSerializer.SaveToFile(Circuit, tempPath);
                    break;
            }

            System.IO.File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }


    [RelayCommand]
    private void RotateSelectedComponent()
    {
        if (SelectedConnection != null)
        {
            RotateSelectedConnectionRoute();
            return;
        }
        var comp = SelectedComponent;
        if (comp == null)
        {
            StatusText = "No component selected — click a component first";
            return;
        }
        comp.Rotation = (comp.Rotation + 90.0) % 360.0;
        ApplyLogicalSchematicLayout();
        StatusText = $"Rotated {comp.Reference} → {comp.Rotation:F0}°";
        CircuitChanged?.Invoke();
    }

    [RelayCommand]
    private void EditSelectedComponent()
    {
        if (SelectedComponent != null)
            OpenEditDialog(SelectedComponent);
    }

    [RelayCommand]
    public void CopySelectedComponent()
    {
        if (SelectedComponent == null)
        {
            StatusText = "No component selected to copy";
            return;
        }

        var src = SelectedComponent;
        var copy = EESimulator.Core.Components.Registry.ComponentRegistry.CreateInstance(
            src.ComponentType,
            GetNextReference(src.ComponentType),
            src.X + 30,
            src.Y + 30);

        copy.Rotation = src.Rotation;
        copy.ScaleX = src.ScaleX;
        copy.ScaleY = src.ScaleY;
        copy.BreakingCurrent = src.BreakingCurrent;
        foreach (var kvp in src.ElectricalParameters)
        {
            copy.ElectricalParameters[kvp.Key] = kvp.Value;
        }
        foreach (var kvp in src.CustomProperties)
        {
            copy.CustomProperties[kvp.Key] = kvp.Value;
        }

        var cmd = new AddComponentCommand(Circuit, copy)
        {
            Origin = CommandOrigin.Schematic
        };
        CommandManager.ExecuteCommand(cmd);

        ApplyLogicalSchematicLayout();
        SelectedComponent = copy;
        OnComponentSelected(copy);
        CircuitChanged?.Invoke();
        StatusText = $"Copied {src.Reference} → {copy.Reference}";
    }

    [RelayCommand]
    public void DeleteSelectedComponent()
    {
        if (SelectedConnection != null)
        {
            var connection = SelectedConnection;
            CommandManager.ExecuteCommand(new RemoveConnectionCommand(Circuit, connection)
            {
                Origin = CommandOrigin.Schematic
            });
            SelectedConnection = null;
            CircuitChanged?.Invoke();
            RequestSimulationRefresh();
            StatusText = $"Deleted wire {connection.NetName}";
            return;
        }
        if (SelectedComponent == null)
        {
            StatusText = "No component selected to delete";
            return;
        }

        var comp = SelectedComponent;
        var cmd = new RemoveComponentCommand(Circuit, comp)
        {
            Origin = CommandOrigin.Schematic
        };
        CommandManager.ExecuteCommand(cmd);

        ApplyLogicalSchematicLayout();
        SelectedComponent = null;
        SelectedConnection = null;
        CircuitChanged?.Invoke();
        RequestSimulationRefresh();
        StatusText = $"Deleted {comp.Reference}";
    }

    private void RotateSelectedConnectionRoute()
    {
        var connection = SelectedConnection;
        if (connection == null) return;
        var from = Circuit.FindChildById<ComponentNode>(connection.FromComponentId);
        var to = Circuit.FindChildById<ComponentNode>(connection.ToComponentId);
        var fromPin = from?.GetPin(connection.FromPin);
        var toPin = to?.GetPin(connection.ToPin);
        if (from == null || to == null || fromPin == null || toPin == null)
        {
            StatusText = "Cannot rotate this wire because one of its endpoint nodes is missing.";
            return;
        }

        static Point2D PinWorldPosition(ComponentNode component, ComponentPin pin)
        {
            ComponentPinPosition local = AdaptiveComponentLayout.Get(component).GetPinPosition(pin);
            double localX = local.X;
            double localY = local.Y;
            double radians = component.Rotation * Math.PI / 180.0;
            return new Point2D(
                component.X + localX * Math.Cos(radians) - localY * Math.Sin(radians),
                component.Y + localX * Math.Sin(radians) + localY * Math.Cos(radians));
        }
        var fromPosition = PinWorldPosition(from, fromPin);
        var toPosition = PinWorldPosition(to, toPin);
        double x1 = fromPosition.X;
        double y1 = fromPosition.Y;
        double x2 = toPosition.X;
        double y2 = toPosition.Y;
        double centerX = (x1 + x2) / 2.0;
        double centerY = (y1 + y2) / 2.0;
        if (connection.RoutingPoints.Count == 0)
        {
            double middleX = (x1 + x2) / 2.0;
            connection.RoutingPoints.Add(new Point2D(middleX, y1));
            connection.RoutingPoints.Add(new Point2D(middleX, y2));
        }

        for (int index = 0; index < connection.RoutingPoints.Count; index++)
        {
            var point = connection.RoutingPoints[index];
            double dx = point.X - centerX;
            double dy = point.Y - centerY;
            connection.RoutingPoints[index] = new Point2D(centerX - dy, centerY + dx);
        }
        connection.NotifyModified(nameof(connection.RoutingPoints));
        CircuitChanged?.Invoke();
        StatusText = $"Rotated wire route {connection.NetName} 90°; endpoint nodes remain connected";
    }

    [RelayCommand]
    public void ToggleDescriptionSidebar()
    {
        IsDescriptionSidebarVisible = !IsDescriptionSidebarVisible;
        if (IsDescriptionSidebarVisible && SelectedComponent != null)
        {
            OnComponentSelected(SelectedComponent);
        }
    }

    [RelayCommand]
    public void ToggleLiveMeasurements()
    {
        IsLiveMeasurementsVisible = !IsLiveMeasurementsVisible;
        StatusText = IsLiveMeasurementsVisible
            ? "Live voltage and current overlays enabled"
            : "Live voltage and current overlays hidden";
        CircuitChanged?.Invoke();
    }

    [RelayCommand]
    public void ToggleSelectedWaveform()
    {
        if (SelectedConnection != null)
        {
            var fromComponent = Circuit.FindChildById<ComponentNode>(SelectedConnection.FromComponentId);
            var toComponent = Circuit.FindChildById<ComponentNode>(SelectedConnection.ToComponentId);
            var wireSignals = new[] { fromComponent, toComponent }.Where(source => source != null).Cast<ComponentNode>().Distinct().ToList();
            if (wireSignals.Any(source => source.ShowWaveform))
            {
                foreach (var source in wireSignals) source.ShowWaveform = false;
                IsWaveformDrawerOpen = false;
                CircuitChanged?.Invoke();
                return;
            }
            PrepareConnectionWaveform(SelectedConnection);
            return;
        }

        var component = SelectedComponent;
        if (component == null) return;

        if (component.ShowWaveform &&
            WaveformSignals.Any(signal => signal.Component.Id == component.Id))
        {
            component.ShowWaveform = false;
            RemoveWaveformSignals(component);
            StatusText = $"Waveform disabled for {component.Reference}";
        }
        else
        {
            ConfigureWaveformForComponent(component, showTerminalPicker: true);
        }
        CircuitChanged?.Invoke();
    }

    private void ConfigureWaveformForComponent(ComponentNode component, bool showTerminalPicker)
    {
        WaveformTracePickerComponent = component;
        WaveformTraceCandidates.Clear();
        foreach (var signal in CreateWaveformSignals(component))
            WaveformTraceCandidates.Add(signal);

        if (component.Pins.Count > 2 && showTerminalPicker)
        {
            IsWaveformTracePickerVisible = true;
            return;
        }

        EnableWaveformForComponent(component);
    }

    private static IEnumerable<WaveformSignal> CreateWaveformSignals(ComponentNode component)
    {
        if (component is EESimulator.Core.Components.General.ProbeComponent probe)
        {
            yield return new WaveformSignal(
                probe, string.Empty, $"{probe.Reference}: probe voltage", WaveformSignalKind.ComponentVoltage);
        }
        else if (component.Pins.Count > 2)
        {
            foreach (var pin in component.Pins)
                yield return new WaveformSignal(component, pin.Id,
                    $"{component.Reference}: {pin.Name} voltage", WaveformSignalKind.PinVoltage);
        }
        else if (component.Pins.Count == 1)
        {
            var pin = component.Pins[0];
            yield return new WaveformSignal(component, pin.Id,
                $"{component.Reference}: {pin.Name} voltage", WaveformSignalKind.PinVoltage);
        }
        else
        {
            yield return new WaveformSignal(component, string.Empty,
                $"{component.Reference}: component voltage", WaveformSignalKind.ComponentVoltage);
            if (component.Pins.Count != 1)
                yield return new WaveformSignal(component, string.Empty,
                    $"{component.Reference}: component current", WaveformSignalKind.ComponentCurrent);
        }
    }

    private void PrepareConnectionWaveform(ConnectionNode connection)
    {
        var from = Circuit.FindChildById<ComponentNode>(connection.FromComponentId);
        var to = Circuit.FindChildById<ComponentNode>(connection.ToComponentId);
        var fromPin = from?.GetPin(connection.FromPin);
        var toPin = to?.GetPin(connection.ToPin);
        if (from == null || to == null || fromPin == null || toPin == null)
        {
            StatusText = "Cannot plot this wire because an endpoint node is missing.";
            return;
        }

        WaveformTraceCandidates.Clear();
        WaveformTracePickerComponent = from;
        WaveformTraceCandidates.Add(new WaveformSignal(from, fromPin.Id, $"{connection.NetName}: {from.Reference}:{fromPin.Name}", WaveformSignalKind.PinVoltage));
        WaveformTraceCandidates.Add(new WaveformSignal(to, toPin.Id, $"{connection.NetName}: {to.Reference}:{toPin.Name}", WaveformSignalKind.PinVoltage));
        IsWaveformTracePickerVisible = true;
    }

    private void EnableWaveformForComponent(ComponentNode component)
    {
        component.ShowWaveform = true;
        foreach (var source in WaveformTraceCandidates.Where(signal => signal.IsSelected).Select(signal => signal.Component).Distinct())
            source.ShowWaveform = true;
        WaveformComponent = component;
        AddSelectedWaveformSignals(WaveformTraceCandidates);
        SyncEnabledWaveformSignals();
        IsWaveformDrawerOpen = true;
        IsWaveformTracePickerVisible = false;
        StatusText = $"Waveform enabled for {component.Reference}; graphing {WaveformGraphSummary}";
        OnPropertyChanged(nameof(WaveformGraphSummary));
        CircuitChanged?.Invoke();
        RequestSimulationRefresh();
    }

    [RelayCommand]
    public void SetAllWaveformSignals(object? parameter)
    {
        bool selected = parameter switch
        {
            bool b => b,
            string str => bool.TryParse(str, out var parsed) && parsed,
            _ => true
        };
        foreach (var signal in WaveformTraceCandidates)
            signal.IsSelected = selected;
    }

    [RelayCommand]
    public void ConfirmWaveformTraceSelection()
    {
        var component = WaveformTracePickerComponent;
        if (component == null) return;
        if (!WaveformTraceCandidates.Any(signal => signal.IsSelected))
        {
            StatusText = "Select at least one terminal waveform.";
            return;
        }
        EnableWaveformForComponent(component);
    }

    private void AddSelectedWaveformSignals(IEnumerable<WaveformSignal> candidates)
    {
        foreach (var signal in candidates.Where(candidate => candidate.IsSelected))
        {
            bool alreadyAdded = WaveformSignals.Any(existing =>
                existing.Component.Id == signal.Component.Id &&
                existing.SignalId.Equals(signal.SignalId, StringComparison.OrdinalIgnoreCase) &&
                existing.Kind == signal.Kind);
            if (!alreadyAdded)
                WaveformSignals.Add(signal);
        }
        OnPropertyChanged(nameof(WaveformGraphSummary));
    }

    private void RemoveWaveformSignals(ComponentNode component)
    {
        foreach (var signal in WaveformSignals.Where(signal => signal.Component.Id == component.Id).ToArray())
            WaveformSignals.Remove(signal);
        if (WaveformComponent?.Id == component.Id)
            WaveformComponent = WaveformSignals.FirstOrDefault()?.Component;
        OnPropertyChanged(nameof(WaveformGraphSummary));
    }

    private void SyncEnabledWaveformSignals()
    {
        foreach (var signal in WaveformSignals.Where(signal =>
                     signal.Component is EESimulator.Core.Components.General.ProbeComponent &&
                     !AreProbeWaveformsVisible).ToArray())
            WaveformSignals.Remove(signal);

        foreach (var component in Circuit.Components.Where(component =>
                     component.ShowWaveform ||
                     (AreProbeWaveformsVisible && component is EESimulator.Core.Components.General.ProbeComponent)))
        {
            if (component is EESimulator.Core.Components.General.ProbeComponent &&
                !AreProbeWaveformsVisible)
                continue;
            if (WaveformSignals.Any(signal => signal.Component.Id == component.Id))
                continue;
            foreach (var signal in CreateWaveformSignals(component))
                WaveformSignals.Add(signal);
        }

        OnPropertyChanged(nameof(WaveformGraphSummary));
    }

    [RelayCommand]
    public void CancelWaveformTraceSelection() => IsWaveformTracePickerVisible = false;

    [RelayCommand]
    private void ToggleProbeWaveforms() => AreProbeWaveformsVisible = !AreProbeWaveformsVisible;

    [RelayCommand]
    public void ApplyDescriptionSidebarChanges()
    {
        if (SelectedConnection is { } connection)
        {
            string netName = EditConnectionNetName.Trim();
            if (string.IsNullOrWhiteSpace(netName))
            {
                StatusText = "Wire net name cannot be empty.";
                return;
            }
            if (!double.IsFinite(EditConnectionResistance) || EditConnectionResistance < 0)
            {
                StatusText = "Wire resistance must be a finite, non-negative value.";
                return;
            }

            connection.NetName = netName;
            connection.Resistance = EditConnectionResistance;
            OnPropertyChanged(nameof(SelectedConnectionDetails));
            ActiveComponentInfo = $"Wire {connection.NetName}: {SelectedConnectionDetails}";
            CircuitChanged?.Invoke();
            RequestSimulationRefresh();
            StatusText = $"Updated wire properties for {connection.NetName}";
            return;
        }

        if (SelectedComponent == null) return;
        if (SelectedComponent is ACSourceComponent acSource)
        {
            if (!AcSourceWaveformTypes.Contains(EditAcSourceWaveformType, StringComparer.OrdinalIgnoreCase))
            {
                StatusText = "Choose a supported AC source waveform.";
                return;
            }
            if (!double.IsFinite(EditAcSourceVoltage) || EditAcSourceVoltage < 0 ||
                !double.IsFinite(EditAcSourceFrequency) || EditAcSourceFrequency <= 0 ||
                !double.IsFinite(EditAcSourcePhaseDegrees) || !double.IsFinite(EditAcSourceOffset))
            {
                StatusText = "AC source voltage and phase must be finite, and frequency must be greater than zero.";
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(EditName))
        {
            SelectedComponent.Name = EditName.Trim();
            SelectedComponent.Reference = EditName.Trim();
        }
        SelectedComponent.CustomProperties["Description"] = EditDescription;
        SelectedComponent.BreakingCurrent = Math.Max(EditBreakingCurrent, 0.0);
        if (SelectedComponent is not ACSourceComponent && !string.IsNullOrEmpty(EditParamName))
        {
            SelectedComponent.ElectricalParameters[EditParamName] = EditParamValue;
        }
        if (SelectedComponent is ACSourceComponent editableAcSource)
        {
            editableAcSource.WaveformType = EditAcSourceWaveformType;
            editableAcSource.Frequency = EditAcSourceFrequency;
            editableAcSource.PhaseDegrees = EditAcSourcePhaseDegrees;
            editableAcSource.Offset = EditAcSourceOffset;
            editableAcSource.Voltage = EditAcSourceVoltage;
        }
        SelectedComponent.ShowLiveProbe = EditShowLiveProbe;
        SelectedComponent.ShowWaveform = EditShowWaveform;
        if (SelectedComponent is GroundComponent ground &&
            !ground.GroundGroup.Equals(EditGroundGroup, StringComparison.Ordinal))
        {
            string group = string.IsNullOrWhiteSpace(EditGroundGroup)
                ? GroundComponent.DefaultGroundGroup
                : EditGroundGroup.Trim();
            CommandManager.ExecuteCommand(new SetComponentPropertyCommand(
                Circuit, SelectedComponent.Name, nameof(GroundComponent.GroundGroup), group)
            {
                Origin = CommandOrigin.Schematic
            });
            EditGroundGroup = ground.GroundGroup;
            RefreshGroundGroupOptions();
        }
        else if (SelectedComponent is ProbeComponent probe &&
                 !probe.ReferenceGroundGroup.Equals(EditGroundGroup, StringComparison.OrdinalIgnoreCase))
        {
            string group = string.IsNullOrWhiteSpace(EditGroundGroup)
                ? GroundComponent.DefaultGroundGroup
                : EditGroundGroup.Trim();
            CommandManager.ExecuteCommand(new SetComponentPropertyCommand(
                Circuit, SelectedComponent.Name, nameof(ProbeComponent.ReferenceGroundGroup), group)
            {
                Origin = CommandOrigin.Schematic
            });
            EditGroundGroup = probe.ReferenceGroundGroup;
        }
        SelectedComponent.NotifyModified(SelectedComponent.Reference);
        CircuitChanged?.Invoke();
        RequestSimulationRefresh();
        StatusText = $"Updated description & properties for {SelectedComponent.Reference}";
    }

}
