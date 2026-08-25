using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MessageBox = System.Windows.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;

namespace NeteaseResourcesInstaller.Pages;

public class ResourceItem
{
    public string Title { get; set; }
    public string FolderName { get; set; }
    public BitmapImage Thumbnail { get; set; }
    public string Description { get; set; }
}

public partial class InstallBedrockResources : Page, INotifyPropertyChanged
{
    #region Var

    private ObservableCollection<ResourceItem> _resourceItems = new ObservableCollection<ResourceItem>();
    public ObservableCollection<ResourceItem> ResourceItems
    {
        get => _resourceItems;
        set { _resourceItems = value; OnPropertyChanged(); }
    }
    
    public static string ResourcePath = string.Empty;

    private readonly List<string> NeedToDelete = new()
    {
        "ui\\hud_screen.json", "ui\\inventory_screen.json", "ui\\inventory_screen_pocket.json",
        "ui\\pause_screen.json", "ui\\settings_screen.json", "ui\\enchanting_screen.json",
        "ui\\enchanting_screen_pocket.json", "ui\\trade_screen.json", "ui\\how_to_play_screen.json",
        "ui\\progress_screen.json", "ui\\permissions_screen.json", "ui\\reconnect_screen.json",
        "ui\\contents.json", "ui\\player_tips.json", "ui\\emote_wheel.json", "ui\\emote_screen.json",
        "ui\\AchievementGate.json", "ui\\AchievementSys.json", "ui\\emote_two_person.json",
        "ui\\lobby_setting_screen.json", "ui\\PE_AchievementSys.json", "ui\\PopWindow.json",
        "ui\\researchPopUI.json", "ui\\researchResponseUI.json", "ui\\researchUI.json",
        "ui\\encyclopedia_screen.json", "textures\\ui\\title.png"
    };

    private readonly List<string> MustToDelete = new() { "contents.json", "ui\\contents.json" };

    #endregion

    #region Application with xaml
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion

    public InstallBedrockResources()
    {
        InitializeComponent();
        DataContext = this;
        ResourcesList.ItemsSource = ResourceItems;
        ResourcePath = Path.Combine(Var.CurrentPath, "ExtResources");

        // 【重构1】将耗时操作移出构造函数，改为页面加载完毕后异步执行
        this.Loaded += InstallBedrockResources_Loaded;
    }

    private async void InstallBedrockResources_Loaded(object sender, RoutedEventArgs e)
    {
        this.Loaded -= InstallBedrockResources_Loaded;
        await LoadResourcesAsync();
    }

    #region Helper / IO Functions

    public static BitmapImage BytesToBitmapImage(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0) return null;
        using var memoryStream = new MemoryStream(imageBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = memoryStream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private BitmapImage LoadDefaultThumbnail()
    {
        try
        {
            var image = new BitmapImage(new Uri("pack://application:,,,/Resources/pack.png"));
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    // 【重构2】合并所有 Zip 解压方法，消除 90% 重复代码
    /// <summary>
    /// 统一的解压方法
    /// </summary>
    /// <param name="zipFilePath">Zip包路径</param>
    /// <param name="extractPath">解压目标路径</param>
    /// <param name="selectedSubpack">选中的子包名（null表示不要子包，"*"表示全部子包）</param>
    private void ExtractMcpackUnified(string zipFilePath, string extractPath, string selectedSubpack = null)
    {
        using var archive = ZipFile.OpenRead(zipFilePath);
        
        // 检测是否包含单根文件夹包裹（例如外面套了一层文件夹）
        var rootEntries = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.FullName) && e.FullName.Count(c => c == '/') == 1 && e.FullName.EndsWith("/"))
            .Select(e => e.FullName)
            .ToList();
            
        string rootPrefix = rootEntries.Count == 1 && archive.Entries.All(e => e.FullName.StartsWith(rootEntries[0])) 
                            ? rootEntries[0] : string.IsNullOrEmpty(rootEntries.FirstOrDefault()) ? "" : rootEntries[0];

        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(rootPrefix)) continue;

            string relativePath = entry.FullName.Substring(rootPrefix.Length);
            if (string.IsNullOrEmpty(relativePath)) continue; // 根文件夹自身

            bool isSubpacksDir = relativePath.StartsWith("subpacks/");
            string destinationPath = Path.Combine(extractPath, relativePath);

            if (isSubpacksDir)
            {
                // 如果不要子包，跳过
                if (string.IsNullOrEmpty(selectedSubpack)) continue;
                
                if (selectedSubpack != "*")
                {
                    // 仅提取特定子包，并将其映射到根目录
                    string targetSubpackPrefix = $"subpacks/{selectedSubpack}/";
                    if (!relativePath.StartsWith(targetSubpackPrefix)) continue;
                    
                    // 路径映射：将 subpacks/xxx/a.json 变成 a.json
                    string mappedPath = relativePath.Substring(targetSubpackPrefix.Length);
                    destinationPath = Path.Combine(extractPath, mappedPath);
                }
            }

            // 执行解压
            if (entry.FullName.EndsWith("/"))
            {
                Directory.CreateDirectory(destinationPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                entry.ExtractToFile(destinationPath, true);
            }
        }
    }

