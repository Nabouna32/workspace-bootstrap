using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BounaDevEnvironment;

public sealed class DesktopEngineClient
{
    private readonly string _repositoryRoot;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DesktopEngineClient(string repositoryRoot)
    {
        _repositoryRoot = repositoryRoot;
    }

    public async Task<DesktopCommandResponse<T>> ExecuteAsync<T>(
        string command,
        string? profileId = null,
        string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        var (fileName, arguments, workingDirectory) = ResolveEngine(command, profileId, operationId);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (string.IsNullOrWhiteSpace(stdout))
        {
            var detail = string.IsNullOrWhiteSpace(stderr)
                ? "Le moteur natif n'a retourné aucune réponse."
                : stderr.Trim();
            throw new InvalidOperationException(detail);
        }

        DesktopCommandResponse<T> response;
        try
        {
            response = JsonSerializer.Deserialize<DesktopCommandResponse<T>>(stdout, JsonOptions)
                ?? throw new InvalidOperationException("Réponse JSON vide.");
        }
        catch (JsonException ex)
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? string.Empty : $" STDERR: {stderr.Trim()}";
            throw new InvalidOperationException($"Réponse du moteur natif invalide : {ex.Message}.{detail}", ex);
        }

        if (response.SchemaVersion != 1)
            throw new InvalidOperationException($"Version de contrat desktop non prise en charge : {response.SchemaVersion}.");

        if (response.ExitCode != process.ExitCode)
            throw new InvalidOperationException($"Incohérence du contrat : exitCode={response.ExitCode}, processus={process.ExitCode}.");

        return response;
    }

    public Task<DesktopCommandResponse<string[]>> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync<string[]>("capabilities", cancellationToken: cancellationToken);

    public Task<DesktopCommandResponse<ProvisioningOperationStartData>> StartProvisioningAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteAsync<ProvisioningOperationStartData>("provisioning-start", profileId, cancellationToken: cancellationToken);

    public Task<DesktopCommandResponse<ProvisioningOperationState>> GetProvisioningStatusAsync(string operationId, CancellationToken cancellationToken = default) =>
        ExecuteAsync<ProvisioningOperationState>("provisioning-status", null, operationId, cancellationToken);

    public Task<DesktopCommandResponse<ProvisioningOperationStartData>> ResumeProvisioningAsync(string operationId, CancellationToken cancellationToken = default) =>
        ExecuteAsync<ProvisioningOperationStartData>("provisioning-resume", null, operationId, cancellationToken);

    public Task<DesktopCommandResponse<ProvisioningOperationState>> GetProvisioningRecoveryAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync<ProvisioningOperationState>("provisioning-recovery", cancellationToken: cancellationToken);

    public Task<DesktopCommandResponse<ProvisioningOperationHistoryItem[]>> GetProvisioningHistoryAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync<ProvisioningOperationHistoryItem[]>("provisioning-history", cancellationToken: cancellationToken);

    public Task<DesktopCommandResponse<ProvisioningOperationDetail>> GetProvisioningDetailAsync(string operationId, CancellationToken cancellationToken = default) =>
        ExecuteAsync<ProvisioningOperationDetail>("provisioning-detail", null, operationId, cancellationToken);

    private (string FileName, string Arguments, string WorkingDirectory) ResolveEngine(string command, string? profileId, string? operationId)
    {
        var installed = Environment.GetEnvironmentVariable("WORKSPACE_BOOTSTRAP_ENGINE");
        var candidates = new[]
        {
            installed,
            Path.Combine(@"C:\Dev\WorkspaceBootstrap\app", "WorkspaceBootstrap.exe"),
            Path.Combine(AppContext.BaseDirectory, "WorkspaceBootstrap.exe")
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>();

        var executable = candidates.FirstOrDefault(File.Exists);
        if (executable is not null)
        {
            var args = command;
            if (!string.IsNullOrWhiteSpace(profileId))
                args += $" --profile \"{profileId}\"";
            if (!string.IsNullOrWhiteSpace(operationId))
                args += $" --operation \"{operationId}\"";
            return (executable, args, Path.GetDirectoryName(executable)!);
        }

        // Development-only fallback: the source tree may be used before publish.
        var project = Path.Combine(_repositoryRoot, "src", "WorkspaceBootstrap.Cli", "WorkspaceBootstrap.Cli.csproj");
        if (!File.Exists(project))
            throw new FileNotFoundException("WorkspaceBootstrap.exe introuvable et le projet CLI de développement est absent.", project);

        var runArgs = $"run --project \"{project}\" -- {command}";
        if (!string.IsNullOrWhiteSpace(profileId))
            runArgs += $" --profile \"{profileId}\"";
        if (!string.IsNullOrWhiteSpace(operationId))
            runArgs += $" --operation \"{operationId}\"";
        return ("dotnet", runArgs, _repositoryRoot);
    }
}
