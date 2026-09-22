namespace BounaDevEnvironment;

public sealed class DesktopCapabilitySnapshotProvider : ICapabilitySnapshotProvider
{
    private readonly DesktopEngineClient _client;

    public DesktopCapabilitySnapshotProvider(DesktopEngineClient client)
    {
        _client = client;
    }

    public async Task<CapabilitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client.GetCapabilitiesAsync(cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error ?? "Capability detection failed.");
        }

        var capabilities = new HashSet<string>(
            response.Data ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        return new CapabilitySnapshot(
            capabilities,
            response.TimestampUtc,
            "windows-engine");
    }
}
