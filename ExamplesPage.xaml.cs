using EEE.ViewModels;
using Microsoft.Maui.Controls;

namespace EEE;

public partial class ExamplesPage : ContentPage
{
    private readonly ExamplesViewModel _vm;

    public ExamplesPage(ExamplesViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommunityExamplesAsync();
    }
}
