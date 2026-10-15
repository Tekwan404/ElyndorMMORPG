namespace Elyndor.Infrastructure.Combat;

// A gate timeout occurs before this request can mutate the session. An I/O
// timeout after acquiring the gate has an ambiguous outcome and must not use this code.
public sealed class CombatGateBusyException : TimeoutException
{
    public CombatGateBusyException() : base("Combat is busy processing another operation.") { }
    public CombatGateBusyException(string message) : base(message) { }
    public CombatGateBusyException(string message, Exception innerException) : base(message, innerException) { }
}
