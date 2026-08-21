using System.Security.Cryptography;
using VirtualTerminal;
using VirtualTerminal.Interop;

namespace YaziDesktopHost;

public static class YaziProcessCreationInfoFactory
{
    private static readonly object EnvironmentGate = new();
    private static readonly string[] BridgeEnvironmentNames =
    [
        "YAZI_DESKTOP_HOST_PIPE",
        "YAZI_DESKTOP_HOST_INSTANCE_ID",
        "YAZI_DESKTOP_HOST_PROTOCOL",
    ];

    public static ProcessCreationInfo Create(
        string executable,
        string currentDirectory,
        Guid instanceId,
        string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("The bridge instance identifier must not be empty.", nameof(instanceId));
        }

        return new ProcessCreationInfo
        {
            ApplicationName = executable,
            // Yazi's --client-id parser accepts a globally unique number, not
            // the GUID used by the host-side bridge contract.
            CommandLine = $"{Quote(executable)} --client-id {CreateClientId()}",
            CurrentDirectory = currentDirectory,
        };
    }

    public static CommandLineSession StartSession(
        string executable,
        string currentDirectory,
        Guid instanceId,
        string pipeName)
    {
        var processInfo = Create(executable, currentDirectory, instanceId, pipeName);
        lock (EnvironmentGate)
        {
            // VirtualTerminal.CommandLine 1.8.1 passes ProcessCreationInfo.Environment
            // to CreateProcess without CREATE_UNICODE_ENVIRONMENT. Temporarily setting
            // the host environment lets CreateProcess inherit the bridge values while
            // avoiding that incompatible environment-block path.
            using var environment = new BridgeEnvironmentScope(instanceId, pipeName);
            return new CommandLineSession(processInfo);
        }
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static long CreateClientId()
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        RandomNumberGenerator.Fill(bytes);
        var clientId = BitConverter.ToInt64(bytes) & long.MaxValue;
        return clientId == 0 ? 1 : clientId;
    }

    private sealed class BridgeEnvironmentScope : IDisposable
    {
        private readonly IReadOnlyDictionary<string, string?> _previousValues;

        public BridgeEnvironmentScope(Guid instanceId, string pipeName)
        {
            _previousValues = BridgeEnvironmentNames.ToDictionary(
                name => name,
                Environment.GetEnvironmentVariable,
                StringComparer.OrdinalIgnoreCase);

            Environment.SetEnvironmentVariable("YAZI_DESKTOP_HOST_PIPE", pipeName);
            Environment.SetEnvironmentVariable("YAZI_DESKTOP_HOST_INSTANCE_ID", instanceId.ToString("D"));
            Environment.SetEnvironmentVariable(
                "YAZI_DESKTOP_HOST_PROTOCOL",
                YaziBridgeMessageParser.SupportedProtocol);
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previousValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
