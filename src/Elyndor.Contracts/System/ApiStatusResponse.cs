namespace Elyndor.Contracts.System;

public sealed record ApiStatusResponse(
    string Service,
    string Status,
    DateTimeOffset UtcNow);
