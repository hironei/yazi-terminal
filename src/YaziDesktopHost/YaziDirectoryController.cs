using System.ComponentModel;
using System.Diagnostics;

namespace YaziDesktopHost;

public static class YaziDirectoryController
{
    public static ProcessStartInfo CreateStartInfo(
        string yaExecutable,
        string clientId,
        string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var startInfo = new ProcessStartInfo
        {
            FileName = yaExecutable,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("emit-to");
        startInfo.ArgumentList.Add(clientId);
        startInfo.ArgumentList.Add("cd");
        startInfo.ArgumentList.Add(directory);
        return startInfo;
    }

    public static async Task<bool> ChangeDirectoryAsync(
        string yaExecutable,
        string clientId,
        string directory,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(yaExecutable, clientId, directory),
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
}
