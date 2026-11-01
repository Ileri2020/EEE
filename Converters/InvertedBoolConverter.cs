using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace EEE.Converters;

/// <summary>Inverts a boolean value for XAML bindings.</summary>
public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
