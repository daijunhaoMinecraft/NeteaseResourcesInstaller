using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using iNKORE.UI.WPF.Modern.Controls;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace NeteaseResourcesInstaller.Pages;

public partial class LogsPage : Page
{
    public static LogsPageViewModel ViewModel { get; private set; }
    
    public static void InitializeViewModel()
    {
        if (ViewModel == null)
        {
            ViewModel = new LogsPageViewModel();
        }
    }

    public LogsPage()
    {
        InitializeComponent();
        if (ViewModel == null)
        {
            ViewModel = new LogsPageViewModel();
        }
        DataContext = ViewModel;
        ViewModel.IsInfoBarOpen = false;
        
        // 移除自动滚动功能，避免日志过多时的性能问题
    }
    
    private void ExportLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = $"日志_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            
            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, ViewModel.Logs);
                MessageBox.Show("日志已成功导出！", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出日志时发生错误：{ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show("确定要清空所有日志吗？此操作不可撤销。", "确认清空日志", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            ViewModel.Logs = "";
        }
    }
}