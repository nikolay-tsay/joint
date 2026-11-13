namespace Joint.Api.Domain;

/// <summary>What a goal is for, which decides the section it is listed in.</summary>
public enum GoalKind
{
    /// <summary>Money put aside: a holiday, a car, a reserve.</summary>
    Saving,

    /// <summary>A portfolio to build up to a certain value.</summary>
    Investment,
}
