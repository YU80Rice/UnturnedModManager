namespace UnturnedModManager.Models;

public enum ModPackageEntryConflictType
{
    New,
    ConflictDll,
    ConflictConfig
}

public sealed record ModPackagePlanEntry(
    string RelativePath,
    string EntryType,
    long Size,
    ModPackageEntryConflictType ConflictType,
    string Description);

public sealed class ModPackageImportPlan
{
    public UmmpkManifest Manifest { get; set; } = new();
    public string PackagePath { get; set; } = string.Empty;
    public List<ModPackagePlanEntry> Entries { get; } = [];

    public int ConflictingDllCount => Entries.Count(e => e.ConflictType == ModPackageEntryConflictType.ConflictDll);
    public int ConflictingConfigCount => Entries.Count(e => e.ConflictType == ModPackageEntryConflictType.ConflictConfig);
    public int NewCount => Entries.Count(e => e.ConflictType == ModPackageEntryConflictType.New);
}

public sealed class ModPackageImportOptions
{
    public string? TargetProfileId { get; set; }
    public string? NewProfileName { get; set; }
    public bool CreateNewProfile { get; set; }
    public bool PreserveLocalConfig { get; set; } = true;
    public bool BackupConflictingDlls { get; set; } = true;
}
