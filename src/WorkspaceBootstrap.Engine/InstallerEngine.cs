using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkspaceBootstrap;

public sealed class InstallerEngine
{
    private readonly WorkspacePaths _paths;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(20) };

    public InstallerEngine(WorkspacePaths paths) => _paths = paths;

    public async Task InstallAsync(ComponentManifest component, bool cacheOnly, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(component.PackageId))
            throw new InvalidOperationException($"Le composant '{component.Id}' ne possède aucun packageId.");

        var artifact = await ResolveArtifactAsync(component, cacheOnly, token);
        if (artifact is null)
            return;

        var arguments = component.InstallArguments ?? [];
        var psi = new ProcessStartInfo
        {
            FileName = artifact.FilePath,
            WorkingDirectory = Path.GetDirectoryName(artifact.FilePath)!,
            UseShellExecute = true,
            Verb = "runas"
        };

        if (string.Equals(component.InstallerType, "msi", StringComparison.OrdinalIgnoreCase)
            || string.Equals(component.InstallerType, "wix", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = "msiexec.exe";
            psi.Arguments = $"/i \"{artifact.FilePath}\" {string.Join(' ', arguments)}";
        }
        else
        {
            psi.Arguments = string.Join(' ', arguments.Select(EscapeArgument));
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Impossible de lancer l'installeur de '{component.Name}'.");
        await process.WaitForExitAsync(token);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"L'installation de '{component.Name}' a échoué (code {process.ExitCode}).");
    }

    private async Task<InstallerArtifact?> ResolveArtifactAsync(
        ComponentManifest component,
        bool cacheOnly,
        CancellationToken token)
    {
        if (cacheOnly)
        {
            return FindVerifiedCached(component);
        }

        if (component.OfficialSource is null)
            return await InstallViaWingetFallbackAsync(component, token);

        return component.OfficialSource.Type switch
        {
            "github-release" => await ResolveGitHubReleaseAsync(component, component.OfficialSource, token),
            "rarlab-localized" => await ResolveRarLabAsync(component, token),
            "chrome-enterprise" => await ResolveChromeAsync(component, token),
            _ => await InstallViaWingetFallbackAsync(component, token)
        };
    }

    private InstallerArtifact? FindVerifiedCached(ComponentManifest component, string? version = null)
    {
        var metadataDir = Path.Combine(_paths.MetadataRoot, Sanitize(component.Id));
        if (!Directory.Exists(metadataDir))
            return null;

        foreach (var metadataPath in Directory.EnumerateFiles(metadataDir, "*.json").OrderByDescending(x => x))
        {
            try
            {
                var artifact = JsonSerializer.Deserialize<InstallerArtifact>(
                    File.ReadAllText(metadataPath), JsonDefaults.Options);

                if (artifact is null
                    || !File.Exists(artifact.FilePath)
                    || !string.Equals(artifact.Source, "official", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(artifact.ComponentId, component.Id, StringComparison.OrdinalIgnoreCase)
                    || (version is not null && !string.Equals(artifact.Version, version, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (!string.Equals(ComputeSha256(artifact.FilePath), artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                    continue;

                ValidateAuthenticodeIfRequired(artifact.FilePath);
                return artifact;
            }
            catch (CryptographicException)
            {
                // A corrupt or invalidly signed cache entry is not trusted.
            }
            catch (IOException)
            {
                // A transiently unavailable cache entry is skipped.
            }
            catch (JsonException)
            {
                // Invalid metadata is ignored; online resolution can repair it.
            }
        }

        return null;
    }

    private async Task<InstallerArtifact> ResolveGitHubReleaseAsync(
        ComponentManifest component,
        OfficialSource source,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(source.Repository))
            throw new InvalidOperationException($"La source GitHub du composant '{component.Name}' est incomplète.");

        var url = $"https://api.github.com/repos/{source.Repository}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("WorkspaceBootstrap/1.0");

        using var response = await _http.SendAsync(request, token);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var tag = doc.RootElement.GetProperty("tag_name").GetString()
            ?? throw new InvalidOperationException("Release GitHub sans version.");

        var versionMatch = Regex.Match(tag, source.VersionRegex ?? "^v(.+)$");
        if (!versionMatch.Success)
            throw new InvalidOperationException($"Version GitHub inattendue : {tag}");

        var asset = doc.RootElement.GetProperty("assets")
            .EnumerateArray()
            .Select(x => new
            {
                Name = x.GetProperty("name").GetString()!,
                Url = x.GetProperty("browser_download_url").GetString()!
            })
            .FirstOrDefault(x => Regex.IsMatch(
                x.Name,
                source.AssetRegex ?? ".*",
                RegexOptions.IgnoreCase))
            ?? throw new InvalidOperationException(
                $"Aucun asset officiel correspondant pour {component.Name}.");

        return await DownloadAndVerifyAsync(
            component,
            versionMatch.Groups[1].Value,
            asset.Url,
            asset.Name,
            token);
    }

    private async Task<InstallerArtifact> ResolveRarLabAsync(
        ComponentManifest component,
        CancellationToken token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://www.rarlab.com/download.htm");
        request.Headers.UserAgent.ParseAdd("WorkspaceBootstrap/1.0");

        using var response = await _http.SendAsync(request, token);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(token);
        var match = Regex.Match(
            html,
            @"href=[\"'](?<url>[^\"']*winrar-x64-(?<version>\d+)fr\.exe)[\"']",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            throw new InvalidOperationException(
                "Installeur WinRAR français x64 introuvable sur RARLAB.");

        var relative = match.Groups["url"].Value;
        var uri = new Uri(new Uri("https://www.rarlab.com/"), relative);

        return await DownloadAndVerifyAsync(
            component,
            match.Groups["version"].Value,
            uri.ToString(),
            Path.GetFileName(uri.LocalPath),
            token);
    }

    private async Task<InstallerArtifact> ResolveChromeAsync(
        ComponentManifest component,
        CancellationToken token)
    {
        const string versionApi =
            "https://versionhistory.googleapis.com/v1/chrome/platforms/win64/channels/stable/versions?order_by=version%20desc";

        using var response = await _http.GetAsync(versionApi, token);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var version = doc.RootElement.GetProperty("versions")[0]
            .GetProperty("version")
            .GetString()
            ?? throw new InvalidOperationException("Version Chrome introuvable.");

        return await DownloadAndVerifyAsync(
            component,
            version,
            "https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi",
            "googlechromestandaloneenterprise64.msi",
            token);
    }

    private async Task<InstallerArtifact?> InstallViaWingetFallbackAsync(
        ComponentManifest component,
        CancellationToken token)
    {
        if (!string.Equals(
            component.FallbackPackageManager,
            "winget",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Aucune source d'installation prise en charge pour '{component.Name}'.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "winget.exe",
            Arguments =
                $"install --id {EscapeArgument(component.PackageId!)} --exact " +
                "--accept-source-agreements --accept-package-agreements --silent",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("WinGet est introuvable.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
        var stderrTask = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"WinGet a échoué pour '{component.Name}' (code {process.ExitCode}) : {stderr.Trim()}");
        }

        return null;
    }

    private async Task<InstallerArtifact> DownloadAndVerifyAsync(
        ComponentManifest component,
        string version,
        string url,
        string fileName,
        CancellationToken token)
    {
        var cached = FindVerifiedCached(component, version);
        if (cached is not null)
            return cached;

        var componentDir = Path.Combine(_paths.InstallersRoot, Sanitize(component.Id));
        var metadataDir = Path.Combine(_paths.MetadataRoot, Sanitize(component.Id));
        Directory.CreateDirectory(componentDir);
        Directory.CreateDirectory(metadataDir);

        var finalPath = Path.Combine(componentDir, fileName);
        var staging = Path.Combine(
            _paths.StagingRoot,
            $"{Sanitize(component.Id)}-{Sanitize(version)}-{Guid.NewGuid():N}.download");

        try
        {
            using var response = await _http.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                token);
            response.EnsureSuccessStatusCode();

            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = File.Create(staging))
            {
                await input.CopyToAsync(output, token);
            }

            if (new FileInfo(staging).Length == 0)
                throw new InvalidOperationException("L'installeur téléchargé est vide.");

            var sha = ComputeSha256(staging);
            ValidateAuthenticodeIfRequired(staging);

            if (File.Exists(finalPath))
            {
                var existingHash = ComputeSha256(finalPath);
                if (!string.Equals(existingHash, sha, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Le cache contient déjà un fichier différent pour {component.Name} {version}.");
            }
            else
            {
                File.Move(staging, finalPath);
            }

            var artifact = new InstallerArtifact(
                component.Id,
                version,
                finalPath,
                "official",
                url,
                sha);

            PersistMetadata(artifact, metadataDir);
            return artifact;
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }

    private static void PersistMetadata(
        InstallerArtifact artifact,
        string metadataDir)
    {
        var path = Path.Combine(
            metadataDir,
            $"{Sanitize(artifact.Version)}-{artifact.Sha256[..12]}.json");

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(artifact, JsonDefaults.Options));
    }

    private static void ValidateAuthenticodeIfRequired(string path)
    {
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".msi", StringComparison.OrdinalIgnoreCase))
            return;

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Authenticode n'est disponible que sur Windows.");

        try
        {
            using var certificate = new X509Certificate2(
                X509Certificate.CreateFromSignedFile(path));
            using var chain = new X509Chain();

            if (!chain.Build(certificate))
            {
                var status = string.Join(
                    "; ",
                    chain.ChainStatus.Select(x => x.StatusInformation.Trim()));
                throw new CryptographicException(
                    $"Chaîne de signature Authenticode invalide : {status}");
            }
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"Signature Authenticode invalide : {path}",
                ex);
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Sanitize(string value) =>
        Regex.Replace(value, @"[^A-Za-z0-9._-]", "_");

    private static string EscapeArgument(string value) =>
        value.Contains(' ')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
