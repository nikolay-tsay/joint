using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Insights;

/// <summary>
/// The dates Overview and Categories report on. The bar above them steps to the period
/// before or after; tapping its title opens a month grid in place (<see cref="DateRangePicker"/>)
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

    /// <param name="changed">Reloads what depends on the period; called after every change.</param>
    public PeriodFilter(Func<Task> changed)
    {
        _changed = changed;
        Current = Remembered() ?? DatePeriod.MonthOf(Today);
        Describe();
    }

    public DatePeriod Current { get; private set; }

    /// <summary>The month grid, shown while <see cref="IsOpen"/>.</summary>
    public DateRangePicker Picker { get; } = new(DatePeriod.MaxDays);

    /// <summary>"November 2026", or "15 Oct – 14 Nov 2026".</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "";

    /// <summary>False once the next period would start after today.</summary>
    [ObservableProperty]
    public partial bool CanGoForward { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    // Insights works in UTC days, as the backend does, so "today" is never ahead of it.
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [RelayCommand]
    private Task PreviousAsync() => ChangeAsync(Current.Shift(-1));

    [RelayCommand]
    private Task NextAsync() => Current.Shift(1).From <= Today ? ChangeAsync(Current.Shift(1)) : Task.CompletedTask;

    [RelayCommand]
    private void Toggle()
    {
        if (!IsOpen)
        {
            Picker.Begin(Current);
        }

        IsOpen = !IsOpen;
    }

    [RelayCommand]
    private Task ThisMonthAsync() => ChangeAsync(DatePeriod.MonthOf(Today));

    [RelayCommand]
    private Task LastMonthAsync() => ChangeAsync(DatePeriod.MonthOf(Today).Shift(-1));

    [RelayCommand]
    private Task ApplyAsync() => Picker.Range is { } range ? ChangeAsync(range) : Task.CompletedTask;

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
