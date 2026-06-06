namespace WinLogAnalyzer.Core.Logging;

public enum LogLevel { INFO, WARN, ERROR }

/// <summary>
/// Logger fichier simple (thread-safe) ecrivant dans %AppData%/WinLogAnalyzer/logs/app.log.
/// Format : [ISO8601] [LEVEL] message. Rotation au-dela de 1 Mo.
/// </summary>
public sealed class FileLogger
{
    private static readonly object Gate = new();
    private readonly string _path;
    private const long MaxBytes = 1_048_576; // 1 Mo

    public FileLogger()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinLogAnalyzer", "logs");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "app.log");
    }

    public void Info(string msg) => Write(LogLevel.INFO, msg);
    public void Warn(string msg) => Write(LogLevel.WARN, msg);
    public void Error(string msg) => Write(LogLevel.ERROR, msg);

    public void Write(LogLevel level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss}] [{level}] {message}";
        try
        {
            lock (Gate)
            {
                Rotate();
                File.AppendAllText(_path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Le logging ne doit jamais faire planter l'app.
        }
    }

    private void Rotate()
    {
        var fi = new FileInfo(_path);
        if (!fi.Exists || fi.Length < MaxBytes) return;

        var archiveDir = Path.Combine(Path.GetDirectoryName(_path)!, "archive");
        Directory.CreateDirectory(archiveDir);
        var dest = Path.Combine(archiveDir, $"app_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        File.Move(_path, dest, overwrite: true);
    }
}
