using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;


namespace NeteaseResourcesInstaller.Pages;

public partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        DataContext = this;
        Notify.Text = "测试版本(不接入公告API)";
    }
}