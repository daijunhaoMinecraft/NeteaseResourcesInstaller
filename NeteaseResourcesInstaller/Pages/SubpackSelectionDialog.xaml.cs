using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using Newtonsoft.Json.Linq;

namespace NeteaseResourcesInstaller.Pages
{
    public partial class SubpackSelectionDialog : ContentDialog
    {
        public class SubpackItem
        {
            public string Name { get; set; }
            public string FolderName { get; set; }
            public JObject Data { get; set; }
        }

        public enum SubpackSelectionType
        {
            NoSubpacks,
            SpecificSubpack
        }

        public SubpackSelectionType SelectionType { get; private set; }
        public JObject SelectedSubpack { get; private set; }
        public int SelectedSubpackIndex { get; private set; } = -1;

        private List<SubpackItem> _subpackItems = new List<SubpackItem>();
        private List<RadioButton> _subpackRadioButtons = new List<RadioButton>();
        
        public SubpackSelectionDialog(JArray subpacks)
        {
            InitializeComponent();
            
            // 为每个子包创建一个RadioButton
            for (int i = 0; i < subpacks.Count; i++)
            {
                JObject subpack = (JObject)subpacks[i];
                var subpackItem = new SubpackItem
                {
                    Name = subpack["name"]?.ToString() ?? "未知名称",
                    FolderName = subpack["folder_name"]?.ToString() ?? "未知文件夹",
                    Data = subpack
                };
                
                _subpackItems.Add(subpackItem);
                
                var radioButton = new RadioButton
                {
                    Content = $"{subpackItem.Name} ({subpackItem.FolderName})",
                    Margin = new Thickness(0, 4, 0, 4),
                    Tag = i  // 使用索引作为Tag
                };
                
                _subpackRadioButtons.Add(radioButton);
                OptionsPanel.Children.Add(radioButton);
            }
        }

        public void ContentDialog_PrimaryButtonClick()
        {
            // 检查是否选择了"不使用子包"
            if (NoSubpacksOption.IsChecked == true)
            {
                SelectionType = SubpackSelectionType.NoSubpacks;
                SelectedSubpack = null;
                SelectedSubpackIndex = -1;
            }
            else
            {
                // 查找被选中的子包RadioButton
                RadioButton selectedRadioButton = null;
                foreach (var radioButton in _subpackRadioButtons)
                {
                    if (radioButton.IsChecked == true)
                    {
                        selectedRadioButton = radioButton;
                        break;
                    }
                }
                
                if (selectedRadioButton != null && selectedRadioButton.Tag is int index && index >= 0 && index < _subpackItems.Count)
                {
                    SelectionType = SubpackSelectionType.SpecificSubpack;
                    SelectedSubpack = _subpackItems[index].Data;
                    SelectedSubpackIndex = index;
                }
                else
                {
                    // 如果没有选择任何子包，默认为不使用子包
                    SelectionType = SubpackSelectionType.NoSubpacks;
                    SelectedSubpack = null;
                    SelectedSubpackIndex = -1;
                }
            }
        }
    }
}