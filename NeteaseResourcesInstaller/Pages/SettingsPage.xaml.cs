using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using Newtonsoft.Json;
using System.Threading.Tasks; // 引入 Task
using System.Linq;
using System.Collections.Generic;
using System;

namespace NeteaseResourcesInstaller.Pages;

public partial class SettingsPage : Page, INotifyPropertyChanged
{
    #region Var
    public static readonly string ConfigFolder = Path.Combine(Var.CurrentPath, "config");
    private ObservableCollection<Tuple<string, string>> _canSelectBedrockVersion = new ObservableCollection<Tuple<string, string>>();

    public ObservableCollection<Tuple<string, string>> canSelectBedrockVersion
    {
        get => _canSelectBedrockVersion;
        set
        {
            _canSelectBedrockVersion = value;
            OnPropertyChanged();
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
            OnPropertyChanged(); // 建议加上
        }
    }

    public static string pBedrockPath = string.Empty;
    public static string selectBedrockFolder = string.Empty;
    public static bool deleteDuplicate = false;

    #endregion
    
    #region Application with xaml
    
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region function
    
    public static bool VerifyMinecraftWindowsFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return false;
        if (!File.Exists(Path.Combine(folderPath, "Minecraft.Windows.exe"))) return false;
        if (!Directory.Exists(Path.Combine(folderPath, "data", "resource_packs", "vanilla_netease"))) return false;
        return true;
    }

    // 【修改点1】将目录扫描改为异步方法，放入后台线程执行
    public async Task GetMinecraftVersionsAsync(string folderPath)
    {
        // 先在 UI 线程清空下拉框并禁用，防止用户在扫描时误操作
        ComboBoxSelectBedrockVersion.ItemsSource = null;
        ComboBoxSelectBedrockVersion.IsEnabled = false;

        try
        {
            // Task.Run 将耗时的磁盘扫描放到后台线程，不会卡死 UI
            var versions = await Task.Run(() =>
            {
                List<string> validVersions = new List<string>();
                if (!Directory.Exists(folderPath)) return validVersions;

                // 可能会遇到权限异常，加上 try-catch
                try
                {
                    string[] subDirectories = Directory.GetDirectories(folderPath);
                    foreach (string subDirectory in subDirectories)
                    {
                        if (VerifyMinecraftWindowsFolder(subDirectory))
                        {
                            validVersions.Add(Path.GetFileName(subDirectory));
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // 忽略没有权限访问的文件夹
                }
                return validVersions;
            });

            // 扫描完成后，自动回到 UI 线程更新控件
            ComboBoxSelectBedrockVersion.ItemsSource = versions;
        }
        finally
        {
            ComboBoxSelectBedrockVersion.IsEnabled = true;
        }
    }
    
    public static long GetCurrentTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // 【修改点2】将配置读取也改为异步，防止文件读取卡顿
    public async Task<JsonConfig> ReadConfigAsync()
    {
        string configPath = Path.Combine(ConfigFolder, "settings.json");
        if (!File.Exists(configPath)) return null;

        try
        {
            string fileContent = await Task.Run(() => File.ReadAllText(configPath));
            JsonConfig jJsonConfig = JsonConvert.DeserializeObject<JsonConfig>(fileContent);
            
            // 下面的校验逻辑最好也放到后台执行，因为有 File/Directory 操作
            return await Task.Run(() => 
            {
                string checkPath = Path.Combine(jJsonConfig.bedrockPath, jJsonConfig.selectedBedrockVersion ?? "");
                if (!VerifyMinecraftWindowsFolder(checkPath)) throw new Exception("目录校验失败");

                bool isChannel = bedrockList.Any(x => x.Item1.Equals(jJsonConfig.channel));
                if (!isChannel) throw new Exception("渠道校验失败");

                if (jJsonConfig.channel != "自定义路径")
                {
                    string channelToFolderName = bedrockList.FirstOrDefault(x => x.Item1.Equals(jJsonConfig.channel))?.Item2;
                    if (channelToFolderName == null) throw new Exception("渠道校验失败");
                    
                    using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\Netease\\{channelToFolderName}"))
                    {
                        object value = subKey?.GetValue("MinecraftBENeteasePath");
                        if (value != null) jJsonConfig.bedrockPath = value.ToString();
                        else jJsonConfig.channel = "custom";
                    }
                }
                return jJsonConfig;
            });
        }
        catch (Exception e)
        {
            string backupFileName = $"settings.json_{GetCurrentTimestamp()}.bak";
            string backupPath = Path.Combine(ConfigFolder, backupFileName);
            
            await Task.Run(() => 
            {
                File.Copy(configPath, backupPath, true);
                File.Delete(configPath);
            });

            // 回到 UI 线程再弹窗
            Function.ShowDialog($"我们在处理你的json文件时发生错误(已备份为:{backupFileName}):\n{e.Message}", "错误");
            return null;
        }
    }

    public async Task InitConfigAsync()
    {
        if (!Directory.Exists(ConfigFolder))
        {
            Directory.CreateDirectory(ConfigFolder);
        }

        JsonConfig currentConfig = await ReadConfigAsync();
        if (currentConfig != null)
        {
            ComboBoxSelectBedrockPath.SelectedItem = bedrockList.FirstOrDefault(x => x.Item1 == currentConfig.channel);
            bedrockPath = currentConfig.bedrockPath;
            
            await GetMinecraftVersionsAsync(bedrockPath); // 异步获取版本
            
            ComboBoxSelectBedrockVersion.SelectedItem = currentConfig.selectedBedrockVersion;
            selectBedrockFolder = currentConfig.selectedBedrockVersion;
            deleteDuplicate = currentConfig.deleteDuplicate;
            CheckBoxDeleteDuplicate.IsChecked = currentConfig.deleteDuplicate;
        }
    }

    #endregion

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = this;

        // 【修改点3】不要在构造函数中执行耗时操作，改为在 Page_Loaded 事件中执行
        this.Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        // 确保 Loaded 逻辑只执行一次
        this.Loaded -= SettingsPage_Loaded;

        // 注册表读取非常快，可以在此保留，但复杂 IO 需要等待
        foreach (Tuple<string, string> bedrockTest in bedrockList)
        {
            if (bedrockTest.Item2.Equals("Custom"))
            {
                canSelectBedrockVersion.Add(bedrockTest);
                continue;
            }
            using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\Netease\\{bedrockTest.Item2}"))
            {
                object value = subKey?.GetValue("MinecraftBENeteasePath");
                if (value != null)
                {
                    canSelectBedrockVersion.Add(new Tuple<string, string>(bedrockTest.Item1, value.ToString()));
                }
            }
        }
        if (ComboBoxSelectBedrockPath.Items.Count > 0)
            ComboBoxSelectBedrockPath.SelectedIndex = 0;

        await InitConfigAsync(); // 异步初始化配置
    }

    // 【修改点4】事件处理器改为 async void
    private async void SelectBedrockPath_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboBoxSelectBedrockPath.SelectedItem is Tuple<string, string> selected)
        {
            string selectedBedrockPath = selected.Item2;
            if (selectedBedrockPath.Equals("Custom"))
            {
                SettingsCardCustomBedrockPath.IsEnabled = true;
            }
            else
            {
                bedrockPath = selectedBedrockPath;
                SettingsCardCustomBedrockPath.IsEnabled = false;
                await GetMinecraftVersionsAsync(selectedBedrockPath); // 异步调用
            }
        }
    }

    private async void SelectFolder_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog openFolderDialog = new OpenFolderDialog()
        {
            Title = "选择基岩版文件夹"
        };
        if (openFolderDialog.ShowDialog() == true)
        {
            string selectFolderName = openFolderDialog.FolderName;
            bedrockPath = selectFolderName;
            await GetMinecraftVersionsAsync(selectFolderName); // 异步调用
        }
    }

    private async void SaveConfig_Onclick(object sender, RoutedEventArgs e)
    {
        if (ComboBoxSelectBedrockVersion.SelectedIndex == -1)
        {
            Function.ShowDialog("你尚未选择基岩版版本","错误");
            return;
        }
        
        if (!Directory.Exists(ConfigFolder)) Directory.CreateDirectory(ConfigFolder);
        
        deleteDuplicate = CheckBoxDeleteDuplicate.IsChecked ?? false;
        string selectVersion = ComboBoxSelectBedrockVersion.SelectedItem.ToString();
        string channel = (ComboBoxSelectBedrockPath.SelectedItem as Tuple<string, string>)?.Item1 ?? "自定义路径";

        string json = JsonConvert.SerializeObject(new JsonConfig()
        {
            bedrockPath = bedrockPath,
            selectedBedrockVersion = selectVersion,
            channel = channel,
            deleteDuplicate = deleteDuplicate
        });

        // 异步写入文件
        await Task.Run(() => File.WriteAllText(Path.Combine(ConfigFolder, "settings.json"), json));
        
        await GetMinecraftVersionsAsync(bedrockPath); // 异步刷新列表
        
        selectBedrockFolder = selectVersion;
        Function.ShowDialog($"保存设置成功!\n - 你选择的基岩版路径: {Path.Combine(bedrockPath, selectVersion)}");
    }
}