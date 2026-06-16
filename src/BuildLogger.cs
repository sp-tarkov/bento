using Spectre.Console;

namespace Bento;

/// <summary>
/// Routes build output. Stage milestones go to the console (or to the live progress display via
/// <see cref="StatusSink"/> while the parallel stages run); full child-process output always goes to a per-stage log
/// file, and additionally to the console when there is no interactive terminal.
/// </summary>
public sealed class BuildLogger : IDisposable
{
    private readonly string _logDir;
    private readonly bool _interactive;
    private readonly Dictionary<string, StreamWriter> _writers = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// When set (during the live progress display), stage milestones go here instead of the console.
    /// </summary>
    public Action<string, string>? StatusSink { get; set; }

    /// <summary>
    /// Creates the logger, clearing and recreating the log directory.
    /// </summary>
    public BuildLogger(string logDir, bool interactive)
    {
        _logDir = logDir;
        _interactive = interactive;
        Fs.DeleteDirectory(logDir);
        Directory.CreateDirectory(logDir);
    }

    /// <summary>
    /// The log file path for a stage.
    /// </summary>
    public string LogPath(string stage)
    {
        return Path.Combine(_logDir, $"{stage}.log");
    }

    /// <summary>
    /// A stage milestone: always logged, surfaced to the user via console or progress display.
    /// </summary>
    public void Status(string stage, string message)
    {
        WriteToFile(stage, $"==> {message}");
        if (StatusSink is { } sink)
        {
            sink(stage, message);
            return;
        }

        lock (_gate)
        {
            AnsiConsole.MarkupLine($"[blue][[{stage}]][/] {Markup.Escape(message)}");
        }
    }

    /// <summary>
    /// A raw child-process output line.
    /// </summary>
    public void Line(string stage, string line)
    {
        WriteToFile(stage, line);
        if (_interactive)
        {
            return;
        }

        lock (_gate)
        {
            AnsiConsole.MarkupLine($"[grey][[{stage}]][/] {Markup.Escape(line)}");
        }
    }

    /// <summary>
    /// Returns the last count lines of a stage's log, after flushing the open writer.
    /// </summary>
    public IReadOnlyList<string> Tail(string stage, int count)
    {
        lock (_gate)
        {
            if (_writers.TryGetValue(stage, out var writer))
            {
                writer.Flush();
            }
        }

        var path = LogPath(stage);
        if (!File.Exists(path))
        {
            return [];
        }

        // The writer still holds the file open; share read & write to peek at it.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines.Count <= count ? lines : lines[^count..];
    }

    /// <summary>
    /// Appends a line to the stage's log file, opening the writer on first use.
    /// </summary>
    private void WriteToFile(string stage, string line)
    {
        lock (_gate)
        {
            if (!_writers.TryGetValue(stage, out var writer))
            {
                writer = new StreamWriter(LogPath(stage), append: true) { AutoFlush = true };
                _writers[stage] = writer;
            }

            writer.WriteLine(line);
        }
    }

    /// <summary>
    /// Closes all open log writers.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var writer in _writers.Values)
            {
                writer.Dispose();
            }

            _writers.Clear();
        }
    }
}
