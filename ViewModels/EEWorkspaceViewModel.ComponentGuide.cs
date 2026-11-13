using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Storage;
using EEE.Services;
using EESimulator.Core.Models;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EEE.ViewModels;

public partial class EEWorkspaceViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDescriptionPropertiesTab))]
    [NotifyPropertyChangedFor(nameof(IsDescriptionGuideTab))]
    public partial string SelectedDescriptionTab { get; set; } = "Properties";

    [ObservableProperty]
    public partial ComponentGuide? SelectedComponentGuide { get; set; }

    public bool IsDescriptionPropertiesTab => SelectedDescriptionTab == "Properties";
    public bool IsDescriptionGuideTab => SelectedDescriptionTab == "Guide";

    partial void OnSelectedComponentChanged(ComponentNode? value)
        => SelectedComponentGuide = ComponentGuideService.Create(value);

    [RelayCommand]
    private void SelectDescriptionTab(string tab)
    {
        if (tab is "Properties" or "Guide")
            SelectedDescriptionTab = tab;
    }

    [RelayCommand]
    private async Task CopySelectedComponentGuideAsync()
    {
        if (SelectedComponentGuide == null)
        {
            StatusText = "Select a component before copying its guide.";
            return;
        }

        try
        {
            await Clipboard.Default.SetTextAsync(ComponentGuideService.Format(SelectedComponentGuide));
            StatusText = $"Copied the {SelectedComponentGuide.Name} guide.";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not copy the component guide: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportAllComponentGuidesAsync()
    {
        string? temporaryPath = null;
        try
        {
            var guides = ComponentGuideService.CreateAll();
            string content = ComponentGuideService.FormatAll(guides);
            const string fileName = "EEE-Component-Guides.txt";

#if ANDROID
            temporaryPath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(temporaryPath, content, Encoding.UTF8);
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                await EEE.Platforms.Android.AndroidDownloadSaver.SaveAsync(temporaryPath, fileName);
                StatusText = $"Saved all component guides to Downloads/EEEngine/{fileName}";
                return;
            }

            await using (var stream = File.OpenRead(temporaryPath))
            {
                var result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);
                if (!result.IsSuccessful)
                {
                    StatusText = result.Exception?.Message ?? "Saving component guides was cancelled.";
                    return;
                }
                StatusText = $"Saved all component guides: {result.FilePath}";
            }
            return;
#else
            string outputPath = Path.Combine(GetDefaultSaveDirectory(), fileName);
            await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8);
            StatusText = $"Saved guides for {guides.Count} components: {outputPath}";
#endif
        }
        catch (Exception ex)
        {
            StatusText = $"Could not save component guides: {ex.Message}";
        }
        finally
        {
            if (temporaryPath != null && File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
