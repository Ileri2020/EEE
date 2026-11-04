using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace EEE.ViewModels;

public partial class PowerSystemPropertyItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsEditable { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    public ICommand? ReturnCommand { get; set; }
}
