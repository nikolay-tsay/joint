namespace Joint.Contracts;

public record AccountResponse(
    Guid Id,
    string Name,
    string? Institution,
    string Kind,
    string Currency,
    decimal Balance,
    string OwnerDisplayName);
