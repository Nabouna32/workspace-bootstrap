using Microsoft.Win32;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Infrastructure;

public interface IRegistryWriter
{
    WorkspaceRegistrySnapshot Capture(RegistrySettingDesiredState desired);
    void Write(RegistrySettingDesiredState desired);
    void Restore(WorkspaceRegistrySnapshot snapshot);
}

public sealed class RegistryDesiredStateWriter(IRegistryWriter? writer = null)
{
    private readonly IRegistryWriter _writer = writer ?? new WindowsRegistryWriter();

    public WorkspaceRegistrySnapshot Capture(RegistrySettingDesiredState desired) =>
        _writer.Capture(desired);

    public void Write(RegistrySettingDesiredState desired) =>
        _writer.Write(desired);

    public void Restore(WorkspaceRegistrySnapshot snapshot) =>
        _writer.Restore(snapshot);

    private sealed class WindowsRegistryWriter : IRegistryWriter
    {
        public WorkspaceRegistrySnapshot Capture(RegistrySettingDesiredState desired)
        {
            var hive = ParseHive(desired.Hive);

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(desired.Key, writable: false);

            if (key is null)
                return new WorkspaceRegistrySnapshot(
                    desired.Hive.ToUpperInvariant(),
                    desired.Key,
                    desired.ValueName,
                    false,
                    null,
                    null);

            var value = key.GetValue(
                desired.ValueName,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);

            if (value is null)
                return new WorkspaceRegistrySnapshot(
                    desired.Hive.ToUpperInvariant(),
                    desired.Key,
                    desired.ValueName,
                    false,
                    null,
                    null);

            return new WorkspaceRegistrySnapshot(
                desired.Hive.ToUpperInvariant(),
                desired.Key,
                desired.ValueName,
                true,
                FormatValue(value),
                FormatValueType(key.GetValueKind(desired.ValueName)));
        }

        public void Write(RegistrySettingDesiredState desired)
        {
            ValidateValueType(desired.ValueType);

            var hive = ParseHive(desired.Hive);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.CreateSubKey(desired.Key, writable: true)
                ?? throw new InvalidOperationException(
                    $"Unable to open registry key '{desired.Hive}\\{desired.Key}' for writing.");

            key.SetValue(
                desired.ValueName,
                ParseValue(desired.Value, desired.ValueType),
                ParseValueKind(desired.ValueType));
        }

        public void Restore(WorkspaceRegistrySnapshot snapshot)
        {
            var hive = ParseHive(snapshot.Hive);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(snapshot.Key, writable: true);

            if (snapshot.Exists)
            {
                if (key is null)
                    throw new InvalidOperationException(
                        $"Unable to open registry key '{snapshot.Hive}\\{snapshot.Key}' for rollback.");

                ValidateValueType(snapshot.ValueType);
                key.SetValue(
                    snapshot.ValueName,
                    ParseValue(snapshot.Value ?? string.Empty, snapshot.ValueType!),
                    ParseValueKind(snapshot.ValueType!));
                return;
            }

            if (key is not null)
            {
                key.DeleteValue(snapshot.ValueName, throwOnMissingValue: false);
            }
        }

        private static RegistryHive ParseHive(string value) =>
            value.Trim().ToUpperInvariant() switch
            {
                "HKCU" => RegistryHive.CurrentUser,
                "HKLM" => RegistryHive.LocalMachine,
                _ => throw new InvalidOperationException($"Unsupported registry hive '{value}'.")
            };

        private static void ValidateValueType(string? valueType)
        {
            if (valueType is null)
                throw new InvalidOperationException("Registry value type is required.");

            _ = ParseValueKind(valueType);
        }

        private static RegistryValueKind ParseValueKind(string valueType) =>
            valueType.Trim().ToLowerInvariant() switch
            {
                "string" => RegistryValueKind.String,
                "expandstring" => RegistryValueKind.ExpandString,
                "dword" => RegistryValueKind.DWord,
                "qword" => RegistryValueKind.QWord,
                "binary" => RegistryValueKind.Binary,
                _ => throw new InvalidOperationException(
                    $"Unsupported registry value type '{valueType}'.")
            };

        private static object ParseValue(string value, string valueType) =>
            ParseValueKind(valueType) switch
            {
                RegistryValueKind.String => value,
                RegistryValueKind.ExpandString => value,
                RegistryValueKind.DWord => ParseInteger(value, sizeof(int)),
                RegistryValueKind.QWord => ParseInteger(value, sizeof(long)),
                RegistryValueKind.Binary => ParseBinary(value),
                _ => throw new InvalidOperationException(
                    $"Unsupported registry value type '{valueType}'.")
            };

        private static object ParseInteger(string value, int size)
        {
            if (!long.TryParse(
                    value,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed))
            {
                throw new InvalidOperationException(
                    $"Registry value '{value}' is not a valid integer.");
            }

            return size == sizeof(int)
                ? checked((int)parsed)
                : parsed;
        }

        private static object ParseBinary(string value)
        {
            try
            {
                return Convert.FromHexString(value);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    "Binary registry values must be encoded as hexadecimal text.",
                    ex);
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
