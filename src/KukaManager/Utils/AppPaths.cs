namespace KukaManager.Utils;

public static class AppPaths
{
    public const string AppName = "酷咔管理系统";
    public const string Version = "1.3.0";

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KukaManager");

    public static string DatabasePath => Path.Combine(DataDirectory, "kuka.sqlite3");
    public static string BackupDirectory => Path.Combine(DataDirectory, "backups");
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
