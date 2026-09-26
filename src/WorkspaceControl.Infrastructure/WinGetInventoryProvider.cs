using System.Diagnostics;
using System.Text.RegularExpressions;
namespace WorkspaceControl.Infrastructure;

public sealed class WinGetInventoryProvider : IInventoryProvider
{
    private static readonly Regex SeparatorRegex = new("-+", RegexOptions.Compiled);
    public string Id => "windows.winget";

    public async Task<InventoryProviderResult> ScanAsync(
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var output = await RunWingetListAsync(cancellationToken);
            var observations = ParseListOutput(output, startedAt);

            return new InventoryProviderResult(
                observations,
                new InventoryProviderDiagnostic(
                    Id,
                    true,
                    $"WinGet inventory scan completed: {observations.Count} package entries."));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new InventoryProviderResult(
                Array.Empty<InventoryObservation>(),
                new InventoryProviderDiagnostic(
                    Id,
                    false,
                    "WinGet inventory scan failed.",
                    ex.Message));
        }
    }

    internal static IReadOnlyList<InventoryObservation> ParseListOutput(
        string output,
        DateTimeOffset detectedAtUtc)
    {
        var lines = output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .ToArray();

        var separatorIndex = Array.FindIndex(
            lines,
            line => SeparatorRegex.Matches(line).Count >= 2 &&
                    line.Trim().Replace("-", string.Empty).Replace(" ", string.Empty).Length == 0);

        if (separatorIndex < 0)
        {
            return Array.Empty<InventoryObservation>();
        }

        var separator = lines[separatorIndex];
        var starts = SeparatorRegex.Matches(separator)
            .Select(match => match.Index)
            .ToArray();

        if (starts.Length < 3)
        {
            return Array.Empty<InventoryObservation>();
        }

        var observations = new List<InventoryObservation>();

        for (var i = separatorIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            var columns = SliceColumns(line, starts);
            if (columns.Count < 3)
            {
                continue;
            }

            var name = columns[0].Trim();
            var id = columns[1].Trim();
            var version = NormalizeUnknown(columns[2]);

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(id) ||
                IsHeaderOrFooter(name, id))
            {
                continue;
            }

            var source = columns.Count >= 5
                ? NormalizeUnknown(columns[4])
                : columns.Count == 4 ? NormalizeUnknown(columns[3]) : null;
            var available = columns.Count >= 5 ? NormalizeUnknown(columns[3]) : null;

            var providerId = string.IsNullOrWhiteSpace(source)
                ? id
                : $"{source}:{id}";

            var instanceIdentity = $"{providerId}|{version}";
            observations.Add(new InventoryObservation(
                $"windows.winget.instance.{Normalize(instanceIdentity)}",
                name,
                version,
                InferFamilyId(name, id),
                "windows.winget",
                providerId,
                source ?? "winget",
                InventoryScope.Unknown,
                null,
                BuildRemovalCommand(id, version, source),
                null,
                InventoryOwnership.Unknown,
                Array.Empty<string>(),
                string.IsNullOrWhiteSpace(available)
                    ? Array.Empty<InventoryEvidence>()
                    : [new InventoryEvidence(
                        "available-update",
                        $"WinGet reports an available version: {available}.",
                        false,
                        "winget")],
                detectedAtUtc,
                available,
                BuildRemovalCapability(id, source)));
        }

        return observations;
    }

    private static async Task<string> RunWingetListAsync(
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "winget.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("list");
        process.StartInfo.ArgumentList.Add("--include-unknown");
        process.StartInfo.ArgumentList.Add("--disable-interactivity");
        process.StartInfo.ArgumentList.Add("--accept-source-agreements");

        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start winget.exe.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"winget list exited with code {process.ExitCode}: {stderr.Trim()}");
        }

        return stdout;
    }

    private static IReadOnlyList<string> SliceColumns(
        string line,
        IReadOnlyList<int> starts)
    {
        var columns = new List<string>(starts.Count);

        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            if (start >= line.Length)
            {
                columns.Add(string.Empty);
                continue;
            }

            var end = i + 1 < starts.Count ? Math.Min(starts[i + 1], line.Length) : line.Length;
            columns.Add(line[start..end]);
        }

        return columns;
    }

    private static string? NormalizeUnknown(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized) ||
               normalized.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }

    private static bool IsHeaderOrFooter(string name, string id) =>
        name.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
        id.Equals("Id", StringComparison.OrdinalIgnoreCase);

    private static RemovalCapability? BuildRemovalCapability(
        string packageId,
        string? source)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return null;
        }

        return new RemovalCapability(
            RemovalCapabilityKindCodes.WinGet,
            "windows.winget",
            packageId,
            source,
            InventoryScope.Unknown);
    }

    private static string? InferFamilyId(string displayName, string packageId)
    {
        var value = $"{displayName} {packageId}".ToLowerInvariant();

        if (value.Contains("java") || value.Contains("jdk"))
        {
            return "java";
        }

        if (value.Contains(".net") || value.Contains("dotnet"))
        {
            return "dotnet";
        }

        if (value.Contains("node.js") || value.Contains("nodejs"))
        {
            return "nodejs";
        }

        if (value.Contains("python"))
        {
            return "python";
        }

        if (value.Contains("flutter"))
        {
            return "flutter";
        }

        return null;
    }

    private static string Normalize(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();

        return new string(chars).Trim('-');
    }
}
