using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace NeteaseResourcesInstaller.Pages;

public class ResourceItem
{
    public string Title { get; set; }
    public string FolderName { get; set; }
    public BitmapImage Thumbnail { get; set; }
    public string Description { get; set; }
}

public partial class InstallBedrockResources : Page
{
    #region Var

    // private ObservableCollection<ResourceItem> _resourceItems = new ObservableCollection<ResourceItem>();
    // public ObservableCollection<ResourceItem> ResourceItems
    // {
    //     get { return _resourceItems; }
    //     set { _resourceItems = value; OnPropertyChanged(nameof(ResourceItems)); }
    // }
    private ObservableCollection<ResourceItem> _resourceItems = new ObservableCollection<ResourceItem>();
    public static string ResourcePath = string.Empty;

    #endregion

    #region function

    public static BitmapImage BytesToBitmapImage(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
            return null;

        using (var memoryStream = new MemoryStream(imageBytes))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = memoryStream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze(); // 冻结以提高性能并使其跨线程可用
            return bitmap;
        }
    }
    private void ExtractMcpackFile(string zipFilePath, string extractPath)
    {
        using (var archive = ZipFile.OpenRead(zipFilePath))
        {
            // 获取根目录下的所有条目
            var rootEntries = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.FullName) && e.FullName.IndexOf('/') == e.FullName.LastIndexOf('/'))
                .Select(e => e.FullName.Split('/')[0])
                .Distinct()
                .ToList();

            // 如果只有一个根文件夹
            if (rootEntries.Count == 1 && archive.Entries.All(e => e.FullName.StartsWith(rootEntries[0] + "/")))
            {
                // 只解压该文件夹的内容（跳过根文件夹）
                string rootFolderName = rootEntries[0] + "/";
                
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith(rootFolderName))
                    {
                        // 移除根文件夹名称
                        string relativePath = entry.FullName.Substring(rootFolderName.Length);
                        if (string.IsNullOrEmpty(relativePath)) continue;
                        
                        string destinationPath = Path.Combine(extractPath, relativePath);
                        
                        if (entry.FullName.EndsWith("/"))
                        {
                            // 创建目录
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            // 创建目录（如果不存在）
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            // 解压文件
                            entry.ExtractToFile(destinationPath, true);
                        }
                    }
                }
            }
            else
            {
                // 正常解压所有内容
                Directory.CreateDirectory(extractPath);
                ZipFile.ExtractToDirectory(zipFilePath, extractPath);
            }
        }
    }
    private BitmapImage LoadDefaultThumbnail()
    {
        try 
        {
            // 方式1：从资源文件加载
            Uri resourceUri = new Uri("pack://application:,,,/Resources/pack.png");
            BitmapImage image = new BitmapImage(resourceUri);
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            // 处理加载失败情况
            Console.WriteLine($"Failed to load image: {ex.Message}");
        }
        return null;
    }

    private void LoadResources()
    {
        try
        {
            // Extract File
            if (!Directory.Exists(ResourcePath))
            {
                Function.AddLog("创建文件夹: ExtResources");
                Directory.CreateDirectory(ResourcePath);
                return;
            }
            Function.AddLog("读取已经解压好的资源..");
            // 获取文件夹列表
            string[] resourceFolders = Directory.GetDirectories(ResourcePath);
            foreach (string currectResourcePath in resourceFolders)
            {
                Function.AddLog($"读取资源: {currectResourcePath}");
                Function.AddLog("检查是否存在 manifest.json 文件");
                if (!File.Exists(Path.Combine(currectResourcePath, "manifest.json")))
                {
                    throw new Exception("不是有效的资源包: manifest.json不存在");
                }
                JObject jManifest = JObject.Parse(File.ReadAllText(Path.Combine(currectResourcePath, "manifest.json")));
                Function.AddLog("读取资源包Header信息");
                string name = jManifest["header"]["name"].ToString();
                string description = jManifest["header"]["description"].ToString();
                Function.AddLog($"资源包名称: {name}");
                Function.AddLog($"资源包描述: {description}");
                // contents.json to empty
                File.WriteAllText(Path.Combine(currectResourcePath, "contents.json"), "{}");
                if (Directory.Exists(Path.Combine(currectResourcePath, "ui")))
                {
                    File.WriteAllText(Path.Combine(currectResourcePath,"ui" , "contents.json"), "{}");
                }
                var newItem = new ResourceItem
                {
                    Title = name,
                    FolderName = Path.GetFileName(currectResourcePath),
                    Thumbnail = File.Exists(Path.Combine(currectResourcePath, "pack_icon.png")) ? BytesToBitmapImage(File.ReadAllBytes(Path.Combine(currectResourcePath, "pack_icon.png"))) : LoadDefaultThumbnail(),
                    Description = description
                };
                _resourceItems.Add(newItem);
            }
        }
        catch (Exception e)
        {
            Function.AddLog($"读取资源包时出现错误:{e.Message}\nStackTrac: {e.StackTrace}");
            Function.ShowDialog($"读取资源包时出现错误:{e.Message}", "错误");
        }
    }



    #endregion

    public InstallBedrockResources()
    {
        InitializeComponent();
        DataContext = this;
        ResourcesList.ItemsSource = _resourceItems;
        ResourcePath = Path.Combine(Var.CurrentPath, "ExtResources");
        LoadResources();
    }

    private void LoadNewResource_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Creat Dialog
            FileDialog selectMcpackDialog = new OpenFileDialog()
            {
                Title = "选择mcpack文件",
                Filter = "(*.mcpack)|*.mcpack"
            };
            if (selectMcpackDialog.ShowDialog() == true)
            {
                Task.Run(() =>
                {
                    // Extract File
                    if (!Directory.Exists(ResourcePath))
                    {
                        Function.AddLog("创建文件夹: ExtResources");
                        Directory.CreateDirectory(ResourcePath);
                    }

                    string currectResourcePath = Path.Combine(ResourcePath,
                        Path.GetFileNameWithoutExtension(selectMcpackDialog.FileName));
                    Function.AddLog($"解压资源: {selectMcpackDialog.FileName}");
                
                    // 检查ZIP文件结构并解压
                    ExtractMcpackFile(selectMcpackDialog.FileName, currectResourcePath);
                
                    Function.AddLog("解压完毕!");
                    Function.AddLog("检查是否存在 manifest.json 文件");
                    if (!File.Exists(Path.Combine(currectResourcePath, "manifest.json")))
                    {
                        throw new Exception("不是有效的资源包: manifest.json不存在");
                    }
                    JObject jManifest = JObject.Parse(File.ReadAllText(Path.Combine(currectResourcePath, "manifest.json")));
                    Function.AddLog("读取资源包Header信息");
                    string name = jManifest["header"]["name"].ToString();
                    string description = jManifest["header"]["description"].ToString();
                    Function.AddLog($"资源包名称: {name}");
                    Function.AddLog($"资源包描述: {description}");
                    // contents.json to empty
                    File.WriteAllText(Path.Combine(currectResourcePath, "contents.json"), "{}");
                    if (Directory.Exists(Path.Combine(currectResourcePath, "ui")))
                    {
                        File.WriteAllText(Path.Combine(currectResourcePath,"ui" , "contents.json"), "{}");
                    }
                    
                    // 使用Dispatcher更新UI
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var newItem = new ResourceItem
                        {
                            Title = name,
                            FolderName = Path.GetFileName(currectResourcePath),
                            Thumbnail = File.Exists(Path.Combine(currectResourcePath, "pack_icon.png")) ? BytesToBitmapImage(File.ReadAllBytes(Path.Combine(currectResourcePath, "pack_icon.png"))) : LoadDefaultThumbnail(),
                            Description = description
                        };
                        _resourceItems.Add(newItem);
                    });
                });
            }
        }
        catch (Exception exception)
        {
            Function.ShowDialog($"处理资源包时出现错误,{exception.Message}", "错误");
        }
        // // 示例：添加一个测试项（实际可从文件导入）
        // var newItem = new ResourceItem
        // {
        //     Title = $"材质 {_resourceItems.Count + 1}",
        //     Thumbnail = BytesToBitmapImage(File.ReadAllBytes("C:\\Users\\Administrator\\Desktop\\pack.png")),
        //     Description = "这是一个示例材质包描述。"
        // };
        // _resourceItems.Add(newItem);
    }

    private void DeleteResource_OnClick(object sender, RoutedEventArgs e)
    {
        var toRemove = ResourcesList.SelectedItems.Cast<ResourceItem>().ToList();
        if (toRemove.Count == 0)
        {
            Function.ShowDialog("请选择要删除的资源包", "提示");
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            $"确定要删除选中的 {toRemove.Count} 个资源包吗？此操作不可撤销。",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.No)
        {
            return;
        }

        foreach (var item in toRemove)
        {
            // 获取要删除的资源包路径
            string resourcePath = Path.Combine(ResourcePath, item.FolderName);
            // 删除文件夹中的所有内容
            if (Directory.Exists(resourcePath))
            {
                try
                {
                    Directory.Delete(resourcePath, true);
                    Function.AddLog($"已删除资源包: {item.Title}");
                }
                catch (Exception ex)
                {
                    Function.AddLog($"删除资源包 {item.Title} 时出错: {ex.Message}");
                    Function.ShowDialog($"删除资源包 {item.Title} 时出错: {ex.Message}", "错误");
                    continue; // 继续删除其他选中的资源包
                }
            }
            
            // 从列表中移除
            _resourceItems.Remove(item);
        }
        
        Function.AddLog($"成功删除 {toRemove.Count} 个资源包");
    }

    private void ResourceUp_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelectedBlocks(up: true);
    }

    private void ResourceDown_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelectedBlocks(up: false);
    }
    private void MoveSelectedBlocks(bool up)
    {
        if (ResourcesList.SelectedItems.Count == 0) return;

        var selectedItems = ResourcesList.SelectedItems.Cast<ResourceItem>().ToList();
        var indices = selectedItems.Select(item => _resourceItems.IndexOf(item))
            .Where(i => i >= 0)
            .OrderBy(i => i)
            .ToList();

        if (indices.Count == 0) return;

        // 分割连续块
        var blocks = new List<List<int>>();
        var current = new List<int> { indices[0] };
        for (int i = 1; i < indices.Count; i++)
        {
            if (indices[i] == indices[i - 1] + 1)
                current.Add(indices[i]);
            else
            {
                blocks.Add(current);
                current = new List<int> { indices[i] };
            }
        }

        blocks.Add(current);

        var list = _resourceItems.ToList();
        bool moved = false;

        foreach (var block in blocks)
        {
            int first = block[0];
            int last = block[^1];
            var blockItems = block.Select(i => list[i]).ToList();

            if (up)
            {
                if (first == 0) continue;
                // 上移：插入到 first - 1
                foreach (int i in block.OrderByDescending(x => x))
                    list.RemoveAt(i);
                list.InsertRange(first - 1, blockItems);
                moved = true;
            }
            else
            {
                if (last == list.Count - 1) continue;
                // 下移：插入到 last + 2 - block.Count
                int insertIndex = last + 2 - block.Count;
                foreach (int i in block.OrderByDescending(x => x))
                    list.RemoveAt(i);
                list.InsertRange(insertIndex, blockItems);
                moved = true;
            }
        }

        if (moved)
        {
            _resourceItems.Clear();
            foreach (var item in list)
                _resourceItems.Add(item);

            ResourcesList.SelectedItems.Clear();
            foreach (var item in selectedItems)
                ResourcesList.SelectedItems.Add(item);
        }
    }

    private void ContentGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 可用于启用/禁用按钮（例如：无选择时禁用删除/移动）
        bool hasSelection = ResourcesList.SelectedItems.Count > 0;
        // 例如：
        // DeleteButton.IsEnabled = hasSelection;
        // UpButton.IsEnabled = hasSelection && _resourceItems.IndexOf(ResourcesList.SelectedItems[0] as ResourceItem) > 0;
    }

    // private void GetMaterialList(object sender, RoutedEventArgs e)
    // {
    //     // Console.WriteLine(SettingsPage.pBedrockPath.ToString());
    //     // throw new NotImplementedException();
    //     if (SettingsPage.pBedrockPath == string.Empty)
    //     {
    //         Function.ShowDialog("你尚未设置基岩版路径,请你前往设置去设置基岩版路径", "错误");
    //         // 获取当前应用程序的主窗口
    //         if (Application.Current.MainWindow is MainWindow mainWindow)
    //         {
    //             // 导航到设置页面
    //             mainWindow.NavigationView_Root.SelectedItem = mainWindow.NavigationView_Root.SettingsItem;
    //         }
    //
    //         return;
    //     }
    //
    //     string currentNeteaseVanillaPath = Path.Combine(SettingsPage.pBedrockPath, SettingsPage.selectBedrockFolder,
    //         "data", "resource_packs", "vanilla_netease");
    //     if (!Directory.Exists(currentNeteaseVanillaPath))
    //     {
    //         Function.ShowDialog("基岩版文件夹不存在,请前往设置去设置基岩版路径", "错误");
    //         // 获取当前应用程序的主窗口
    //         if (Application.Current.MainWindow is MainWindow mainWindow)
    //         {
    //             // 导航到设置页面
    //             mainWindow.NavigationView_Root.SelectedItem = mainWindow.NavigationView_Root.SettingsItem;
    //         }
    //
    //         return;
    //     }
    //
    //     string[] subDirectories = Directory.GetDirectories(currentNeteaseVanillaPath);
    //     List<string> folderFileNames = subDirectories.Select(x => Path.GetFileName(x)).ToList();
    //     if (!folderFileNames.Contains("ResourceConfig"))
    //     {
    //         Function.ShowDialog("无法找到材质包配置文件,你可能没有安装材质包");
    //     }
    // }

    #region Application with xaml

    // 实现 INotifyPropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    private void InstallResource_Onclick(object sender, RoutedEventArgs e)
    {
        // 自动导航到日志页面
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigationView_Root.SelectedItem = mainWindow.NavigationViewItem_Logs;
        }

        // 在新线程中执行安装操作
        Task.Run(() =>
        {
            try
            {
                if (SettingsPage.pBedrockPath == string.Empty)
                {
                    Function.AddLog("你尚未设置基岩版路径，请前往设置页面进行设置");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Function.ShowDialog("你尚未设置基岩版路径，请前往设置页面进行设置", "错误");
                        // 获取当前应用程序的主窗口
                        if (Application.Current.MainWindow is MainWindow mainWindow)
                        {
                            // 导航到设置页面
                            mainWindow.NavigationView_Root.SelectedItem = mainWindow.NavigationView_Root.SettingsItem;
                        }
                    });
                    return;
                }

                string vanillaResourcePath = Path.Combine(SettingsPage.pBedrockPath, SettingsPage.selectBedrockFolder, "data", "resource_packs", "vanilla_netease");
                if (!Directory.Exists(vanillaResourcePath))
                {
                    Function.AddLog("基岩版原版资源包目录不存在，请检查设置是否正确");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Function.ShowDialog("基岩版原版资源包目录不存在，请检查设置是否正确", "错误");
                    });
                    return;
                }

                // 检查是否需要还原操作
                string installedFlagPath = Path.Combine(vanillaResourcePath, "InstalledResources");
                string resourceConfigPath = Path.Combine(vanillaResourcePath, "ResourceConfig");
                bool needRestore = File.Exists(installedFlagPath) || Directory.Exists(resourceConfigPath);
                if (needRestore)
                {
                    Function.AddLog("检测到已安装的资源包，正在执行还原操作...");
                    RestoreOriginalFiles(vanillaResourcePath, resourceConfigPath);
                }
                else
                {
                    Function.AddLog("未检测到已安装的资源包，直接开始安装...");
                }

                // 执行安装操作
                InstallSelectedResources(vanillaResourcePath);
            }
            catch (Exception ex)
            {
                Function.AddLog($"安装过程中发生错误: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Function.ShowDialog($"安装过程中发生错误: {ex.Message}", "错误");
                });
            }
        });
    }

    /// <summary>
    /// 还原原始文件
    /// </summary>
    /// <param name="vanillaPath">原版资源路径</param>
    /// <param name="configPath">资源配置目录路径</param>
    private void RestoreOriginalFiles(string vanillaPath, string configPath)
    {
        try
        {
            string restoreJsonPath = Path.Combine(configPath, "Restore.json");
            if (File.Exists(restoreJsonPath))
            {
                string restoreJsonContent = File.ReadAllText(restoreJsonPath);
                JObject restoreData = JObject.Parse(restoreJsonContent);
                JArray addFiles = (JArray)restoreData["AddFile"];
                JObject fileMappings = (JObject)restoreData["FileMappings"];

                // 还原被修改的文件
                foreach (var mapping in fileMappings)
                {
                    string changedFile = mapping.Key;
                    string originalFileBackup = mapping.Value.ToString();
                    if (originalFileBackup == string.Empty)
                    {
                        addFiles.Add(changedFile);
                        continue;
                    }

                    string backupFilePath = Path.Combine(configPath, "OriginalFiles", originalFileBackup);
                    string targetFilePath = Path.Combine(vanillaPath, changedFile);

                    // 如果备份文件存在，则还原它
                    if (File.Exists(backupFilePath))
                    {
                        // 确保目标目录存在
                        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath));
                        File.Copy(backupFilePath, targetFilePath, true);
                        Function.AddLog($"已还原文件: {changedFile}");
                    }
                }

                // 删除添加的文件
                foreach (string addedFile in addFiles)
                {
                    string filePath = Path.Combine(vanillaPath, addedFile);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        Function.AddLog($"已删除添加的文件: {addedFile}");
                        
                        // 删除空目录
                        TryDeleteEmptyDirectories(Path.GetDirectoryName(filePath), vanillaPath);
                    }
                }
            }

            // 删除整个ResourceConfig目录
            if (Directory.Exists(configPath))
            {
                Directory.Delete(configPath, true);
                Function.AddLog("已删除ResourceConfig目录");
            }

            // 删除安装标记文件
            string installedFlagPath = Path.Combine(vanillaPath, "InstalledResources");
            if (File.Exists(installedFlagPath))
            {
                File.Delete(installedFlagPath);
                Function.AddLog("已删除安装标记文件");
            }
        }
        catch (Exception ex)
        {
            Function.AddLog($"还原过程中发生错误: {ex.Message}");
            Function.ShowDialog($"还原过程中发生错误: {ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 尝试删除空目录
    /// </summary>
    /// <param name="dirPath">要检查的目录路径</param>
    /// <param name="basePath">基础路径，防止删除基础目录</param>
    private void TryDeleteEmptyDirectories(string dirPath, string basePath)
    {
        if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath) || dirPath.Equals(basePath, StringComparison.OrdinalIgnoreCase))
            return;

        // 检查目录是否为空
        if (Directory.GetFileSystemEntries(dirPath).Length == 0)
        {
            try
            {
                Directory.Delete(dirPath);
                Function.AddLog($"已删除空目录: {dirPath}");
                
                // 递归检查父目录
                TryDeleteEmptyDirectories(Path.GetDirectoryName(dirPath), basePath);
            }
            catch (Exception ex)
            {
                Function.AddLog($"删除空目录 {dirPath} 时出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 安装选中的资源包
    /// </summary>
    /// <param name="vanillaPath">原版资源路径</param>
    private void InstallSelectedResources(string vanillaPath)
    {
        try
        {
            // 在UI线程上获取选中的项目
            var selectedItems = Application.Current.Dispatcher.Invoke(() =>
            {
                return ResourcesList.SelectedItems.Cast<ResourceItem>().ToList();
            });
            
            if (selectedItems.Count == 0)
            {
                Function.AddLog("请选择要安装的资源包");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Function.ShowDialog("请选择要安装的资源包", "提示");
                });
                return;
            }

            // 创建ResourceConfig目录结构
            string resourceConfigDir = Path.Combine(vanillaPath, "ResourceConfig");
            string originalFilesDir = Path.Combine(resourceConfigDir, "OriginalFiles");
            
            // 如果目录已存在，先删除
            if (Directory.Exists(resourceConfigDir))
            {
                Directory.Delete(resourceConfigDir, true);
            }
            
            Directory.CreateDirectory(resourceConfigDir);
            Directory.CreateDirectory(originalFilesDir);

            JObject restoreData = new JObject();
            JArray addFiles = new JArray();
            JObject fileMappings = new JObject();

            // 直接遍历 ExtResources 目录下的所有文件夹（资源包）
            // string[] resourceFolders = Directory.GetDirectories(ResourcePath);
            foreach (var resourceFolder in selectedItems)
            {
                string resourceName = resourceFolder.FolderName;
                
                // 检查该资源包是否在选中列表中
                bool isSelected = selectedItems.Any(item => item.FolderName == resourceName);
                if (!isSelected)
                {
                    continue; // 跳过未选中的资源包
                }
                
                if (!Directory.Exists(Path.Combine(ResourcePath, resourceFolder.FolderName)))
                {
                    Function.AddLog($"资源包目录不存在: {resourceFolder}");
                    continue;
                }

                Function.AddLog($"正在安装资源包: {resourceName}");
                ProcessResourceDirectory(Path.Combine(ResourcePath, resourceFolder.FolderName), vanillaPath, resourceConfigDir, originalFilesDir, fileMappings, addFiles);
            }

            // 保存Restore.json文件
            restoreData["AddFile"] = addFiles;
            restoreData["FileMappings"] = fileMappings;
            string restoreJsonPath = Path.Combine(resourceConfigDir, "Restore.json");
            File.WriteAllText(restoreJsonPath, restoreData.ToString());

            // 创建安装标记文件
            string installedFlagPath = Path.Combine(vanillaPath, "InstalledResources");
            File.Create(installedFlagPath).Dispose();

            Function.AddLog("资源包安装完成");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Function.ShowDialog("资源包安装完成", "提示");
            });
        }
        catch (Exception ex)
        {
            Function.AddLog($"安装过程中发生错误: {ex.Message}");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Function.ShowDialog($"安装过程中发生错误: {ex.Message}", "错误");
            });
        }
    }

    /// <summary>
    /// 处理资源包目录中的文件
    /// </summary>
    /// <param name="resourcePath">资源包路径</param>
    /// <param name="vanillaPath">原版资源路径</param>
    /// <param name="resourceConfigDir">资源配置目录</param>
    /// <param name="originalFilesDir">原始文件备份目录</param>
    /// <param name="fileMappings">文件映射关系</param>
    /// <param name="addFiles">新增文件列表</param>
    private void ProcessResourceDirectory(string resourcePath, string vanillaPath, string resourceConfigDir, string originalFilesDir, JObject fileMappings, JArray addFiles)
    {
        string[] files = Directory.GetFiles(resourcePath, "*.*", SearchOption.AllDirectories);
        
        foreach (string file in files)
        {
            try
            {
                string relativePath = file.Substring(resourcePath.Length + 1); // 获取相对路径
                string targetPath = Path.Combine(vanillaPath, relativePath);
                string targetDir = Path.GetDirectoryName(targetPath);

                // 跳过manifest.json文件
                if (Path.GetFileName(file).ToLower() == "manifest.json")
                {
                    Function.AddLog($"跳过manifest.json文件: {relativePath}");
                    continue;
                }

                // 确保目标目录存在
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // 检查是否为JSON文件
                if (Path.GetExtension(file).ToLower() == ".json")
                {
                    MergeJsonFiles(file, targetPath, relativePath, resourceConfigDir, originalFilesDir, fileMappings);
                }
                // 检查是否为Lang文件
                else if (Path.GetExtension(file).ToLower() == ".lang")
                {
                    MergeLangFiles(file, targetPath, relativePath, resourceConfigDir, originalFilesDir, fileMappings);
                }
                else
                {
                    // 对于非JSON文件，直接复制替换
                    // 如果目标文件存在且映射中还没有记录，则备份原文件
                    if (File.Exists(targetPath) && !fileMappings.ContainsKey(relativePath))
                    {
                        string uniqueBackupName = GetUniqueBackupFileName(relativePath, originalFilesDir);
                        string backupPath = Path.Combine(originalFilesDir, uniqueBackupName);
                        
                        File.Copy(targetPath, backupPath, true);
                        fileMappings[relativePath] = uniqueBackupName;
                    }
                    else if (!File.Exists(targetPath))
                    {
                        // 如果是新增文件，记录到AddFile列表
                        addFiles.Add(relativePath);
                    }
                    File.Copy(file, targetPath, true);
                    Function.AddLog($"已复制文件: {relativePath}");
                }
            }
            catch (Exception ex)
            {
                Function.AddLog($"处理文件 {file} 时出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 获取唯一的备份文件名
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <param name="originalFilesDir">原始文件备份目录</param>
    /// <returns>唯一备份文件名</returns>
    private string GetUniqueBackupFileName(string relativePath, string originalFilesDir)
    {
        string fileName = relativePath.Replace("\\", "_").Replace("/", "_");
        string extension = Path.GetExtension(fileName);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        string uniqueFileName = fileName;
        int counter = 1;

        while (File.Exists(Path.Combine(originalFilesDir, uniqueFileName)))
        {
            uniqueFileName = $"{fileNameWithoutExtension}_{counter}{extension}";
            counter++;
        }

        return uniqueFileName;
    }

    /// <summary>
    /// 合并JSON文件
    /// </summary>
    /// <param name="sourceFile">源文件路径</param>
    /// <param name="targetFile">目标文件路径</param>
    /// <param name="relativePath">相对路径</param>
    /// <param name="resourceConfigDir">资源配置目录</param>
    /// <param name="originalFilesDir">原始文件备份目录</param>
    /// <param name="fileMappings">文件映射关系</param>
    private void MergeJsonFiles(string sourceFile, string targetFile, string relativePath, string resourceConfigDir, string originalFilesDir, JObject fileMappings)
    {
        try
        {
            // 读取源文件
            string sourceJsonText = File.ReadAllText(sourceFile);
            // 去除注释
            sourceJsonText = RemoveJsonComments(sourceJsonText);
            JObject sourceJson = JObject.Parse(sourceJsonText);

            // 如果目标文件存在且还没有备份，则备份原文件
            if (File.Exists(targetFile) && !fileMappings.ContainsKey(relativePath))
            {
                string uniqueBackupName = GetUniqueBackupFileName(relativePath, originalFilesDir);
                string backupPath = Path.Combine(originalFilesDir, uniqueBackupName);
                File.Copy(targetFile, backupPath, true);
                fileMappings[relativePath] = uniqueBackupName;
            }
            else if (!File.Exists(targetFile))
            {
                // 如果是新增文件，记录到AddFile列表
                fileMappings[relativePath] = ""; // 占位符，表示新增文件
            }

            // 如果目标文件存在，则合并
            if (File.Exists(targetFile))
            {
                string targetJsonText = File.ReadAllText(targetFile);
                // 去除注释
                targetJsonText = RemoveJsonComments(targetJsonText);
                JObject targetJson = JObject.Parse(targetJsonText);

                // 根据常量判断是否使用单层对比
                bool useSingleLevelMerge = true; // 默认使用深度合并
                
                // 可以根据文件名或路径设置特定文件使用单层合并
                // 例如：useSingleLevelMerge = relativePath.Contains("some_specific_file.json");
                string[] specificFiles = new string[] { "_ui_defs.json", "_global_variables.json" };
                if (specificFiles.Any(f => relativePath.EndsWith(f)))
                {
                    useSingleLevelMerge = false;
                }
                
                if (useSingleLevelMerge)
                {
                    // // 单层对比合并 - 只合并顶层属性
                    foreach (var property in sourceJson.Properties())
                    {
                        targetJson[property.Name] = property.Value.DeepClone();
                    }
                    //targetJson = sourceJson;
                }
                else
                {
                    // 深度合并 - 合并整个JSON结构
                    targetJson.Merge(sourceJson, new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Concat,
                        MergeNullValueHandling = MergeNullValueHandling.Merge,
                        PropertyNameComparison = StringComparison.OrdinalIgnoreCase
                    });
                    // if (relativePath.EndsWith("_ui_defs.json") && targetJson["ui_defs"] != null)
                    // {
                    //     JArray uiDefs = (JArray)targetJson["ui_defs"];
                    //     for (int i = uiDefs.Count - 1; i >= 0; i--)
                    //     {
                    //         string defsUI = uiDefs[i].ToString();
                    //         if (defsUI.StartsWith("ui/netease/"))
                    //         {
                    //             uiDefs.RemoveAt(i);
                    //         }
                    //     }
                    // }
                }
                

                // 写入合并后的结果，使用格式化输出
                File.WriteAllText(targetFile, targetJson.ToString(Newtonsoft.Json.Formatting.Indented));
                if (relativePath.EndsWith("contents.json"))
                {
                    File.Delete(targetFile);
                    Function.AddLog($"已删除字典文件: {targetFile}");
                }
                Function.AddLog($"已合并JSON文件: {relativePath}" + (useSingleLevelMerge ? " (单层合并)" : " (深度合并)"));
            }
            else
            {
                // 目标文件不存在，直接复制
                File.WriteAllText(targetFile, sourceJson.ToString(Newtonsoft.Json.Formatting.Indented));
                Function.AddLog($"已创建JSON文件: {relativePath}");
            }
        }
        catch (JsonReaderException jsonEx)
        {
            Function.AddLog($"JSON格式错误 {relativePath}: {jsonEx.Message}");
        }
        catch (Exception ex)
        {
            Function.AddLog($"合并JSON文件 {relativePath} 时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 移除JSON中的注释
    /// </summary>
    /// <param name="jsonText">包含注释的JSON文本</param>
    /// <returns>移除注释后的JSON文本</returns>
    private string RemoveJsonComments(string jsonText)
    {
        if (string.IsNullOrEmpty(jsonText))
            return jsonText;

        var result = new StringBuilder();
        bool inString = false;
        bool escapeNext = false;
        bool inSingleLineComment = false;
        bool inMultiLineComment = false;
        
        for (int i = 0; i < jsonText.Length; i++)
        {
            char c = jsonText[i];
            char next = i < jsonText.Length - 1 ? jsonText[i + 1] : '\0';

            // 处理转义字符
            if (escapeNext)
            {
                escapeNext = false;
                result.Append(c);
                continue;
            }

            // 处理字符串内的字符
            if (inString)
            {
                if (c == '\\' && !escapeNext)
                {
                    escapeNext = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                result.Append(c);
                continue;
            }

            // 处理多行注释
            if (inMultiLineComment)
            {
                if (c == '*' && next == '/')
                {
                    inMultiLineComment = false;
                    i++; // 跳过 '/'
                }
                continue;
            }

            // 处理单行注释
            if (inSingleLineComment)
            {
                if (c == '\n' || c == '\r')
                {
                    inSingleLineComment = false;
                    result.Append(c);
                }
                continue;
            }

            // 检查是否进入字符串
            if (c == '"')
            {
                inString = true;
                result.Append(c);
                continue;
            }

            // 检查是否是注释开始
            if (c == '/' && next == '/')
            {
                inSingleLineComment = true;
                i++; // 跳过下一个 '/'
                continue;
            }

            if (c == '/' && next == '*')
            {
                inMultiLineComment = true;
                i++; // 跳过下一个 '*'
                continue;
            }

            // 正常字符
            result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>
    /// 合并Lang文件
    /// </summary>
    /// <param name="sourceFile">源文件路径</param>
    /// <param name="targetFile">目标文件路径</param>
    /// <param name="relativePath">相对路径</param>
    /// <param name="resourceConfigDir">资源配置目录</param>
    /// <param name="originalFilesDir">原始文件备份目录</param>
    /// <param name="fileMappings">文件映射关系</param>
    private void MergeLangFiles(string sourceFile, string targetFile, string relativePath, string resourceConfigDir, string originalFilesDir, JObject fileMappings)
    {
        try
        {
            // 读取源文件
            Dictionary<string, string> sourceLangEntries = ParseLangFile(sourceFile);

            // 如果目标文件存在且还没有备份，则备份原文件
            if (File.Exists(targetFile) && !fileMappings.ContainsKey(relativePath))
            {
                string uniqueBackupName = GetUniqueBackupFileName(relativePath, originalFilesDir);
                string backupPath = Path.Combine(originalFilesDir, uniqueBackupName);
                File.Copy(targetFile, backupPath, true);
                fileMappings[relativePath] = uniqueBackupName;
            }
            else if (!File.Exists(targetFile))
            {
                // 如果是新增文件，记录到AddFile列表
                fileMappings[relativePath] = ""; // 占位符，表示新增文件
            }

            // 如果目标文件存在，则合并
            if (File.Exists(targetFile))
            {
                // 读取目标文件
                Dictionary<string, string> targetLangEntries = ParseLangFile(targetFile);

                // 合并条目：源文件的条目会覆盖目标文件中的同名条目
                foreach (var entry in sourceLangEntries)
                {
                    targetLangEntries[entry.Key] = entry.Value;
                }

                // 写入合并后的内容
                WriteLangFile(targetFile, targetLangEntries);
                Function.AddLog($"已合并Lang文件: {relativePath}");
            }
            else
            {
                // 目标文件不存在，直接写入源文件内容
                WriteLangFile(targetFile, sourceLangEntries);
                Function.AddLog($"已创建Lang文件: {relativePath}");
            }
        }
        catch (Exception ex)
        {
            Function.AddLog($"合并Lang文件 {relativePath} 时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析Lang文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>键值对字典</returns>
    private Dictionary<string, string> ParseLangFile(string filePath)
    {
        var entries = new Dictionary<string, string>();
        string[] lines = File.ReadAllLines(filePath);

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();

            // 跳过空行和注释行
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("##"))
                continue;

            // 查找等号分隔符（第一个等号）
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex > 0)
            {
                string key = line.Substring(0, equalsIndex).TrimEnd();
                string value = line.Substring(equalsIndex + 1);

                // 处理行尾注释（以##开头）
                int commentIndex = value.IndexOf("##");
                if (commentIndex >= 0)
                {
                    value = value.Substring(0, commentIndex).TrimEnd('\t');
                }

                entries[key] = value;
            }
        }

        return entries;
    }

    /// <summary>
    /// 写入Lang文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="entries">键值对字典</param>
    private void WriteLangFile(string filePath, Dictionary<string, string> entries)
    {
        var lines = new List<string>();

        foreach (var entry in entries)
        {
            lines.Add($"{entry.Key}={entry.Value}");
        }

        File.WriteAllLines(filePath, lines);
    }

    /// <summary>
    /// 判断是否为特殊JSON文件需要特殊处理
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <returns>是否为特殊JSON文件</returns>
    private bool IsSpecialJsonFile(string relativePath)
    {
        string fileName = Path.GetFileName(relativePath).ToLower();
        
        // 包含动画、行为、战利品表等文件通常需要特殊的合并处理
        string[] specialPatterns = { "animation", "behavior", "loot_table", "recipe", "trading" };
        
        foreach (string pattern in specialPatterns)
        {
            if (fileName.Contains(pattern))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// 合并特殊JSON文件
    /// </summary>
    /// <param name="targetJson">目标JSON对象</param>
    /// <param name="sourceJson">源JSON对象</param>
    /// <param name="relativePath">相对路径</param>
    private void MergeSpecialJsonFiles(JObject targetJson, JObject sourceJson, string relativePath)
    {
        try
        {
            // 对于特殊JSON文件，我们采用更精细的合并策略
            foreach (var property in sourceJson.Properties())
            {
                string propertyName = property.Name;
                
                // 对于数组类型的属性，使用合并而非替换
                if (property.Value.Type == JTokenType.Array)
                {
                    if (targetJson.ContainsKey(propertyName))
                    {
                        // 如果目标已存在该数组，合并两个数组
                        JArray targetArray = (JArray)targetJson[propertyName];
                        JArray sourceArray = (JArray)property.Value;
                        
                        // 将源数组元素添加到目标数组
                        foreach (var item in sourceArray)
                        {
                            targetArray.Add(item);
                        }
                    }
                    else
                    {
                        // 如果目标不存在该数组，直接添加
                        targetJson[propertyName] = property.Value.DeepClone();
                    }
                }
                else if (property.Value.Type == JTokenType.Object)
                {
                    // 对于对象类型，递归处理
                    if (targetJson.ContainsKey(propertyName) && targetJson[propertyName].Type == JTokenType.Object)
                    {
                        // 递归合并子对象
                        JObject targetObj = (JObject)targetJson[propertyName];
                        JObject sourceObj = (JObject)property.Value;
                        MergeSpecialJsonFiles(targetObj, sourceObj, relativePath);
                    }
                    else
                    {
                        // 如果目标不存在该对象或类型不同，直接替换
                        targetJson[propertyName] = property.Value.DeepClone();
                    }
                }
                else
                {
                    // 其他类型直接替换
                    targetJson[propertyName] = property.Value.DeepClone();
                }
            }
        }
        catch (Exception ex)
        {
            Function.AddLog($"合并特殊JSON文件 {relativePath} 时出错: {ex.Message}");
        }
    }

    private void RestoreResources_OnClick(object sender, RoutedEventArgs e)
    {
        // 自动导航到日志页面
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigationView_Root.SelectedItem = mainWindow.NavigationViewItem_Logs;
        }

        // 在新线程中执行还原操作
        Task.Run(() =>
        {
            try
            {
                if (SettingsPage.pBedrockPath == string.Empty)
                {
                    Function.AddLog("你尚未设置基岩版路径，请前往设置页面进行设置");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Function.ShowDialog("你尚未设置基岩版路径，请前往设置页面进行设置", "错误");
                        // 获取当前应用程序的主窗口
                        if (Application.Current.MainWindow is MainWindow mainWindow)
                        {
                            // 导航到设置页面
                            mainWindow.NavigationView_Root.SelectedItem = mainWindow.NavigationView_Root.SettingsItem;
                        }
                    });
                    return;
                }

                string vanillaResourcePath = Path.Combine(SettingsPage.pBedrockPath, SettingsPage.selectBedrockFolder, "data", "resource_packs", "vanilla_netease");
                if (!Directory.Exists(vanillaResourcePath))
                {
                    Function.AddLog("基岩版原版资源包目录不存在，请检查设置是否正确");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Function.ShowDialog("基岩版原版资源包目录不存在，请检查设置是否正确", "错误");
                    });
                    return;
                }

                // 检查是否需要还原操作
                string installedFlagPath = Path.Combine(vanillaResourcePath, "InstalledResources");
                string resourceConfigPath = Path.Combine(vanillaResourcePath, "ResourceConfig");
                bool needRestore = File.Exists(installedFlagPath) || Directory.Exists(resourceConfigPath);
                
                if (needRestore)
                {
                    Function.AddLog("检测到已安装的资源包，正在执行还原操作...");
                    RestoreOriginalFiles(vanillaResourcePath, resourceConfigPath);
                    Function.AddLog("资源包还原完成");
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Function.ShowDialog("资源包还原完成", "提示");
                    });
                }
                else
                {
                    Function.AddLog("未检测到已安装的资源包，无需还原");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Function.ShowDialog("未检测到已安装的资源包，无需还原", "提示");
                    });
                }
            }
            catch (Exception ex)
            {
                Function.AddLog($"还原过程中发生错误: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Function.ShowDialog($"还原过程中发生错误: {ex.Message}", "错误");
                });
            }
        });
    }
}