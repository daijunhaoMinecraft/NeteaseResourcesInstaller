using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using System.Diagnostics;
using Newtonsoft.Json;

namespace NeteaseResourcesInstaller.Pages;

public partial class SettingsPage : Page
{
    #region Var
    public static readonly string ConfigFolder = Path.Combine(Var.CurrentPath, "config");
    private ObservableCollection<Tuple<string, string>> _canSelectBedrockVersion =
        new ObservableCollection<Tuple<string, string>>();

    public ObservableCollection<Tuple<string, string>> canSelectBedrockVersion
    {
        get => _canSelectBedrockVersion;
        set
        {
            _canSelectBedrockVersion = value;
            OnPropertyChanged(nameof(canSelectBedrockVersion));
        }
    }
    public ObservableCollection<Tuple<string, string>> bedrockList = new ObservableCollection<Tuple<string, string>>()
    {
        new Tuple<string, string>("网易版", "MCLauncher"),
        new Tuple<string, string>("4399版", "PC4399_MCLauncher"),
        new Tuple<string, string>("自定义路径", "Custom")
    };

    private string _bedrockPath = string.Empty;
    public string bedrockPath
    {
        get => _bedrockPath;
        set
        {
            _bedrockPath = value;
            pBedrockPath = value;
            TextBoxSelectBedrockPath.Text = value;
        }
    }

    public static string pBedrockPath = string.Empty;
    public static string selectBedrockFolder = string.Empty;

    #endregion
    
    #region Application with xaml
    
    // 实现 INotifyPropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region function
    
    public static bool VerifyMinecraftWindowsFolder(string folderPath)
    {
        // Check
        if (!Directory.Exists(folderPath))
        {
            //ShowDialog("文件夹不存在", "错误");
            return false;
        }
        if (!File.Exists(Path.Combine(folderPath, "Minecraft.Windows.exe")))
        {
            //ShowDialog("未找到文件:Minecraft.Windows.exe", "错误");
            return false;
        }

        if (!Directory.Exists(Path.Combine(folderPath, "data", "resource_packs", "vanilla_netease")))
        {
            //ShowDialog("无法定位到vanilla_netease文件夹", "错误");
            return false;
        }
        return true;
    }

    public void GetMinecraftVersions(string folderPath)
    {
        // 获取文件夹中的子目录文件夹
        string[] subDirectories = Directory.GetDirectories(folderPath);
        List<string> versions = new List<string>();
        foreach (string subDirectory in subDirectories)
        {
            bool isValid = VerifyMinecraftWindowsFolder(subDirectory);
            string version = Path.GetFileName(subDirectory);
            if (isValid)
            {
                versions.Add(version);
            }
        }
        ComboBoxSelectBedrockVersion.ItemsSource = versions;
    }
    
    // 获取当前毫秒级时间戳
    public static long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public JsonConfig ReadConfig()
    {
        string fileContent = File.ReadAllText(Path.Combine(ConfigFolder, "settings.json"));
        try
        {
            // To Json Convert
            JsonConfig jJsonConfig = JsonConvert.DeserializeObject<JsonConfig>(fileContent);
            // check
            string bedrockPath = Path.Combine(jJsonConfig.bedrockPath, jJsonConfig.selectedBedrockVersion);
            // Step 1: check folder and files
            if (!VerifyMinecraftWindowsFolder(bedrockPath))
            {
                throw new Exception("目录校验失败");
            }
            // Step 2: Check Channel
            bool isChannel = bedrockList.Any(x => x.Item1.Equals(jJsonConfig.channel));
            if (!isChannel)
            {
                throw new Exception("渠道校验失败");
            }
            if (jJsonConfig.channel != "自定义路径")
            {
                string channelToFolderName = bedrockList.FirstOrDefault(x => x.Item1.Equals(jJsonConfig.channel)).Item2;
                if (channelToFolderName == null)
                {
                    throw new Exception("渠道校验失败");
                }
                using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\Netease\\{channelToFolderName}"))
                {
                    if (subKey != null)
                    {
                        object value = subKey.GetValue("MinecraftBENeteasePath");
                        if (value != null)
                        {
                            jJsonConfig.bedrockPath = value.ToString();
                        }
                        else
                        {
                            jJsonConfig.channel = "custom";
                        }
                    }
                    else
                    {
                        jJsonConfig.channel = "custom";
                    }
                }
            }
            return JsonConvert.DeserializeObject<JsonConfig>(fileContent);
        }
        catch (Exception e)
        {
            // Backup Old File
            string BackupFileName = $"settings.json_{GetCurrentTimestamp().ToString()}.bak";
            System.IO.File.Copy(Path.Combine(ConfigFolder, "settings.json"), System.IO.Path.Combine(ConfigFolder, $"settings.json_{GetCurrentTimestamp().ToString()}.bak"));
            System.IO.File.Delete(Path.Combine(ConfigFolder, "settings.json"));
            Function.ShowDialog($"我们在处理你的json文件时发生错误(我们已将原设置备份并且重新初始化了设置配置,文件名:{BackupFileName} in config folder):{e.Message}\n StackTrace: \n{e.StackTrace}", "错误");
            return null;
        }
    }

