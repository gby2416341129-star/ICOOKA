using KukaManager.Utils;

namespace KukaManager.Services;

public sealed class LogService
{
    private readonly object _gate = new();
    public void Info(string message) => Write("INFO", message);
    public void Error(Exception ex, string context = "") => Write("ERROR", (context.Length > 0 ? context + " | " : "") + ex);
    private void Write(string level, string message)
    {
        try
        {
            AppPaths.EnsureDirectories();
            var path = Path.Combine(AppPaths.LogDirectory, $"kuka-{DateTime.Today:yyyyMMdd}.log");
            lock (_gate) File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
