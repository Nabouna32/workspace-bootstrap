using Microsoft.Win32;
using System.Management;

namespace WorkspaceBootstrap;

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

public sealed class WindowsSystemService
{
    public BaselineSnapshot GetBaseline()
    {
        using var os = new ManagementObjectSearcher("SELECT Caption, BuildNumber, OSArchitecture, LastBootUpTime FROM Win32_OperatingSystem")
            .Get().Cast<ManagementObject>().First();
        using var computer = new ManagementObjectSearcher("SELECT TotalPhysicalMemory, NumberOfLogicalProcessors FROM Win32_ComputerSystem")
            .Get().Cast<ManagementObject>().First();
        using var cpu = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor")
            .Get().Cast<ManagementObject>().First();

        var boot = ManagementDateTimeConverter.ToDateTime((string)os["LastBootUpTime"]);
        var volumes = DriveInfo.GetDrives()
            .Where(x => x.IsReady)
            .Select(x => new VolumeSnapshot(
                x.Name.TrimEnd('\\'),
                x.TotalSize,
                x.AvailableFreeSpace,
                "Ready"))
            .ToArray();

        return new BaselineSnapshot(
            (string?)os["Caption"] ?? "Windows",
            (string?)os["BuildNumber"] ?? "",
            (string?)os["OSArchitecture"] ?? "",
            (string?)cpu["Name"] ?? "CPU",
            Convert.ToInt32(computer["NumberOfLogicalProcessors"]),
            Math.Round(Convert.ToDouble(computer["TotalPhysicalMemory"]) / 1073741824d, 1),
            Math.Round((DateTime.Now - boot).TotalHours, 1),
            volumes,
            GetStartupEntries());
    }

    private static IReadOnlyList<StartupSnapshot> GetStartupEntries()
    {
        var names = new List<string>();
        foreach (var (root, subKey) in new[]
        {
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run")
        })
        {
            using var key = root.OpenSubKey(subKey);
            if (key is null) continue;
            names.AddRange(key.GetValueNames());
        }

        return names
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Select(x => new StartupSnapshot(x))
            .ToArray();
    }
}
