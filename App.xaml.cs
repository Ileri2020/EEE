using Microsoft.Extensions.DependencyInjection;

namespace EEE;

public partial class App : Application
{
	public App(AppShell appShell)
	{
		InitializeComponent();
		_appShell = appShell;
	}

	private readonly AppShell _appShell;

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_appShell);
	}
}
