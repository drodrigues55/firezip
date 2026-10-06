namespace Firezip.Core.Security;

/// <summary>
/// Exception thrown when an archive entry violates security policies (Zip Slip, traversal, bomb, etc.).
/// </summary>
public class SecurityValidationException : Exception
{
    public string EntryPath { get; }
    public string SecurityReason { get; }

    public SecurityValidationException(string message, string entryPath, string securityReason)
        : base(message)
    {
        EntryPath = entryPath;
        SecurityReason = securityReason;
    }
}
