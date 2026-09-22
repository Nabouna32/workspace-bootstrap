namespace WorkspaceControl.Domain;

public sealed record BaselineSnapshot(
    string Windows,
    string Build,
    string Architecture,
    string Cpu,
    int CpuCores,
    double MemoryGB,
    double UptimeHours,
    IReadOnlyList<VolumeSnapshot> Volumes,
    IReadOnlyList<StartupSnapshot> StartupEntries);

public sealed record VolumeSnapshot(string DriveLetter, long Size, long SizeRemaining, string HealthStatus);
public sealed record StartupSnapshot(string Name);
