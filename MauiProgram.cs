using Microsoft.Extensions.Logging;
using EEE.ViewModels;
using UraniumUI;

namespace EEE;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseUraniumUI()
            .UseUraniumUIMaterial()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ─── EESimulator Services ──────────────────────────────────────────
        builder.Services.AddSingleton<EEWorkspaceViewModel>();
        builder.Services.AddSingleton<WorkspacePage>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<HelpPage>();
        builder.Services.AddSingleton<AppShell>();

        // Keep legacy MainPage registered for now (can be removed later)
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