    #endregion

    #region Core Logic: Load, Add, Delete

    private async Task LoadResourcesAsync()
    {
        if (!Directory.Exists(ResourcePath))
        {
            Directory.CreateDirectory(ResourcePath);
            return;
        }

        try
        {
            Function.AddLog("读取已经解压好的资源..");
            // 在后台线程读取磁盘，不卡UI
            var loadedItems = await Task.Run(() =>
            {
                var items = new List<ResourceItem>();
                foreach (string folderPath in Directory.GetDirectories(ResourcePath))
                {
                    string manifestPath = Path.Combine(folderPath, "manifest.json");
                    if (!File.Exists(manifestPath)) continue;

                    JObject jManifest = JObject.Parse(File.ReadAllText(manifestPath));
                    string name = jManifest["header"]?["name"]?.ToString() ?? "Unknown";
                    string desc = jManifest["header"]?["description"]?.ToString() ?? "";

                    // 清空 contents.json
                    File.WriteAllText(Path.Combine(folderPath, "contents.json"), "{}");
                    string uiContents = Path.Combine(folderPath, "ui", "contents.json");
                    if (File.Exists(uiContents)) File.WriteAllText(uiContents, "{}");

                    string iconPath = Path.Combine(folderPath, "pack_icon.png");
                    items.Add(new ResourceItem
                    {
                        Title = name,
                        FolderName = Path.GetFileName(folderPath),
                        Description = desc,
                        // 缩略图在后台读取为 Byte，返回UI线程后再转为 BitmapImage（WPF跨线程限制）
                    });
                }
                return items;
            });

            // 回到 UI 线程绑定数据
            foreach (var item in loadedItems)
            {
                string iconPath = Path.Combine(ResourcePath, item.FolderName, "pack_icon.png");
                item.Thumbnail = File.Exists(iconPath) 
                    ? BytesToBitmapImage(File.ReadAllBytes(iconPath)) 
                    : LoadDefaultThumbnail();
                ResourceItems.Add(item);
            }
        }
        catch (Exception e)
        {
            Function.AddLog($"读取资源包时出现错误:{e.Message}");
            Function.ShowDialog($"读取资源包时出现错误:{e.Message}", "错误");
        }
    }

    // 【重构3】使用 async/await 代替嵌套的 Task.Run 和 Dispatcher.Invoke
    private async void LoadNewResource_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择mcpack文件", Filter = "(*.mcpack)|*.mcpack" };
        if (dialog.ShowDialog() != true) return;

        string zipFilePath = dialog.FileName;
        string extractDir = Path.Combine(ResourcePath, Path.GetFileNameWithoutExtension(zipFilePath));

