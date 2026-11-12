namespace Joint.Api.Domain;

public enum ProviderKind
{
    Fio,
    Trading212,
    AirBank,
}

public enum ConnectionStatus
{
    Pending,
    Active,
    Failing,
    ConsentExpired,
}

/// <summary>What other household members may see of an account.</summary>
public enum AccountVisibility
{
    Hidden,
    BalanceOnly,
    Full,
}

public enum AccountKind
{
    Current,
    Savings,
    Investment,
}
