using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace WorkspaceBootstrap.Desktop;

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
        var launch = ResolveEngine(command, profileId, operationId);

        var psi = new ProcessStartInfo
        {
            FileName = launch.FileName,
            WorkingDirectory = launch.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in launch.Arguments)
            psi.ArgumentList.Add(argument);

        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException($"Impossible de démarrer le moteur natif : {launch.FileName}");

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return ParseResponse(stdout, stderr, process.ExitCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between HasExited and Kill.
            }

            throw;
        }
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

    private (string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory) ResolveEngine(
        string command,
        string? profileId,
        string? operationId)
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "WorkspaceBootstrap.Cli.exe");
        if (File.Exists(executable))
        {
            var arguments = new List<string> { command };
            AddOptionalArgument(arguments, "--profile", profileId);
            AddOptionalArgument(arguments, "--operation", operationId);
            return (executable, arguments, Path.GetDirectoryName(executable)!);
        }

        var project = Path.Combine(_repositoryRoot, "src", "WorkspaceBootstrap.Cli", "WorkspaceBootstrap.Cli.csproj");
        if (!File.Exists(project))
            throw new FileNotFoundException(
                "WorkspaceBootstrap.Cli.exe introuvable et le projet CLI de développement est absent.",
                project);

        var runArguments = new List<string>
        {
            "run",
            "--project",
            project,
            "--",
            command
        };
        AddOptionalArgument(runArguments, "--profile", profileId);
        AddOptionalArgument(runArguments, "--operation", operationId);
        return ("dotnet", runArguments, _repositoryRoot);
    }

    private static void AddOptionalArgument(
        ICollection<string> arguments,
        string name,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        arguments.Add(name);
        arguments.Add(value);
    }

    private static DesktopCommandResponse<T> ParseResponse<T>(
        string stdout,
        string stderr,
        int processExitCode)
    {
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
            throw new InvalidOperationException(
                $"Réponse du moteur natif invalide : {ex.Message}.{detail}",
                ex);
        }

        if (response.SchemaVersion != 1)
            throw new InvalidOperationException(
                $"Version de contrat desktop non prise en charge : {response.SchemaVersion}.");

        if (response.ExitCode != processExitCode)
            throw new InvalidOperationException(
                $"Incohérence du contrat : exitCode={response.ExitCode}, processus={processExitCode}.");

        return response;
    }
}
}