using Microsoft.UI.Xaml;
using System;
using System.IO;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace EEE.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		UnhandledException += (_, e) =>
		{
			try
			{
				var directory = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"EEE");
				Directory.CreateDirectory(directory);
				File.AppendAllText(
					Path.Combine(directory, "windows-xaml-crash.log"),
					$"[{DateTimeOffset.Now:O}] {e.Message}{Environment.NewLine}{e.Exception}{Environment.NewLine}---{Environment.NewLine}");
			}
			catch
			{
				// Crash diagnostics must not cause another startup failure.
			}
		};
		// Subscribe before loading App.xaml: InitializeComponent invokes WinUI's
		// LoadComponent, which can itself throw the startup XAML exception.
		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

