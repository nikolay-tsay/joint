namespace Joint.Api.Domain;

/// <summary>What a marked payee is. The first two are costs that come back on a schedule and are tracked apart.</summary>
public enum RecurringKind
{
    /// <summary>Something that can be cancelled at will: streaming, apps, memberships.</summary>
    Subscription,

    /// <summary>A regular cost of living: rent, internet, energy, insurance.</summary>
    Bill,

    /// <summary>
    /// Money moved to or from an account of the household's own that the app cannot pair
    /// up, such as a broker whose movements are not imported. Neither spending nor income.
    /// </summary>
    Transfer,
}
