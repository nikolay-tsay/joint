namespace Joint.Api.Domain;

/// <summary>The two sorts of payment that come back on a schedule and are tracked apart.</summary>
public enum RecurringKind
{
    /// <summary>Something that can be cancelled at will: streaming, apps, memberships.</summary>
    Subscription,

    /// <summary>A regular cost of living: rent, internet, energy, insurance.</summary>
    Bill,
}
