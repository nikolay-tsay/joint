namespace Joint.Api.Domain;

/// <summary>What a goal is for, which decides the accounts that count towards it.</summary>
public enum GoalKind
{
    /// <summary>Money put aside; every account of the Savings type counts.</summary>
    Saving,

    /// <summary>A portfolio to build up; every account of the Investment type counts.</summary>
    Investment,
}
