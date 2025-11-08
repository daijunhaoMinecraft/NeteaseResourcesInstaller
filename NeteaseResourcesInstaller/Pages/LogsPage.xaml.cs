using iNKORE.UI.WPF.Modern.Controls;

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
    }
}