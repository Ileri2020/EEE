using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EEE.ViewModels;

public partial class MainViewModel : ObservableObject
{
	[ObservableProperty]
	public partial int Count { get; set; }

	[ObservableProperty]
	public partial string Name { get; set; } = string.Empty;

	public string WelcomeMessage => string.IsNullOrWhiteSpace(Name)
		? "Your cross-platform app starts here."
		: $"Great to have you here, {Name}.";

	partial void OnNameChanged(string value) => OnPropertyChanged(nameof(WelcomeMessage));

	[RelayCommand]
	private void IncrementCounter() => Count++;

	[RelayCommand]
	private void ResetCounter() => Count = 0;
}