        try
        {
            if (!Directory.Exists(ResourcePath)) Directory.CreateDirectory(ResourcePath);

            JObject jManifest = null;
            // 1. 在后台轻量级读取 manifest
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipFilePath);
                var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));
                if (entry == null) throw new Exception("不是有效的资源包: manifest.json不存在");

                using var reader = new StreamReader(entry.Open());
                jManifest = JObject.Parse(reader.ReadToEnd());
            });

            string selectedSubpack = null; // 默认不解压子包

            // 2. 检查子包，需要在UI线程弹窗
            if (jManifest.ContainsKey("subpacks") && jManifest["subpacks"] is JArray subpacks && subpacks.Count > 0)
            {
                var subpackDialog = new SubpackSelectionDialog(subpacks);
                if (await subpackDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    subpackDialog.ContentDialog_PrimaryButtonClick();
                    selectedSubpack = subpackDialog.SelectedSubpack?["folder_name"]?.ToString();
                }
                else return; // 取消
            }

            // 3. 在后台执行繁重的解压操作
            await Task.Run(() =>
            {
                ExtractMcpackUnified(zipFilePath, extractDir, selectedSubpack);
            });

            // 4. 解压完成，更新UI列表
            string name = jManifest["header"]?["name"]?.ToString() ?? "未知";
            string desc = jManifest["header"]?["description"]?.ToString() ?? "";
            
            ResourceItems.Add(new ResourceItem
            {
                Title = name,
                FolderName = Path.GetFileName(extractDir),
                Description = desc,
                Thumbnail = File.Exists(Path.Combine(extractDir, "pack_icon.png"))
                    ? BytesToBitmapImage(File.ReadAllBytes(Path.Combine(extractDir, "pack_icon.png")))
                    : LoadDefaultThumbnail()
            });
        }
        catch (Exception ex)
        {
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            Function.ShowDialog($"处理资源包时出现错误: {ex.Message}", "错误");
        }
    }

    private async void DeleteResource_OnClick(object sender, RoutedEventArgs e)
    {
        var toRemove = ResourcesList.SelectedItems.Cast<ResourceItem>().ToList();
        if (!toRemove.Any())
        {
            Function.ShowDialog("请选择要删除的资源包", "提示");
            return;
        }

        if (MessageBox.Show($"确定要删除选中的 {toRemove.Count} 个资源包吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            return;

        // 后台删除文件
        await Task.Run(() =>
        {
            foreach (var item in toRemove)
            {
                string path = Path.Combine(ResourcePath, item.FolderName);
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        });

        // 移除UI
        foreach (var item in toRemove) ResourceItems.Remove(item);
    }
    #endregion

    #region List Moving Logic (Up / Down)
    
    private void ResourceUp_OnClick(object sender, RoutedEventArgs e) => MoveSelectedBlocks(up: true);
    private void ResourceDown_OnClick(object sender, RoutedEventArgs e) => MoveSelectedBlocks(up: false);

    private void MoveSelectedBlocks(bool up)
    {
        if (ResourcesList.SelectedItems.Count == 0) return;

        var selectedItems = ResourcesList.SelectedItems.Cast<ResourceItem>().ToList();
        var indices = selectedItems.Select(item => ResourceItems.IndexOf(item)).OrderBy(i => i).ToList();

        var list = ResourceItems.ToList();
        bool moved = false;

        if (up)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index > 0 && (i == 0 || indices[i - 1] != index - 1))
                {
                    var item = list[index];
                    list.RemoveAt(index);
                    list.Insert(index - 1, item);
                    indices[i]--;
                    moved = true;
                }
            }
        }
        else
        {
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                int index = indices[i];
                if (index < list.Count - 1 && (i == indices.Count - 1 || indices[i + 1] != index + 1))
                {
                    var item = list[index];
                    list.RemoveAt(index);
                    list.Insert(index + 1, item);
                    indices[i]++;
                    moved = true;
                }
            }
        }

        if (moved)
        {
            ResourceItems.Clear();
            foreach (var item in list) ResourceItems.Add(item);
            foreach (var item in selectedItems) ResourcesList.SelectedItems.Add(item);
        }
    }

    #endregion

    #region Install & Merge Logic

    // 【重构4】使用 async void，消除 Task 嵌套和 Invoke
    private async void InstallResource_Onclick(object sender, RoutedEventArgs e)
    {
        var selectedItems = ResourcesList.SelectedItems.Cast<ResourceItem>().ToList();
        if (!selectedItems.Any())
        {
            Function.ShowDialog("请选择要安装的资源包", "提示");
            return;
        }

        if (string.IsNullOrEmpty(SettingsPage.pBedrockPath))
        {
            Function.ShowDialog("你尚未设置基岩版路径，请前往设置页面进行设置", "错误");
            if (Application.Current.MainWindow is MainWindow mw) mw.NavigationView_Root.SelectedItem = mw.NavigationView_Root.SettingsItem;
            return;
        }

        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.NavigationView_Root.SelectedItem = mainWindow.NavigationViewItem_Logs;

        string vanillaPath = Path.Combine(SettingsPage.pBedrockPath, SettingsPage.selectBedrockFolder, "data", "resource_packs", "vanilla_netease");
        
        if (!Directory.Exists(vanillaPath))
        {
            Function.ShowDialog("基岩版原版资源包目录不存在", "错误");
            return;
        }

        try
        {
            // 将耗时安装放到后台
            await Task.Run(() => 
            {
                string configPath = Path.Combine(vanillaPath, "ResourceConfig");
                if (File.Exists(Path.Combine(vanillaPath, "InstalledResources")) || Directory.Exists(configPath))
                {
                    RestoreOriginalFiles(vanillaPath, configPath);
                }

                InstallSelectedResourcesInternal(vanillaPath, selectedItems);
            });

            Function.ShowDialog("资源包安装完成", "提示");
        }
        catch (Exception ex)
        {
            Function.ShowDialog($"安装过程中发生错误: {ex.Message}", "错误");
        }
    }

    private void InstallSelectedResourcesInternal(string vanillaPath, List<ResourceItem> selectedItems)
    {
        string configDir = Path.Combine(vanillaPath, "ResourceConfig");
        string originalFilesDir = Path.Combine(configDir, "OriginalFiles");
        
        if (Directory.Exists(configDir)) Directory.Delete(configDir, true);
        Directory.CreateDirectory(originalFilesDir);

        JObject restoreData = new JObject();
        JArray addFiles = new JArray();
        JObject fileMappings = new JObject();

        string cacheDir = Path.Combine(Path.GetTempPath(), "NeteaseResourcesInstallerCache");
        if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        Directory.CreateDirectory(cacheDir);

        try
        {
            foreach (var item in selectedItems)
            {
                string path = Path.Combine(ResourcePath, item.FolderName);
                if (Directory.Exists(path)) MergeResourceToCache(path, cacheDir);
            }

            ProcessCachedResources(cacheDir, vanillaPath, configDir, originalFilesDir, fileMappings, addFiles);

            restoreData["AddFile"] = addFiles;
            restoreData["FileMappings"] = fileMappings;
            File.WriteAllText(Path.Combine(configDir, "Restore.json"), restoreData.ToString());
            File.Create(Path.Combine(vanillaPath, "InstalledResources")).Dispose();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    private void MergeResourceToCache(string resourcePath, string cacheDir)
    {
        foreach (string file in Directory.GetFiles(resourcePath, "*.*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).Equals("manifest.json", StringComparison.OrdinalIgnoreCase)) continue;

            string relative = file.Substring(resourcePath.Length + 1);
            string targetPath = Path.Combine(cacheDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            try
            {
                if (File.Exists(targetPath))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".json") MergeJsonContent(file, targetPath);
                    else if (ext == ".lang") MergeLangContent(file, targetPath);
                    else File.Copy(file, targetPath, true);
                }
                else
                {
                    File.Copy(file, targetPath, true);
                }
            }
            catch { /* Log if needed */ }
        }
    }

    private void ProcessCachedResources(string cacheDir, string vanillaPath, string configDir, string originalFilesDir, JObject fileMappings, JArray addFiles)
    {
        // 冲突清理
        var deleteList = SettingsPage.deleteDuplicate ? NeedToDelete.Concat(MustToDelete) : MustToDelete;
        foreach (string delRelative in deleteList)
        {
            string targetPath = Path.Combine(vanillaPath, delRelative);
            if (File.Exists(targetPath))
            {
                if (!fileMappings.ContainsKey(delRelative))
                {
                    string backupName = GetUniqueBackupFileName(delRelative, originalFilesDir);
                    File.Copy(targetPath, Path.Combine(originalFilesDir, backupName), true);
                    fileMappings[delRelative] = backupName;
                }
                File.Delete(targetPath);
            }
        }

        // 缓存合并到游戏
        foreach (string file in Directory.GetFiles(cacheDir, "*.*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(cacheDir.Length + 1);
            if (relative.EndsWith("contents.json", StringComparison.OrdinalIgnoreCase)) continue;

            string targetPath = Path.Combine(vanillaPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            string ext = Path.GetExtension(file).ToLower();
            
            if (ext == ".json" || ext == ".material")
            {
                MergeJsonFiles(file, targetPath, relative, originalFilesDir, fileMappings);
            }
            else if (ext == ".lang")
            {
                MergeLangFiles(file, targetPath, relative, originalFilesDir, fileMappings);
            }
            else
            {
                BackupAndCopy(file, targetPath, relative, originalFilesDir, fileMappings, addFiles);
            }
        }
    }

    private void BackupAndCopy(string source, string target, string relative, string originalFilesDir, JObject mappings, JArray adds = null)
    {
        if (File.Exists(target) && !mappings.ContainsKey(relative))
        {
            string backupName = GetUniqueBackupFileName(relative, originalFilesDir);
            File.Copy(target, Path.Combine(originalFilesDir, backupName), true);
            mappings[relative] = backupName;
        }
        else if (!File.Exists(target))
        {
            if (adds != null) adds.Add(relative);
            else mappings[relative] = "";
        }
        File.Copy(source, target, true);
    }

    // JSON 合并复用
    private void MergeJsonContent(string source, string target)
    {
        var srcToken = JToken.Parse(RemoveJsonComments(File.ReadAllText(source)));
        var tgtToken = JToken.Parse(RemoveJsonComments(File.ReadAllText(target)));

        if (srcToken is JArray srcArr && tgtToken is JArray tgtArr)
        {
            var existing = new HashSet<string>(tgtArr.Select(t => t.ToString()));
            foreach (var item in srcArr.Where(i => !existing.Contains(i.ToString()))) tgtArr.Add(item);
            File.WriteAllText(target, tgtArr.ToString(Formatting.Indented));
        }
        else if (srcToken is JObject srcObj && tgtToken is JObject tgtObj)
        {
            tgtObj.Merge(srcObj, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat });
            File.WriteAllText(target, tgtObj.ToString(Formatting.Indented));
        }
        else File.WriteAllText(target, srcToken.ToString(Formatting.Indented));
    }

    private void MergeJsonFiles(string sourceFile, string targetFile, string relativePath, string originalFilesDir, JObject mappings)
    {
        try
        {
            if (!File.Exists(targetFile))
            {
                BackupAndCopy(sourceFile, targetFile, relativePath, originalFilesDir, mappings);
                return;
            }
            
            BackupAndCopy(sourceFile, targetFile + ".tmp", relativePath, originalFilesDir, mappings); // 只触发备份机制
            File.Delete(targetFile + ".tmp");
            MergeJsonContent(sourceFile, targetFile);
        }
        catch 
        {
            BackupAndCopy(sourceFile, targetFile, relativePath, originalFilesDir, mappings);
        }
    }

    private void MergeLangContent(string source, string target)
    {
        var tgtDict = ParseLangFile(target);
        foreach (var kvp in ParseLangFile(source)) tgtDict[kvp.Key] = kvp.Value;
        File.WriteAllLines(target, tgtDict.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private void MergeLangFiles(string source, string target, string relative, string originalFilesDir, JObject mappings)
    {
        if (!File.Exists(target)) BackupAndCopy(source, target, relative, originalFilesDir, mappings);
        else 
        {
            BackupAndCopy(source, target + ".tmp", relative, originalFilesDir, mappings);
            File.Delete(target + ".tmp");
            MergeLangContent(source, target);
        }
    }

    private Dictionary<string, string> ParseLangFile(string path)
    {
        var dict = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(path).Select(l => l.Trim()))
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith("##")) continue;
            int eq = line.IndexOf('=');
            if (eq > 0) dict[line.Substring(0, eq).TrimEnd()] = line.Substring(eq + 1).Split("##")[0].TrimEnd();
        }
        return dict;
    }

    private string RemoveJsonComments(string jsonText)
    {
        // 简化的正则或状态机清理，这里保留你原来的基础逻辑结构，简化了部分代码
        if (string.IsNullOrEmpty(jsonText)) return jsonText;
        var sb = new StringBuilder();
        bool inStr = false, inLineCom = false, inBlockCom = false;
        for (int i = 0; i < jsonText.Length; i++)
        {
            char c = jsonText[i];
            char next = i < jsonText.Length - 1 ? jsonText[i + 1] : '\0';

            if (inStr) { sb.Append(c); if (c == '"' && jsonText[i - 1] != '\\') inStr = false; continue; }
            if (inLineCom) { if (c == '\n') { inLineCom = false; sb.Append(c); } continue; }
            if (inBlockCom) { if (c == '*' && next == '/') { inBlockCom = false; i++; } continue; }

            if (c == '"') { inStr = true; sb.Append(c); }
            else if (c == '/' && next == '/') { inLineCom = true; i++; }
            else if (c == '/' && next == '*') { inBlockCom = true; i++; }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private string GetUniqueBackupFileName(string relativePath, string originalFilesDir)
    {
        string name = Path.GetFileNameWithoutExtension(relativePath);
        string ext = Path.GetExtension(relativePath);
        int counter = 1;
        string finalName = relativePath.Replace("\\", "_").Replace("/", "_");
        while (File.Exists(Path.Combine(originalFilesDir, finalName)))
        {
            finalName = $"{name}_{counter++}{ext}";
        }
        return finalName;
    }

    #endregion

    #region Restore Logic

    private async void RestoreResources_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SettingsPage.pBedrockPath))
        {
            Function.ShowDialog("你尚未设置基岩版路径", "错误");
            return;
        }

        string vanillaPath = Path.Combine(SettingsPage.pBedrockPath, SettingsPage.selectBedrockFolder, "data", "resource_packs", "vanilla_netease");
        string configPath = Path.Combine(vanillaPath, "ResourceConfig");

        if (File.Exists(Path.Combine(vanillaPath, "InstalledResources")) || Directory.Exists(configPath))
        {
            await Task.Run(() => RestoreOriginalFiles(vanillaPath, configPath));
            Function.ShowDialog("资源包还原完成", "提示");
        }
        else
        {
            Function.ShowDialog("未检测到已安装的资源包，无需还原", "提示");
        }
    }

    private void RestoreOriginalFiles(string vanillaPath, string configPath)
    {
        try
        {
            string jsonPath = Path.Combine(configPath, "Restore.json");
            if (File.Exists(jsonPath))
            {
                var data = JObject.Parse(File.ReadAllText(jsonPath));
                foreach (var mapping in (JObject)data["FileMappings"])
                {
                    string target = Path.Combine(vanillaPath, mapping.Key);
                    if (string.IsNullOrEmpty(mapping.Value.ToString()))
                    {
                        if (File.Exists(target)) File.Delete(target);
                    }
                    else
                    {
                        string backup = Path.Combine(configPath, "OriginalFiles", mapping.Value.ToString());
                        if (File.Exists(backup))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            File.Copy(backup, target, true);
                        }
                    }
                }
                foreach (string addFile in (JArray)data["AddFile"])
                {
                    string file = Path.Combine(vanillaPath, addFile);
                    if (File.Exists(file)) File.Delete(file);
                }
            }
            if (Directory.Exists(configPath)) Directory.Delete(configPath, true);
            string flag = Path.Combine(vanillaPath, "InstalledResources");
            if (File.Exists(flag)) File.Delete(flag);
        }
        catch { /* Log errors */ }
    }

    #endregion
}