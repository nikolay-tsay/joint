namespace Joint.Contracts;

/// <summary>
/// A stretch of days, both ends included, that Insights reports on. Shared by the backend,
/// which cuts the figures by it, and the app, which steps it back and forward, so the two
/// agree on what the period before or after one is.
/// </summary>
/// <remarks>
/// A period that runs from a day to the day before the same date one or more months later
/// (1 Nov to 30 Nov, or payday to payday: 15 Oct to 14 Nov) is monthly and steps by whole
/// months. Any other period steps by its own length in days.
/// </remarks>
public record DatePeriod(DateOnly From, DateOnly To)
{
    /// <summary>The longest period that can be asked for.</summary>
    public const int MaxDays = 366;

    /// <summary>The calendar month a day falls in.</summary>
    public static DatePeriod MonthOf(DateOnly day)
    {
        var first = new DateOnly(day.Year, day.Month, 1);
        return new DatePeriod(first, first.AddMonths(1).AddDays(-1));
    }

    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>How many months the period spans when it is monthly; otherwise null.</summary>
    public int? Months
    {
        get
        {
            for (var months = 1; months <= 12; months++)
            {
                if (From.AddMonths(months) == To.AddDays(1))
                {
                    return months;
                }
            }

            return null;
        }
    }

    /// <summary>Exactly one calendar month: the 1st to its last day.</summary>
    public bool IsCalendarMonth => From.Day == 1 && Months == 1;

    public bool Contains(DateOnly day) => day >= From && day <= To;

    /// <summary>
    /// The period <paramref name="steps"/> periods later (earlier when negative). Monthly
    /// periods keep their day of the month, short months aside.
    /// </summary>
    public DatePeriod Shift(int steps)
    {
        if (Months is { } months)
        {
            var from = From.AddMonths(steps * months);
            return new DatePeriod(from, From.AddMonths((steps + 1) * months).AddDays(-1));
        }

        return new DatePeriod(From.AddDays(steps * Days), To.AddDays(steps * Days));
    }

    /// <summary>
    /// The repeat of this period that holds <paramref name="day"/>, counted from this one so a
    /// monthly period keeps its day of the month (no drift after a short month). This one when
    /// it already holds the day, and when the day is before it.
    /// </summary>
    public DatePeriod RepeatHolding(DateOnly day)
    {
        if (day <= To)
        {
            return this;
        }

        // Jump close for a long way back, then step: a period of a few days repeats often.
        var steps = Months is { } months
            ? Math.Max(((day.Year - From.Year) * 12 + day.Month - From.Month) / months - 1, 0)
            : Math.Max((day.DayNumber - From.DayNumber) / Days - 1, 0);
        while (Shift(steps).To < day)
        {
            steps++;
        }

        return Shift(steps);
    }

    /// <summary>
    /// The same kind of period moved forward until it holds <paramref name="today"/>, so a
    /// payday-to-payday choice made last month opens on the current pay period. Only monthly
    /// periods move; a period that already holds today, or lies after it, is kept.
    /// </summary>
    public DatePeriod RolledTo(DateOnly today)
    {
        if (Months is null || To >= today)
        {
            return this;
        }

        var period = this;
        while (period.To < today)
        {
            period = period.Shift(1);
        }

        return period;
    }
}
