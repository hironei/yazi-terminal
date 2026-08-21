using System.Security.Cryptography;

namespace YaziDesktopHost;

public static class YaziProcessLaunchConfiguration
{
    private static readonly string[] BridgeEnvironmentNames =
    [
        "YAZI_DESKTOP_HOST_PIPE",
        "YAZI_DESKTOP_HOST_INSTANCE_ID",
        "YAZI_DESKTOP_HOST_PROTOCOL",
    ];

    public static string CreateCommandLine(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        return $"{Quote(executable)} --client-id {CreateClientId()}";
    }

    public static IDisposable EnterBridgeEnvironment(Guid instanceId, string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("The bridge instance identifier must not be empty.", nameof(instanceId));
        }

        return new BridgeEnvironmentScope(instanceId, pipeName);
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
