using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using iNKORE.UI.WPF.Modern.Controls;
using NeteaseResourcesInstaller.Pages;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace NeteaseResourcesInstaller;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    #region Pages Var

    public Pages.HomePage Page_Home = new();
    public Pages.SettingsPage Page_Settings = new();
    public Pages.InstallBedrockResources Page_InstallBedrockResources = new();
    public Pages.LogsPage Page_Logs = new();

    #endregion
    
    public MainWindow()
    {
        InitializeComponent();
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var item = sender.SelectedItem;
        Page? page = null;

        if (item == NavigationViewItem_Home)
        {
            page = Page_Home;
        }
        else if (item == NavigationViewItem_ResourcesInstall)
        {
            page = Page_InstallBedrockResources;
        }
        else if (item == NavigationViewItem_Logs)
        {
            page = Page_Logs;
        }
        else if (args.IsSettingsSelected)
        {
            page = Page_Settings;
        }

        if(page != null)
        {
            NavigationView_Root.Header = page.Title;
            Frame_Main.Navigate(page);
        }
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        NavigationView_Root.SelectedItem = NavigationViewItem_Home;
        //throw new NotImplementedException();
    }
}