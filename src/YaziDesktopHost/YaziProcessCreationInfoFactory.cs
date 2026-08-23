using System.Security.Cryptography;

namespace YaziDesktopHost;

public sealed record YaziProcessLaunchInfo(string CommandLine, string ClientId);

public static class YaziProcessLaunchConfiguration
{
    private static readonly string[] BridgeEnvironmentNames =
    [
        "YAZI_DESKTOP_HOST_PIPE",
        "YAZI_DESKTOP_HOST_INSTANCE_ID",
        "YAZI_DESKTOP_HOST_PROTOCOL",
        "YAZI_CONFIG_HOME",
        "COLORTERM",
        "TERM",
        "NO_COLOR",
    ];

    public static string CreateCommandLine(string executable)
    {
        return Create(executable).CommandLine;
    }

    public static YaziProcessLaunchInfo Create(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        var clientId = CreateClientId().ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new YaziProcessLaunchInfo(
            $"{Quote(executable)} --client-id {clientId}",
            clientId);
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
            Environment.SetEnvironmentVariable(
                "YAZI_CONFIG_HOME",
                YaziThemeLoader.ResolveConfigHome());
            Environment.SetEnvironmentVariable("COLORTERM", "truecolor");
            Environment.SetEnvironmentVariable("TERM", "xterm-256color");
            Environment.SetEnvironmentVariable("NO_COLOR", null);
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
