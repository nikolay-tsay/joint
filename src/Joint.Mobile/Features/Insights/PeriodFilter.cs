using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;

namespace Joint.Mobile.Features.Insights;

/// <summary>
/// The dates Overview and Categories report on. The bar above them steps to the period
/// before or after; tapping its title opens a month grid in place (no system date picker)
/// where the first and the last day are tapped in turn.
/// </summary>
/// <remarks>
/// A monthly period, such as payday to payday, is remembered on the device and moved on to
/// the one holding today when the app starts, so a pay period chosen once keeps opening on
/// the current one. Any other period lasts until the app closes and does not replace the
/// remembered one.
/// </remarks>
public partial class PeriodFilter : ObservableObject
{
    private const string FromKey = "insights.period.from";
    private const string ToKey = "insights.period.to";

    private readonly Func<Task> _changed;

    /// <summary>The month the grid shows: its 1st.</summary>
    private DateOnly _shownMonth;

    private DateOnly? _start;
    private DateOnly? _end;

    /// <param name="changed">Reloads what depends on the period; called after every change.</param>
    public PeriodFilter(Func<Task> changed)
    {
        _changed = changed;
        Current = Remembered() ?? DatePeriod.MonthOf(Today);
        Describe();
    }

    public DatePeriod Current { get; private set; }

    /// <summary>"November 2026", or "15 Oct – 14 Nov 2026".</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "";

    /// <summary>False once the next period would start after today.</summary>
    [ObservableProperty]
    public partial bool CanGoForward { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>"November 2026", over the grid.</summary>
    [ObservableProperty]
    public partial string CalendarTitle { get; set; } = "";

    [ObservableProperty]
    public partial bool CanShowLaterMonth { get; set; }

    /// <summary>What has been picked so far, or what to tap next.</summary>
    [ObservableProperty]
    public partial string Selection { get; set; } = "";

    [ObservableProperty]
    public partial bool CanApply { get; set; }

    public ObservableCollection<CalendarDay> Days { get; } = [];

    // Insights works in UTC days, as the backend does, so "today" is never ahead of it.
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [RelayCommand]
    private Task PreviousAsync() => ChangeAsync(Current.Shift(-1));

    [RelayCommand]
    private Task NextAsync() => Current.Shift(1).From <= Today ? ChangeAsync(Current.Shift(1)) : Task.CompletedTask;

    [RelayCommand]
    private void Toggle()
    {
        if (IsOpen)
        {
            IsOpen = false;
            return;
        }

        _start = Current.From;
        _end = Current.To;
        ShowMonth(new DateOnly(Current.From.Year, Current.From.Month, 1));
        IsOpen = true;
    }

    [RelayCommand]
    private Task ThisMonthAsync() => ChangeAsync(DatePeriod.MonthOf(Today));

    [RelayCommand]
    private Task LastMonthAsync() => ChangeAsync(DatePeriod.MonthOf(Today).Shift(-1));

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

    /// <summary>
    /// The first tap picks the first day, the second the last. A second tap before the first
    /// day starts again from there; a tap after both are picked starts a new choice.
    /// </summary>
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

    [RelayCommand]
    private Task ApplyAsync() =>
        _start is { } start && _end is { } end && CanApply ? ChangeAsync(new DatePeriod(start, end)) : Task.CompletedTask;

    private async Task ChangeAsync(DatePeriod period)
    {
        IsOpen = false;
        Current = period;
        Remember(period);
        Describe();
        await _changed();
    }

    private void Describe()
    {
        Title = PeriodText.Describe(Current);
        CanGoForward = Current.Shift(1).From <= Today;
    }

    private void ShowMonth(DateOnly first)
    {
        _shownMonth = first;
        CalendarTitle = first.ToString("MMMM yyyy");

        // The month after today's is as far as it goes: a period may end in the future, never start there.
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

        CanApply = false;
        if (_start is not { } start)
        {
            Selection = "Tap the first day.";
        }
        else if (start > Today)
        {
            Selection = "A period cannot start after today. Tap an earlier first day.";
        }
        else if (_end is not { } end)
        {
            Selection = $"From {start:d MMM yyyy}. Now tap the last day.";
        }
        else if (end.DayNumber - start.DayNumber + 1 > DatePeriod.MaxDays)
        {
            Selection = "A period can be at most a year long.";
        }
        else
        {
            var period = new DatePeriod(start, end);
            Selection = period.Days == 1
                ? PeriodText.Describe(period)
                : $"{PeriodText.Describe(period)}, {period.Days} days";
            CanApply = true;
        }
    }

    private static DatePeriod? Remembered()
    {
        try
        {
            if (DateOnly.TryParseExact(Preferences.Default.Get(FromKey, ""), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
                && DateOnly.TryParseExact(Preferences.Default.Get(ToKey, ""), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
                && from <= to)
            {
                var period = new DatePeriod(from, to).RolledTo(Today);
                return period.From <= Today ? period : null;
            }
        }
        catch (Exception)
        {
            // A preference that cannot be read only means starting on the current month.
        }

        return null;
    }

    /// <summary>
    /// Monthly periods are kept for next time. Any other choice is for now only and leaves
    /// the kept one alone, so looking at a few odd days does not lose the pay period.
    /// </summary>
    private static void Remember(DatePeriod period)
    {
        if (period.Months is null)
        {
            return;
        }

        Preferences.Default.Set(FromKey, period.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Preferences.Default.Set(ToKey, period.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
