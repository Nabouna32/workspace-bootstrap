using Microsoft.Win32;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Infrastructure;

public sealed record RegistryObservation(
    bool Exists,
    string? Value,
    string? ValueType,
    string? Error);

public interface IRegistryReader
{
    RegistryObservation Read(RegistrySettingDesiredState desired);
}

public sealed class RegistryDesiredStateObserver(IRegistryReader? reader = null)
{
    private readonly IRegistryReader _reader = reader ?? new WindowsRegistryReader();

    public RegistryObservation Observe(RegistrySettingDesiredState desired) =>
        _reader.Read(desired);

    private sealed class WindowsRegistryReader : IRegistryReader
    {
        public RegistryObservation Read(RegistrySettingDesiredState desired)
        {
            try
            {
                var hive = desired.Hive.Trim().ToUpperInvariant() switch
                {
                    "HKCU" => RegistryHive.CurrentUser,
                    "HKLM" => RegistryHive.LocalMachine,
                    _ => throw new InvalidOperationException($"Unsupported registry hive '{desired.Hive}'.")
                };

                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(desired.Key, writable: false);

                if (key is null)
                    return new RegistryObservation(false, null, null, null);

                var value = key.GetValue(
                    desired.ValueName,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);

                if (value is null)
                    return new RegistryObservation(false, null, null, null);

                return new RegistryObservation(
                    true,
                    FormatValue(value),
                    FormatValueType(key.GetValueKind(desired.ValueName)));
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException
                or System.Security.SecurityException
                or IOException
                or ArgumentException)
            {
                return new RegistryObservation(
                    false,
                    null,
                    null,
                    $"Unable to read registry value '{desired.Hive}\\{desired.Key}\\{desired.ValueName}': {ex.Message}");
            }
        }

        private static string FormatValue(object value) =>
            value switch
            {
                byte[] bytes => Convert.ToHexString(bytes),
                _ => Convert.ToString(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
            };

        private static string FormatValueType(RegistryValueKind kind) =>
            kind switch
            {
                RegistryValueKind.String => "string",
                RegistryValueKind.ExpandString => "string",
                RegistryValueKind.DWord => "dword",
                RegistryValueKind.QWord => "qword",
                RegistryValueKind.Binary => "binary",
                _ => kind.ToString().ToLowerInvariant()
            };
    }
}
