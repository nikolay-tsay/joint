using CommunityToolkit.Mvvm.ComponentModel;

namespace Joint.Mobile.Features.Insights;

/// <summary>
/// One cell of the period picker's month grid. Cells before the 1st and after the last day
/// are blank, so every month fills the same seven columns.
/// </summary>
public partial class CalendarDay(DateOnly? date, int row, int column, bool isToday) : ObservableObject
{
    public DateOnly? Date { get; } = date;

    public string Text => Date?.Day.ToString() ?? "";

    public int Row { get; } = row;

    public int Column { get; } = column;

    public bool IsToday { get; } = isToday;

    /// <summary>The first or last day picked.</summary>
    [ObservableProperty]
    public partial bool IsEdge { get; set; }

    /// <summary>A day between the first and last day picked.</summary>
    [ObservableProperty]
    public partial bool IsBetween { get; set; }
}
