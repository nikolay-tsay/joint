using Joint.Contracts;

namespace Joint.Mobile.Features.Insights;

/// <summary>How a period reads on screen: a calendar month by name, anything else by its dates.</summary>
public static class PeriodText
{
    /// <summary>"November 2026", or "15 Oct – 14 Nov 2026".</summary>
    public static string Describe(DatePeriod period)
    {
        if (period.IsCalendarMonth)
        {
            return period.From.ToString("MMMM yyyy");
        }

        if (period.From == period.To)
        {
            return period.From.ToString("d MMM yyyy");
        }

        return period.From.Year == period.To.Year
            ? $"{period.From:d MMM} – {period.To:d MMM yyyy}"
            : $"{period.From:d MMM yyyy} – {period.To:d MMM yyyy}";
    }

    /// <summary>"in November", or "15 Oct – 14 Nov 2026", to follow a word such as "Saved".</summary>
    public static string After(DatePeriod period) =>
        period.IsCalendarMonth ? $"in {period.From:MMMM}" : Describe(period);

    /// <summary>Under a bar: "Nov" for calendar months, otherwise the first day, "15 Oct".</summary>
    public static string Short(DatePeriod period) =>
        period.IsCalendarMonth ? period.From.ToString("MMM") : period.From.ToString("d MMM");

    /// <summary>"month" or "period", for sentences that compare one with the one before.</summary>
    public static string Noun(DatePeriod period) => period.IsCalendarMonth ? "month" : "period";
}
