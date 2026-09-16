using Microsoft.Win32;

namespace KukaManager.Services;

public static class FileDialogService
{
    public static string? Open(string title, string filter)
    {
        var d = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true, Multiselect = false };
        return d.ShowDialog() == true ? d.FileName : null;
    }

    public static string? Save(string title, string filter, string fileName)
    {
        var d = new SaveFileDialog { Title = title, Filter = filter, FileName = fileName, AddExtension = true, OverwritePrompt = true };
        return d.ShowDialog() == true ? d.FileName : null;
    }
}
