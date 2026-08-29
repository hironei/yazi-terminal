using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace YaziDesktopHost;

public static class YaziCommandController
{
    public static ProcessStartInfo CreateStartInfo(
        string yaExecutable,
        string clientId,
        string run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        if (!TryTokenize(run, out var tokens) || tokens.Count == 0)
        {
            throw new ArgumentException("The Yazi action is empty or malformed.", nameof(run));
        }

        return CreateStartInfo(
            yaExecutable,
            clientId,
            tokens[0],
            tokens.Skip(1).ToArray());
    }

    public static ProcessStartInfo CreateStartInfo(
        string yaExecutable,
        string clientId,
        string command,
        IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = yaExecutable,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("emit-to");
        startInfo.ArgumentList.Add(clientId);
        startInfo.ArgumentList.Add(command);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    public static async Task<bool> ExecuteAsync(
        string yaExecutable,
        string clientId,
        string run,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(yaExecutable, clientId, [run], cancellationToken).ConfigureAwait(false);
    }

    public static Task<bool> ExecuteAsync(
        string yaExecutable,
        string clientId,
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            CreateStartInfo(yaExecutable, clientId, command, arguments),
            cancellationToken);
    }

    public static async Task<bool> ExecuteAsync(
        string yaExecutable,
        string clientId,
        IReadOnlyList<string> runs,
        CancellationToken cancellationToken = default)
    {
        var startInfos = CreateStartInfos(yaExecutable, clientId, runs);
        foreach (var startInfo in startInfos)
        {
            if (!await ExecuteAsync(startInfo, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<ProcessStartInfo> CreateStartInfos(
        string yaExecutable,
        string clientId,
        IReadOnlyList<string> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        if (runs.Count == 0)
        {
            throw new ArgumentException("The Yazi action sequence is empty.", nameof(runs));
        }

        return runs.Select(run => CreateStartInfo(yaExecutable, clientId, run)).ToArray();
    }

    private static async Task<bool> ExecuteAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = startInfo,
        };

        try
        {
            if (!process.Start())
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited while cancellation was being handled.
            }
            catch (Win32Exception)
            {
                // The process may already have been reaped by the OS.
            }

            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    public static bool TryTokenize(string? run, out IReadOnlyList<string> tokens)
    {
        tokens = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(run))
        {
            return false;
        }

        var result = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var tokenStarted = false;

        for (var index = 0; index < run.Length; index++)
        {
            var character = run[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                    tokenStarted = true;
                }
                else if (character == '\\'
                         && quote == '"'
                         && index + 1 < run.Length
                         && run[index + 1] is '"' or '\\')
                {
                    current.Append(run[++index]);
                    tokenStarted = true;
                }
                else
                {
                    current.Append(character);
                    tokenStarted = true;
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                tokenStarted = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (tokenStarted)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    tokenStarted = false;
                }
            }
            else if (character == '\\'
                     && index + 1 < run.Length
                     && run[index + 1] is '"' or '\\')
            {
                current.Append(run[++index]);
                tokenStarted = true;
            }
            else
            {
                current.Append(character);
                tokenStarted = true;
            }
        }

        if (quote != '\0')
        {
            return false;
        }

        if (tokenStarted)
        {
            result.Add(current.ToString());
        }

        if (result.Any(string.IsNullOrWhiteSpace)
            || result.Any(token => token.Contains('\r') || token.Contains('\n')))
        {
            return false;
        }

        tokens = result;
        return result.Count > 0;
    }
}
