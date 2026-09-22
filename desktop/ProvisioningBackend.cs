using WorkspaceBootstrap.Desktop;

namespace WorkspaceBootstrap.Desktop.Execution;

public sealed record ProvisioningExecutionStart(
    string OperationId,
    string Status);

public interface IProvisioningBackend
{
    Task<ProvisioningExecutionStart> StartAsync(
        string profileId,
        CancellationToken cancellationToken = default);

    Task<ProvisioningOperationState> GetStatusAsync(
        string operationId,
        CancellationToken cancellationToken = default);

    Task<ProvisioningExecutionStart> ResumeAsync(
        string operationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Native provisioning backend backed by the Workspace Bootstrap CLI.
/// The application layer depends on IProvisioningBackend rather than directly
/// coupling provisioning jobs to a second execution implementation.
/// </summary>
public sealed class NativeProvisioningBackend : IProvisioningBackend
{
    private readonly DesktopEngineClient _client;

    public NativeProvisioningBackend(DesktopEngineClient client)
    {
        _client = client;
    }

    public async Task<ProvisioningExecutionStart> StartAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.StartProvisioningAsync(profileId, cancellationToken);
        EnsureSuccess(response, "provisioning-start");

        var data = response.Data
            ?? throw new InvalidOperationException("Le moteur n'a pas retourné d'opération de provisioning.");

        return new ProvisioningExecutionStart(data.OperationId, data.Status);
    }

    public async Task<ProvisioningOperationState> GetStatusAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.GetProvisioningStatusAsync(operationId, cancellationToken);
        EnsureSuccess(response, "provisioning-status");

        return response.Data
            ?? throw new InvalidOperationException("Le moteur n'a pas retourné l'état de l'opération.");
    }

    public async Task<ProvisioningExecutionStart> ResumeAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.ResumeProvisioningAsync(operationId, cancellationToken);
        EnsureSuccess(response, "provisioning-resume");

        var data = response.Data
            ?? throw new InvalidOperationException("Le moteur n'a pas retourné l'opération reprise.");

        return new ProvisioningExecutionStart(data.OperationId, data.Status);
    }

    private static void EnsureSuccess<T>(
        DesktopCommandResponse<T> response,
        string command)
    {
        if (response.Success)
        {
            return;
        }

        var detail = string.IsNullOrWhiteSpace(response.Error)
            ? $"La commande {command} a échoué."
            : response.Error;

        throw new InvalidOperationException(detail);
    }
}
