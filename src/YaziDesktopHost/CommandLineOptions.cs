using System.IO;

namespace YaziDesktopHost;

public sealed record CommandLineOptions(
    bool UseLastInstance,
    string InitialDirectory,
    string? FilePath = null)
{
    public const string LastInstanceOption = "--last-instance";

    public static CommandLineOptions Parse(
        IReadOnlyList<string> args,
        string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var useLastInstance = false;
        string? path = null;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, LastInstanceOption, StringComparison.OrdinalIgnoreCase))
            {
                if (useLastInstance)
                {
                    throw new CommandLineParseException("--last-instance was specified more than once.");
                }

                useLastInstance = true;
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new CommandLineParseException($"Unknown option: {argument}");
            }

            if (path is not null)
            {
                throw new CommandLineParseException("Only one path argument is supported.");
            }

            path = argument;
        }

        string resolvedPath;
        try
        {
            resolvedPath = Path.GetFullPath(path ?? currentDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or IOException
            or NotSupportedException)
        {
            throw new CommandLineParseException("The path argument is not a valid path.", exception);
        }

        if (path is not null && File.Exists(resolvedPath))
        {
            var parentDirectory = Path.GetDirectoryName(resolvedPath);
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                throw new CommandLineParseException("The file argument has no parent directory.");
            }

            return new CommandLineOptions(useLastInstance, parentDirectory, resolvedPath);
        }

        return new CommandLineOptions(useLastInstance, resolvedPath, null);
    }
}

public sealed class CommandLineParseException : Exception
{
    public CommandLineParseException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
