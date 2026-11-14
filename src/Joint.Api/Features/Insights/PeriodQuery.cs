using Joint.Contracts;

namespace Joint.Api.Features.Insights;

/// <summary>
/// Reads the <c>from</c> and <c>to</c> query values that Insights and Categories take: both
/// or neither, the first no later than today, at most <see cref="DatePeriod.MaxDays"/> days.
/// </summary>
public static class PeriodQuery
{
    /// <returns>The period asked for, or null when none was; <paramref name="error"/> says what is wrong otherwise.</returns>
    public static DatePeriod? Read(DateOnly? from, DateOnly? to, DateOnly today, out string? error)
    {
        error = (from, to) switch
        {
            (null, null) => null,
            (null, _) or (_, null) => "Give both from and to, or neither.",
            ({ } f, { } t) when t < f => "The period must not end before it starts.",
            ({ } f, _) when f > today => "The period must not start in the future.",
            ({ } f, { } t) when t.DayNumber - f.DayNumber + 1 > DatePeriod.MaxDays =>
                $"A period can be at most {DatePeriod.MaxDays} days long.",
            _ => null,
        };

        return error is null && from is { } start && to is { } end ? new DatePeriod(start, end) : null;
    }
}
