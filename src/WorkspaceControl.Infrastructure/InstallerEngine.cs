using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkspaceControl.Infrastructure;

public sealed class InstallerEngine
{
    private readonly WorkspacePaths _paths;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(20) };

    public InstallerEngine(WorkspacePaths paths) => _paths = paths;

    public async Task InstallAsync(ComponentManifest component, string actionCode, string? desiredVersion, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(component.PackageId))
            throw new InvalidOperationException($"Le composant '{component.Id}' ne possède aucun packageId.");

        if (actionCode is not (ProvisioningActionCodes.Install or ProvisioningActionCodes.Update))
            throw new InvalidOperationException($"Unsupported provisioning action '{actionCode}' for '{component.Name}'.");

        var artifact = await ResolveArtifactAsync(component, actionCode, desiredVersion, token);
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
            psi.ArgumentList.Add("/i");
            psi.ArgumentList.Add(artifact.FilePath);
            foreach (var argument in arguments)
                psi.ArgumentList.Add(argument);
        }
        else
        {
            foreach (var argument in arguments)
                psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Impossible de lancer l'installeur de '{component.Name}'.");
        await process.WaitForExitAsync(token);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"L'installation de '{component.Name}' a échoué (code {process.ExitCode}).");
    }

    public async Task UninstallAsync(
        ComponentManifest component,
        CancellationToken token)
    {
        if (!string.Equals(
            component.FallbackPackageManager,
            "winget",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Application '{component.Name}' does not declare a supported uninstall provider.");
        }

        if (string.IsNullOrWhiteSpace(component.PackageId))
            throw new InvalidOperationException(
                $"Application '{component.Name}' does not declare a package id.");

        await RunWingetAsync(
            BuildWingetArguments(component, ProvisioningActionCodes.Remove),
            component.Name,
            token);
    }

    private async Task<InstallerArtifact?> ResolveArtifactAsync(
        ComponentManifest component,
        string actionCode,
        string? desiredVersion,
        CancellationToken token)
    {
        if (component.OfficialSource is null)
            return await InstallViaWingetFallbackAsync(component, actionCode, desiredVersion, token);

        return component.OfficialSource.Type switch
        {
            "github-release" => await ResolveGitHubReleaseAsync(component, component.OfficialSource, desiredVersion, token),
            "rarlab-localized" => await ResolveRarLabAsync(component, desiredVersion, token),
            "chrome-enterprise" => await ResolveChromeAsync(component, desiredVersion, token),
            _ => await InstallViaWingetFallbackAsync(component, actionCode, desiredVersion, token)
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
                    || !IsValidArtifactPath(artifact.FilePath)
                    || string.IsNullOrWhiteSpace(artifact.Sha256)
                    || artifact.Sha256.Length != 64
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
            catch (InvalidOperationException)
            {
                // A cache entry with invalid Authenticode is not trusted.
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
        string? desiredVersion,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(source.Repository))
            throw new InvalidOperationException($"La source GitHub du composant '{component.Name}' est incomplète.");

        var url = string.IsNullOrWhiteSpace(desiredVersion)
            ? $"https://api.github.com/repos/{source.Repository}/releases/latest"
            : $"https://api.github.com/repos/{source.Repository}/releases/tags/{Uri.EscapeDataString(desiredVersion.StartsWith('v') ? desiredVersion : $"v{desiredVersion}")}";
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

        var resolvedVersion = versionMatch.Groups[1].Value;
        if (!string.IsNullOrWhiteSpace(desiredVersion)
            && !string.Equals(resolvedVersion, desiredVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Official source resolved version {resolvedVersion}, but {desiredVersion} was required.");

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
            resolvedVersion,
            asset.Url,
            asset.Name,
            token);
    }

    private async Task<InstallerArtifact> ResolveRarLabAsync(
        ComponentManifest component,
        string? desiredVersion,
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
            @"href=['""](?<url>[^'""]*winrar-x64-(?<version>\d+)fr\.exe)['""]",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            throw new InvalidOperationException(
                "Installeur WinRAR français x64 introuvable sur RARLAB.");

        var resolvedVersion = match.Groups["version"].Value;
        if (!string.IsNullOrWhiteSpace(desiredVersion)
            && !string.Equals(resolvedVersion, desiredVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Official source resolved version {resolvedVersion}, but {desiredVersion} was required.");

        var relative = match.Groups["url"].Value;
        var uri = new Uri(new Uri("https://www.rarlab.com/"), relative);

        return await DownloadAndVerifyAsync(
            component,
            resolvedVersion,
            uri.ToString(),
            Path.GetFileName(uri.LocalPath),
            token);
    }

    private async Task<InstallerArtifact> ResolveChromeAsync(
        ComponentManifest component,
        string? desiredVersion,
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

        if (!string.IsNullOrWhiteSpace(desiredVersion)
            && !string.Equals(version, desiredVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Official source resolved version {version}, but {desiredVersion} was required.");

        return await DownloadAndVerifyAsync(
            component,
            version,
            "https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi",
            "googlechromestandaloneenterprise64.msi",
            token);
    }

    private async Task<InstallerArtifact?> InstallViaWingetFallbackAsync(
        ComponentManifest component,
        string actionCode,
        string? desiredVersion,
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

        await RunWingetAsync(
            BuildWingetArguments(component, actionCode, desiredVersion),
            component.Name,
            token);

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

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName) || !string.Equals(safeFileName, fileName, StringComparison.Ordinal))
            throw new InvalidOperationException($"Nom d'installeur invalide : {fileName}");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var downloadUri)
            || downloadUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"La source d'installation doit utiliser HTTPS : {url}");

        var finalPath = Path.Combine(componentDir, safeFileName);
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

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(artifact, JsonDefaults.Options));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
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

        using var fileInfo = new WinTrustFileInfo(path);
        using var trustData = new WinTrustData(fileInfo);

        var action = WinTrustVerifyActionGenericVerifyV2;
        try
        {
            var status = WinVerifyTrust(IntPtr.Zero, ref action, ref trustData.Native);

            if (status != 0)
            {
                throw new InvalidOperationException(
                    $"Signature Authenticode invalide ou non approuvée : {path} (HRESULT 0x{status:X8}).");
            }
        }
        finally
        {
            trustData.Native.dwStateAction = WinTrustStateActionClose;
            _ = WinVerifyTrust(IntPtr.Zero, ref action, ref trustData.Native);
        }
    }

    private static readonly Guid WinTrustVerifyActionGenericVerifyV2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private const uint WinTrustUiChoiceNone = 2;
    private const uint WinTrustRevocationCheckWholeChain = 1;
    private const uint WinTrustUnionChoiceFile = 1;
    private const uint WinTrustStateActionIgnore = 0;
    private const uint WinTrustStateActionClose = 2;
    private const uint WinTrustProviderFlagsSafer = 0x00000100;
    private const uint WinTrustSignatureSettingsNoUi = 0;

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    private static extern uint WinVerifyTrust(
        IntPtr hwnd,
        ref Guid pgActionID,
        ref WinTrustDataNative pWvtData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustDataNative
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWvtStateData;
        public IntPtr pwszUrlReference;
        public uint dwProvFlags;
        public uint dwUiContext;
        public IntPtr pSignatureSettings;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfoNative
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    private sealed class WinTrustFileInfo : IDisposable
    {
        public IntPtr NativePointer { get; }
        private IntPtr _pathPointer;

        public WinTrustFileInfo(string path)
        {
            _pathPointer = Marshal.StringToCoTaskMemUni(path);
            var native = new WinTrustFileInfoNative
            {
                cbStruct = (uint)Marshal.SizeOf<WinTrustFileInfoNative>(),
                pcwszFilePath = _pathPointer
            };
            NativePointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfoNative>());
            Marshal.StructureToPtr(native, NativePointer, fDeleteOld: false);
        }

        public void Dispose()
        {
            if (NativePointer != IntPtr.Zero)
                Marshal.FreeCoTaskMem(NativePointer);
            if (_pathPointer != IntPtr.Zero)
                Marshal.FreeCoTaskMem(_pathPointer);
        }
    }

    private sealed class WinTrustData : IDisposable
    {
        private readonly WinTrustFileInfo _fileInfo;

        public WinTrustDataNative Native;

        public WinTrustData(WinTrustFileInfo fileInfo)
        {
            _fileInfo = fileInfo;
            Native = new WinTrustDataNative
            {
                cbStruct = (uint)Marshal.SizeOf<WinTrustDataNative>(),
                dwUIChoice = WinTrustUiChoiceNone,
                fdwRevocationChecks = WinTrustRevocationCheckWholeChain,
                dwUnionChoice = WinTrustUnionChoiceFile,
                pFile = fileInfo.NativePointer,
                dwStateAction = WinTrustStateActionIgnore,
                dwProvFlags = WinTrustProviderFlagsSafer,
                dwUiContext = WinTrustSignatureSettingsNoUi
            };
        }

        public void Dispose() => _fileInfo.Dispose();
    }

    private bool IsValidArtifactPath(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            return false;

        var installersRoot = Path.GetFullPath(_paths.InstallersRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path);

        return candidate.StartsWith(installersRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    internal static IReadOnlyList<string> BuildWingetArguments(
        ComponentManifest component,
        string actionCode,
        string? desiredVersion = null)
    {
        if (string.IsNullOrWhiteSpace(component.PackageId))
            throw new InvalidOperationException("WinGet operation requires a package id.");

        var command = actionCode switch
        {
            ProvisioningActionCodes.Install => "install",
            ProvisioningActionCodes.Update => "upgrade",
            ProvisioningActionCodes.Remove => "uninstall",
            _ => throw new InvalidOperationException($"Unsupported WinGet provisioning action: {actionCode}")
        };

        var arguments = new List<string>
        {
            command,
            "--id",
            component.PackageId,
            "--exact",
            "--accept-source-agreements",
            "--accept-package-agreements",
            "--silent"
        };

        if (!string.IsNullOrWhiteSpace(desiredVersion))
        {
            arguments.Add("--version");
            arguments.Add(desiredVersion);
        }

        return arguments;
    }

    private static string Sanitize(string value) =>
        Regex.Replace(value, @"[^A-Za-z0-9._-]", "_");

}
