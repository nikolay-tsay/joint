using System.Globalization;

namespace Joint.Mobile.Common;

/// <summary>True when the bound string has content; used to hide empty labels.</summary>
public class HasTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
