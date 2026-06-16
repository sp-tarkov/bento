using System.ComponentModel;
using System.Diagnostics;

namespace Bento;

/// <summary>
/// Runs child processes. A streaming variant for long build commands and a capturing variant for short tool and git
/// queries. Both redirect output and never use a shell.
/// </summary>
public static class ProcessRunner
{
    /// <summary>
    /// Runs a process, streaming merged stdout/stderr lines to <paramref name="onLine"/>.
    /// </summary>
    public static async Task<int> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null,
        Action<string>? onLine = null,
        CancellationToken cancellationToken = default
    )
    {
        using var process = Create(fileName, arguments, workingDirectory, environment);
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                onLine?.Invoke(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                onLine?.Invoke(e.Data);
            }
        };

        Start(process, fileName);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    /// <summary>
    /// Runs a short process and captures its output. For tool checks and git queries.
    /// </summary>
    public static async Task<(int ExitCode, string StdOut, string StdErr)> CaptureAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        using var process = Create(fileName, arguments, workingDirectory, environment: null);
        Start(process, fileName);
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, (await stdOutTask).Trim(), (await stdErrTask).Trim());
    }

    /// <summary>
    /// Builds a redirected, non-shell process with the given arguments, working directory and environment overrides.
    /// </summary>
    private static Process Create(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environment
    )
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is null)
        {
            return new Process { StartInfo = startInfo };
        }

        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        return new Process { StartInfo = startInfo };
    }

    /// <summary>
    /// Starts the process, translating a missing executable into a hinted BentoException.
    /// </summary>
    private static void Start(Process process, string fileName)
    {
        try
        {
            if (!process.Start())
            {
                throw new BentoException($"Failed to start '{fileName}'.");
            }
        }
        catch (Win32Exception ex)
        {
            throw new BentoException(
                $"Failed to start '{fileName}': {ex.Message}",
                $"Is '{fileName}' installed and on PATH?"
            );
        }
    }
}
