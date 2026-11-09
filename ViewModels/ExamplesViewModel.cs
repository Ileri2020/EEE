using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EEE.Services;
using EESimulator.Scripting;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace EEE.ViewModels;

public partial class ExamplesViewModel : ObservableObject
{
    private readonly EEWorkspaceViewModel _workspaceVm;
    private readonly IReadOnlyList<CircuitExampleItem> _allExamples;
    private readonly RemoteEeeApiClient _remoteApi;
    private readonly List<CircuitExampleItem> _communityExamples = new();

    [ObservableProperty]
    public partial ObservableCollection<CircuitExampleItem> FilteredExamples { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllSelected))]
    [NotifyPropertyChangedFor(nameof(IsOscillatorsSelected))]
    [NotifyPropertyChangedFor(nameof(IsAmplifiersSelected))]
    [NotifyPropertyChangedFor(nameof(IsDigitalLogicSelected))]
    [NotifyPropertyChangedFor(nameof(IsPassiveCircuitsSelected))]
    [NotifyPropertyChangedFor(nameof(IsFiltersSelected))]
    public partial string SelectedCategory { get; set; } = "All";

    public bool IsAllSelected => SelectedCategory == "All";
    public bool IsOscillatorsSelected => SelectedCategory == "Oscillators";
    public bool IsAmplifiersSelected => SelectedCategory == "Amplifiers";
    public bool IsDigitalLogicSelected => SelectedCategory == "Digital Logic";
    public bool IsPassiveCircuitsSelected => SelectedCategory == "Passive Circuits";
    public bool IsFiltersSelected => SelectedCategory == "Filters";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public ExamplesViewModel(EEWorkspaceViewModel workspaceVm, RemoteEeeApiClient remoteApi)
    {
        _workspaceVm = workspaceVm ?? throw new ArgumentNullException(nameof(workspaceVm));
        _remoteApi = remoteApi ?? throw new ArgumentNullException(nameof(remoteApi));
        _allExamples = ExampleCircuitService.GetExamples();
        UpdateFilteredExamples();
    }

    [RelayCommand]
    public async Task LoadCommunityExamplesAsync()
    {
        if (!_remoteApi.IsConfigured)
        {
            StatusMessage = "Set an HTTPS EESimulator API URL in Settings to browse community examples.";
            return;
        }

        try
        {
            StatusMessage = "Loading community examples…";
            var items = await _remoteApi.GetCommunityExamplesAsync();
            _communityExamples.Clear();
            _communityExamples.AddRange(items);
            UpdateFilteredExamples();
            StatusMessage = $"Loaded {items.Count} community example(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Community examples unavailable: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task PublishCurrentCircuitAsync()
    {
        if (!_remoteApi.IsConfigured)
        {
            StatusMessage = "Set an HTTPS EESimulator API URL in Settings before publishing.";
            return;
        }

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return;
        string suggestedName = string.IsNullOrWhiteSpace(_workspaceVm.Circuit.Title)
            ? "Community Circuit"
            : _workspaceVm.Circuit.Title;
        string? title = await page.DisplayPromptAsync(
            "Share Circuit",
            "Enter a title for this public community example.",
            accept: "Publish",
            cancel: "Cancel",
            initialValue: suggestedName,
            maxLength: 80);
        if (string.IsNullOrWhiteSpace(title)) return;

        try
        {
            StatusMessage = "Publishing circuit…";
            var published = await _remoteApi.PublishExampleAsync(_workspaceVm.Circuit, title.Trim());
            _communityExamples.RemoveAll(item => item.Id == published.Id);
            _communityExamples.Insert(0, published);
            UpdateFilteredExamples();
            StatusMessage = $"Published '{published.Title}' to the community library.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Publish failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void SelectCategory(string category)
    {
        SelectedCategory = category;
        UpdateFilteredExamples();
    }

    private void UpdateFilteredExamples()
    {
        FilteredExamples.Clear();
        var all = _allExamples.Concat(_communityExamples);
        var items = SelectedCategory == "All"
            ? all
            : all.Where(e => e.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));

        foreach (var item in items)
        {
            FilteredExamples.Add(item);
        }
    }

    [RelayCommand]
    public async Task LoadExample(CircuitExampleItem example)
    {
        if (example == null) return;

        try
        {
            StatusMessage = $"Loading {example.Title}…";
            var circuit = await ExampleCircuitService.LoadCircuitAsync(example);
            _workspaceVm.LoadCircuit(circuit, $"{example.Title}.eee");

            // Navigate back to workspace
            await Shell.Current.GoToAsync("//workspace");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load error: {ex.Message}";
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
            {
                await page.DisplayAlertAsync("Error Loading Example", ex.Message, "OK");
            }
        }
    }

    [RelayCommand]
    public async Task ExportExample(CircuitExampleItem example)
    {
        if (example == null) return;

        try
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs) || !Directory.Exists(docs))
            {
                docs = FileSystem.AppDataDirectory;
            }
            string targetDir = Path.Combine(docs, "EESimulator", "Examples");

            string destPath = await ExampleCircuitService.ExportExampleAsync(example, targetDir);
            StatusMessage = $"Exported to {Path.GetFileName(destPath)}";

            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
            {
                await page.DisplayAlertAsync("Example Exported", $"Exported {example.Title} to:\n{destPath}", "OK");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
            {
                await page.DisplayAlertAsync("Export Error", ex.Message, "OK");
            }
        }
    }

    [RelayCommand]
    public async Task CopyPythonScript(CircuitExampleItem example)
    {
        if (example == null) return;

        try
        {
            var circuit = await ExampleCircuitService.LoadCircuitAsync(example);
            string script = PythonCodeGenerator.GenerateFullScript(circuit);
            await Clipboard.Default.SetTextAsync(script);
            StatusMessage = $"Copied Python script for {example.Title}";

            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
            {
                await page.DisplayAlertAsync("Python Script Copied", $"Script for '{example.Title}' copied to clipboard!\nPaste it into the Script Viewport to execute.", "OK");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Script error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task BackToWorkspace()
    {
        await Shell.Current.GoToAsync("//workspace");
    }
}