    public void InitConfig()
    {
        if (!File.Exists(Path.Combine(ConfigFolder, "settings.json")))
        {
            if (!Directory.Exists(ConfigFolder))
            {
                Directory.CreateDirectory(ConfigFolder);
            }
            return;
        }
        JsonConfig currentConfig = ReadConfig();
        if (currentConfig != null)
        {
            ComboBoxSelectBedrockPath.SelectedItem = currentConfig.channel;
            bedrockPath = currentConfig.bedrockPath;
            GetMinecraftVersions(bedrockPath);
            ComboBoxSelectBedrockVersion.SelectedItem = currentConfig.selectedBedrockVersion;
            selectBedrockFolder = currentConfig.selectedBedrockVersion;
        }
    }

    #endregion

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = this;
        foreach (Tuple<string, string> bedrockTest in bedrockList)
        {
            if (bedrockTest.Item2.Equals("Custom"))
            {
                canSelectBedrockVersion.Add(bedrockTest);
                continue;
            }
            using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\Netease\\{bedrockTest.Item2}"))
            {
                if (subKey != null)
                {
                    object value = subKey.GetValue("MinecraftBENeteasePath");
                    if (value != null)
                    {
                        canSelectBedrockVersion.Add(new Tuple<string, string>(bedrockTest.Item1, value.ToString()));
                    }
                }
            }
            ComboBoxSelectBedrockPath.SelectedIndex = 0;
        }

        InitConfig();
    }

    private void SelectBedrockPath_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        string selectedBedrockPath = (ComboBoxSelectBedrockPath.SelectedItem as Tuple<string, string>).Item2;
        if (selectedBedrockPath.Equals("Custom"))
        {
            // TextBoxSelectBedrockPath.IsReadOnly = false;
            //bedrockPath = "";
            SettingsCardCustomBedrockPath.IsEnabled = true;
        }
        else
        {
            //TextBoxSelectBedrockPath.IsReadOnly = true;
            bedrockPath = selectedBedrockPath;
            GetMinecraftVersions(selectedBedrockPath);
            SettingsCardCustomBedrockPath.IsEnabled = false;
        }
    }

    private void SelectFolder_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog openFolderDialog = new OpenFolderDialog()
        {
            Title = "选择基岩版文件夹"
        };
        if (openFolderDialog.ShowDialog() == true)
        {
            string selectFolderName = openFolderDialog.FolderName;
            bedrockPath = selectFolderName;
            GetMinecraftVersions(selectFolderName);
            // 读取文件信息 Minecraft.Windows.exe
            // FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(Path.Combine(selectFolderName, "Minecraft.Windows.exe"));
            // string fileVersion = fileVersionInfo.FileVersion;
            // bedrockPath = selectFolderName;
            //ShowDialog($"成功选择到基岩版文件夹\n - 基岩版版本:{fileVersion}");
        }
    }

    private void SaveConfig_Onclick(object sender, RoutedEventArgs e)
    {
        if (ComboBoxSelectBedrockVersion.SelectedIndex == -1)
        {
            Function.ShowDialog("你尚未选择基岩版版本","错误");
            return;
        }
        if (!Directory.Exists(ConfigFolder))
        {
            Directory.CreateDirectory(ConfigFolder);
        }

        string selectVersion = ComboBoxSelectBedrockVersion.SelectedItem.ToString();
        File.WriteAllText(ConfigFolder + "\\settings.json", JsonConvert.SerializeObject(new JsonConfig()
        {
            bedrockPath = bedrockPath,
            selectedBedrockVersion = selectVersion,
            channel = (ComboBoxSelectBedrockPath.SelectedItem as Tuple<string, string>).Item1
        }));
        GetMinecraftVersions(bedrockPath);
        selectBedrockFolder = selectVersion;
        Function.ShowDialog($"保存设置成功!\n - 你选择的基岩版路径: {Path.Combine(bedrockPath, selectVersion)}");
    }
}