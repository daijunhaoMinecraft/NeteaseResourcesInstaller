using iNKORE.UI.WPF.Modern.Controls;
using NeteaseResourcesInstaller.Pages;

namespace NeteaseResourcesInstaller;

public class Function
{
    public static void ShowDialog(string content, string title = "信息")
    {
        ContentDialog errorDialog = new ContentDialog()
        {
            Title = title,
            Content = content,
            CloseButtonText = "确定"
        };
        errorDialog.ShowAsync();
    }

    public static void AddLog(string message)
    {
        var stackTrace = new System.Diagnostics.StackTrace();
        var callingMethod = stackTrace.GetFrame(1)?.GetMethod();
        string name = callingMethod?.Name ?? "Unknown";
        // 确保 LogsPage.ViewModel 已初始化
        if (LogsPage.ViewModel == null)
        {
            LogsPage.InitializeViewModel();
        }
        LogsPage.ViewModel.Logs += LogsPage.ViewModel.Logs.Length == 0 ? "" : "\n";
        LogsPage.ViewModel.Logs += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss} {name}] {message}";
    }
}