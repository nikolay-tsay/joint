using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;

namespace Joint.Mobile.Common;

/// <summary>
/// Picking a first and a last day on a month grid that opens in place, shown by
/// <see cref="DateRangeCalendar"/>. The system date picker is a dialog, which the app does
/// not use. The first tap picks the first day and the second the last; a tap before the
/// first day starts again from there, and a tap after both are picked starts a new choice.
/// </summary>
/// <param name="maxDays">The longest range allowed; null for no limit.</param>
public partial class DateRangePicker(int? maxDays = null) : ObservableObject
{
    /// <summary>The month the grid shows: its 1st.</summary>
    private DateOnly _shownMonth;

    private DateOnly? _start;
    private DateOnly? _end;

    /// <summary>"November 2026", over the grid.</summary>
    [ObservableProperty]
    public partial string CalendarTitle { get; set; } = "";

    [ObservableProperty]
    public partial bool CanShowLaterMonth { get; set; }

    /// <summary>What has been picked so far, or what to tap next.</summary>
    [ObservableProperty]
    public partial string Selection { get; set; } = "";

    /// <summary>A complete, allowed range is picked.</summary>
    [ObservableProperty]
    public partial bool HasRange { get; set; }

    public ObservableCollection<CalendarDay> Days { get; } = [];

    /// <summary>The range picked, once <see cref="HasRange"/>.</summary>
    public DatePeriod? Range => HasRange && _start is { } start && _end is { } end ? new DatePeriod(start, end) : null;

    // UTC days, as the backend counts them, so "today" is never ahead of it.
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Starts from a range already chosen, or from nothing on the current month.</summary>
    public void Begin(DatePeriod? range)
    {
        _start = range?.From;
        _end = range?.To;
        var shown = range?.From ?? Today;
        ShowMonth(new DateOnly(shown.Year, shown.Month, 1));
    }

    [RelayCommand]
    private void EarlierMonth() => ShowMonth(_shownMonth.AddMonths(-1));

    [RelayCommand]
    private void LaterMonth()
    {
        if (CanShowLaterMonth)
        {
            ShowMonth(_shownMonth.AddMonths(1));
        }
    }

    [RelayCommand]
    private void Pick(CalendarDay day)
    {
        if (day.Date is not { } date)
        {
            return;
        }

        if (_start is null || _end is not null || date < _start)
        {
            _start = date;
            _end = null;
        }
        else
        {
            _end = date;
        }

        Mark();
    }

    private void ShowMonth(DateOnly first)
    {
        _shownMonth = first;
        CalendarTitle = first.ToString("MMMM yyyy");

        // The month after today's is as far as it goes: a range may end in the future, never start there.
        var thisMonth = new DateOnly(Today.Year, Today.Month, 1);
        CanShowLaterMonth = first <= thisMonth;

        // Weeks start on Monday.
        var blanks = ((int)first.DayOfWeek + 6) % 7;
        var length = DateTime.DaysInMonth(first.Year, first.Month);
        Days.Clear();
        for (var cell = 0; cell < 42; cell++)
        {
            var day = cell - blanks;
            DateOnly? date = day >= 0 && day < length ? first.AddDays(day) : null;
            Days.Add(new CalendarDay(date, cell / 7, cell % 7, date == Today));
        }

        Mark();
    }

    private void Mark()
    {
        foreach (var day in Days)
        {
            day.IsEdge = day.Date is { } date && (date == _start || date == _end);
            day.IsBetween = day.Date is { } d && _start is { } s && _end is { } e && d > s && d < e;
        }

        HasRange = false;
        if (_start is not { } start)
        {
            Selection = "Tap the first day.";
        }
        else if (start > Today)
        {
            Selection = "The first day cannot be after today. Tap an earlier one.";
        }
        else if (_end is not { } end)
        {
            Selection = $"From {start:d MMM yyyy}. Now tap the last day.";
        }
        else if (maxDays is { } max && end.DayNumber - start.DayNumber + 1 > max)
        {
            Selection = "That is too long: at most a year.";
        }
        else
        {
            var range = new DatePeriod(start, end);
            Selection = range.Days == 1
                ? PeriodText.Describe(range)
                : $"{PeriodText.Describe(range)}, {range.Days} days";
            HasRange = true;
        }
    }
}
