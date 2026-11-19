using Android.App;
using Android.Content.PM;
using Android.OS;
using System.IO;
using System.Threading;

namespace EEE;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	private static int _python3AndroidSmokeRequested;

	protected override void OnCreate(Bundle? savedInstanceState)
	{
		string requestPath = Path.Combine(
			Android.App.Application.Context.FilesDir!.AbsolutePath,
			AndroidPython3SmokeRunner.RequestFileName);
		if (File.Exists(requestPath))
		{
			File.Delete(requestPath);
			Interlocked.Exchange(ref _python3AndroidSmokeRequested, 1);
		}

		base.OnCreate(savedInstanceState);
	}

	public static bool ConsumePython3AndroidSmokeRequest() =>
		Interlocked.Exchange(ref _python3AndroidSmokeRequested, 0) == 1;
}
