using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Text;

namespace EEE;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
		AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
	}

	private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		WriteStartupFailure(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
	}

	private static void WriteStartupFailure(Exception exception)
	{
		try
		{
			var path = Path.Combine(FileSystem.AppDataDirectory, "startup-crash.log");
			File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {exception}\n---\n", Encoding.UTF8);
		}
		catch
		{
			// Crash diagnostics must never trigger another application failure.
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_services.GetRequiredService<AppShell>());
	}
}
