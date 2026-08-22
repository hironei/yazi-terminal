using System.IO;

namespace YaziDesktopHost;

public sealed record CommandLineOptions(bool UseLastInstance, string InitialDirectory)
{
    public const string LastInstanceOption = "--last-instance";

    public static CommandLineOptions Parse(
        IReadOnlyList<string> args,
        string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var useLastInstance = false;
        string? directory = null;

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

            if (directory is not null)
            {
                throw new CommandLineParseException("Only one directory argument is supported.");
            }

            directory = argument;
        }

        string resolvedDirectory;
        try
        {
            resolvedDirectory = Path.GetFullPath(directory ?? currentDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or IOException
            or NotSupportedException)
        {
            throw new CommandLineParseException("The directory argument is not a valid path.", exception);
        }

        return new CommandLineOptions(useLastInstance, resolvedDirectory);
    }
}

public sealed class CommandLineParseException : Exception
{
    public CommandLineParseException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
