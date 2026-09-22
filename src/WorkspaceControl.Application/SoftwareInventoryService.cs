using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Application;

public sealed class SoftwareInventoryService : ISoftwareInventoryService
{
    private readonly IReadOnlyList<ISoftwareInventorySource> _sources;

    public SoftwareInventoryService(IEnumerable<ISoftwareInventorySource> sources)
    {
        _sources = sources?.ToArray() ?? throw new ArgumentNullException(nameof(sources));
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
                var result = await source.ScanAsync(cancellationToken);
                items.AddRange(result.Items);
                diagnostics.AddRange(result.Diagnostics);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                diagnostics.Add($"{source.Id}: {ex.Message}");
            }
        }

        var merged = items
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(x => x.Ownership != SoftwareOwnership.Unknown)
                .ThenByDescending(x => x.Version, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SoftwareInventorySnapshot(
            Guid.NewGuid().ToString("N"),
            started,
            DateTimeOffset.UtcNow,
            merged,
            diagnostics);
    }
}
