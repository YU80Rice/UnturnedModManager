using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UnturnedModManager.Prototypes;

public partial class PrototypeDataPage : Page
{
    public PrototypeDataPage()
    {
        InitializeComponent();
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        ResetTabButtons();
        btn.Background = new SolidColorBrush(Color.FromArgb(0xC8, 0x20, 0x29, 0x33));
        btn.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xB0, 0x24));
        btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xB0, 0x24));

        if (btn == TabCharacters)
        {
            TabTitleText.Text = "角色槽位只读总览 (Character Slots)";
            TabDescText.Text = "读取自本地 Worlds/Singleplayer_* 物理目录。第一阶段仅支持只读浏览、对比与快照导出。";
        }
        else if (btn == TabWorlds)
        {
            TabTitleText.Text = "单机世界与关卡资产 (Level & Maps)";
            TabDescText.Text = "识别各单机地图 Level 目录下的建筑、路障、载具数据尺寸与修改时间。";
        }
        else if (btn == TabServerConfig)
        {
            TabTitleText.Text = "服务器与单机配置总览 (Config.json)";
            TabDescText.Text = "读取 ModeConfigData 难度参数快照（仅只读，未受控编辑）。";
        }
        else if (btn == TabBackups)
        {
            TabTitleText.Text = "版本化快照目录与恢复管理 (Backups)";
            TabDescText.Text = "展示已有备份时间戳目录（Backups/YYYY-MM-DD/），支持导出与一键回滚。";
        }
    }

    private void ResetTabButtons()
    {
        var defaultBg = new SolidColorBrush(Color.FromArgb(0x78, 0x1A, 0x21, 0x28));
        var defaultFg = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE0));
        var defaultBorder = new SolidColorBrush(Color.FromArgb(0xFF, 0x2A, 0x34, 0x41));

        foreach (var btn in new[] { TabCharacters, TabWorlds, TabServerConfig, TabBackups })
        {
            btn.Background = defaultBg;
            btn.Foreground = defaultFg;
            btn.BorderBrush = defaultBorder;
        }
    }
}
