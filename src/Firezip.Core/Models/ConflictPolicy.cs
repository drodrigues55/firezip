namespace Firezip.Core.Models;

/// <summary>
/// Policy applied when an extraction destination file already exists.
/// </summary>
public enum ConflictPolicy
{
    AskUser = 0,
    Overwrite = 1,
    Skip = 2,
    Rename = 3,
    Cancel = 4
}

/// <summary>
/// Resolution decision made by user or global policy.
/// </summary>
public record ConflictResolution
{
    public ConflictPolicy Policy { get; init; } = ConflictPolicy.Overwrite;
    public string? CustomNewPath { get; init; }
    public bool ApplyToAllRemaining { get; init; }

    public static ConflictResolution OverwriteCurrent => new() { Policy = ConflictPolicy.Overwrite };
    public static ConflictResolution OverwriteAll => new() { Policy = ConflictPolicy.Overwrite, ApplyToAllRemaining = true };
    public static ConflictResolution SkipCurrent => new() { Policy = ConflictPolicy.Skip };
    public static ConflictResolution SkipAll => new() { Policy = ConflictPolicy.Skip, ApplyToAllRemaining = true };
    public static ConflictResolution RenameCurrent(string newPath) => new() { Policy = ConflictPolicy.Rename, CustomNewPath = newPath };
    public static ConflictResolution RenameAll => new() { Policy = ConflictPolicy.Rename, ApplyToAllRemaining = true };
    public static ConflictResolution CancelOperation => new() { Policy = ConflictPolicy.Cancel };
}
