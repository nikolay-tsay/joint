namespace Joint.Api.Domain;

public enum ProviderKind
{
    /// <summary>Lunch Flow, an aggregator: one connection can carry accounts from several banks.</summary>
    LunchFlow,
    Fio,
    Trading212,
}
