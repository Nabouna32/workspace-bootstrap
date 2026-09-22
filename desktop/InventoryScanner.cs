namespace BounaDevEnvironment;

public sealed class InventoryScanner
{
    private readonly IReadOnlyList<IInventoryProvider> _providers;

    public InventoryScanner(IEnumerable<IInventoryProvider> providers)
    {
        _providers = providers.ToArray();
    }

    public async Task<InventorySnapshot> ScanAsync(
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var observations = new List<InventoryObservation>();
        var diagnostics = new List<InventoryProviderDiagnostic>();

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await provider.ScanAsync(cancellationToken);
                observations.AddRange(result.Observations);
                diagnostics.Add(result.Diagnostic);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                diagnostics.Add(new InventoryProviderDiagnostic(
                    provider.Id,
                    false,
                    "Inventory provider failed.",
                    ex.Message));
            }
        }

        var items = Deduplicate(observations);
        MarkOlderVersionSignals(items);

        return new InventorySnapshot(
            Guid.NewGuid().ToString("N"),
            started,
            DateTimeOffset.UtcNow,
            items,
            diagnostics);
    }

    private static IReadOnlyList<InventoryItem> Deduplicate(
        IReadOnlyList<InventoryObservation> observations)
    {
        return observations
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderByDescending(item => item.DetectedAtUtc).ToArray();
                var first = ordered[0];

                var evidence = ordered
                    .SelectMany(item => item.Evidence)
                    .GroupBy(item => $"{item.Kind}:{item.Description}", StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.First())
                    .ToArray();

                return new InventoryItem
                {
                    Id = group.Key,
                    DisplayName = first.DisplayName,
                    Version = first.Version,
                    FamilyId = first.FamilyId,
                    Scope = first.Scope,
                    InstallLocation = first.InstallLocation,
                    RemovalCommand = first.RemovalCommand,
                    Publisher = first.Publisher,
                    Ownership = ResolveOwnership(ordered),
                    Capabilities = ordered
                        .SelectMany(item => item.Capabilities)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    Observations = ordered,
                    Evidence = evidence,
                    Signals = new HashSet<InventorySignal> { InventorySignal.Installed },
                    DetectedAtUtc = first.DetectedAtUtc
                };
            })
            .ToArray();
    }

    private static void MarkOlderVersionSignals(IReadOnlyList<InventoryItem> items)
    {
        foreach (var family in items
            .Where(item => !string.IsNullOrWhiteSpace(item.FamilyId))
            .GroupBy(item => item.FamilyId!, StringComparer.OrdinalIgnoreCase))
        {
            var versioned = family
                .Where(item => InventoryVersion.TryParse(item.Version, out _))
                .OrderByDescending(item => InventoryVersion.Parse(item.Version!))
                .ToArray();

            foreach (var item in versioned.Skip(1))
            {
                item.Signals.Add(InventorySignal.OlderVersion);
            }
        }
    }

    private static InventoryOwnership ResolveOwnership(
        IReadOnlyList<InventoryObservation> observations)
    {
        if (observations.Any(item => item.Ownership == InventoryOwnership.System))
        {
            return InventoryOwnership.System;
        }

        if (observations.Any(item => item.Ownership == InventoryOwnership.ProductManaged))
        {
            return InventoryOwnership.ProductManaged;
        }

        if (observations.Any(item => item.Ownership == InventoryOwnership.PackageManagerManaged))
        {
            return InventoryOwnership.PackageManagerManaged;
        }

        if (observations.Any(item => item.Ownership == InventoryOwnership.Manual))
        {
            return InventoryOwnership.Manual;
        }

        return InventoryOwnership.Unknown;
    }
}

public sealed class InventoryCleanupAnalyzer
{
    public IReadOnlyList<InventoryCleanupRecommendation> Analyze(
        IReadOnlyList<InventoryItem> items,
        IReadOnlySet<string> desiredComponentIds)
    {
        var recommendations = new List<InventoryCleanupRecommendation>();

        foreach (var family in items
            .Where(item => !string.IsNullOrWhiteSpace(item.FamilyId))
            .GroupBy(item => item.FamilyId!, StringComparer.OrdinalIgnoreCase))
        {
            var versioned = family
                .Where(item => InventoryVersion.TryParse(item.Version, out _))
                .OrderByDescending(item => InventoryVersion.Parse(item.Version!))
                .ToArray();

            if (versioned.Length < 2)
            {
                continue;
            }

            var newest = versioned[0];

            foreach (var candidate in versioned.Skip(1))
            {
                if (desiredComponentIds.Contains(candidate.Id))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(candidate.RemovalCommand) ||
                    candidate.Ownership is InventoryOwnership.Unknown or InventoryOwnership.System)
                {
                    continue;
                }

                var strongEvidence = candidate.Evidence
                    .Where(item => item.IsStrong)
                    .Select(item => item.Description)
                    .ToArray();

                if (strongEvidence.Length > 0)
                {
                    continue;
                }

                recommendations.Add(new InventoryCleanupRecommendation(
                    candidate.Id,
                    candidate.DisplayName,
                    candidate.Version,
                    newest.Version,
                    candidate.Publisher,
                    "inventory.older-version-candidate",
                    [$"newer-version:{newest.Version}", $"ownership:{candidate.Ownership}", "authoritative-uninstall-mechanism:provider"],
                    true));
            }
        }

        return recommendations;
    }
}


internal static class InventoryVersion
{
    public static bool TryParse(string? value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var numericParts = new string(value
            .TrimStart('v', 'V')
            .TakeWhile(character =>
                char.IsDigit(character) ||
                character is '.' or '_' or '-')
            .ToArray())
            .Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(4)
            .ToArray();

        if (numericParts.Length < 2 ||
            numericParts.Any(part => !int.TryParse(part, out _)))
        {
            return false;
        }

        var numbers = numericParts.Select(int.Parse).ToArray();
        version = numbers.Length switch
        {
            2 => new Version(numbers[0], numbers[1]),
            3 => new Version(numbers[0], numbers[1], numbers[2]),
            _ => new Version(numbers[0], numbers[1], numbers[2], numbers[3])
        };

        return true;
    }

    public static Version Parse(string value) =>
        TryParse(value, out var version)
            ? version
            : throw new FormatException($"Unsupported inventory version: {value}");
}
