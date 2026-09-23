using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Application;

public sealed class SoftwareInventoryService : ISoftwareInventoryService
{
    private readonly IReadOnlyList<ISoftwareInventorySource> _sources;

    public SoftwareInventoryService(IEnumerable<ISoftwareInventorySource> sources)
    {
        _sources = sources?.Where(source => source is not null).ToArray()
            ?? throw new ArgumentNullException(nameof(sources));
    }

    public async Task<SoftwareInventorySnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var items = new List<SoftwareItem>();
        var diagnostics = new List<string>();

        foreach (var source in _sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await source.ScanAsync(cancellationToken).ConfigureAwait(false);
                items.AddRange(result.Items);
                diagnostics.AddRange(result.Diagnostics);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                diagnostics.Add($"{source.Id}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        var merged = items
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(Merge)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Version, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SoftwareInventorySnapshot(
            Guid.NewGuid().ToString("N"),
            started,
            DateTimeOffset.UtcNow,
            merged,
            diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static SoftwareItem Merge(IEnumerable<SoftwareItem> candidates)
    {
        var items = candidates.ToArray();
        var representative = items
            .OrderByDescending(item => OwnershipRank(item.Ownership))
            .ThenByDescending(item => ParseVersion(item.Version))
            .ThenByDescending(item => item.Evidence.Any(evidence => evidence.IsStrong))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .First();

        var signals = items
            .SelectMany(item => item.Signals)
            .Distinct()
            .ToArray();

        var evidence = items
            .SelectMany(item => item.Evidence)
            .GroupBy(
                item => $"{item.Source}|{item.Signal}|{item.Detail}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.IsStrong)
                .ThenByDescending(item => item.ObservedAt)
                .First())
            .ToArray();

        return representative with
        {
            Signals = signals,
            Evidence = evidence
        };
    }

    private static int OwnershipRank(SoftwareOwnership ownership) =>
        ownership switch
        {
            SoftwareOwnership.System => 5,
            SoftwareOwnership.ProductManaged => 4,
            SoftwareOwnership.PackageManagerManaged => 3,
            SoftwareOwnership.Manual => 2,
            _ => 1
        };

    private static Version ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new Version(0, 0);

        var normalized = value.Trim().TrimStart('v', 'V');
        var parts = normalized
            .Split('.', '-', '_')
            .Take(4)
            .ToArray();

        if (parts.Length == 0 || parts.Any(part => !int.TryParse(part, out _)))
            return new Version(0, 0);

        var numbers = parts.Select(int.Parse).ToArray();
        return numbers.Length switch
        {
            1 => new Version(numbers[0], 0),
            2 => new Version(numbers[0], numbers[1]),
            3 => new Version(numbers[0], numbers[1], numbers[2]),
            _ => new Version(numbers[0], numbers[1], numbers[2], numbers[3])
        };
    }
}
