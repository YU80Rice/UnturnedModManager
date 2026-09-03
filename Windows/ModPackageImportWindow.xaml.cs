using System.Windows;
using UnturnedModManager.Models;
using UnturnedModManager.Services;

namespace UnturnedModManager.Windows;

public sealed class ProfileTargetOption
{
    public string DisplayName { get; init; } = "";
    public string? ProfileId { get; init; }
    public bool IsCreateNew { get; init; }
    public override string ToString() => DisplayName;
}

public partial class ModPackageImportWindow : Window
{
    private readonly ModPackageImportPlan _plan;
    private readonly PluginProfileService _profileService;

    public PluginProfileOperationResult? Result { get; private set; }

    public ModPackageImportWindow(ModPackageImportPlan plan, PluginProfileService profileService)
    {
        InitializeComponent();
        _plan = plan;
        _profileService = profileService;

        PackageNameText.Text = string.IsNullOrWhiteSpace(plan.Manifest.Name) ? "未命名模组包" : plan.Manifest.Name;
        PackageAuthorText.Text = $"作者: {(string.IsNullOrWhiteSpace(plan.Manifest.Author) ? "未知" : plan.Manifest.Author)}";
        PackageVersionText.Text = $"版本: {(string.IsNullOrWhiteSpace(plan.Manifest.Version) ? "1.0.0" : plan.Manifest.Version)}";
        PackageDateText.Text = $"导出时间: {plan.Manifest.ExportedAt:yyyy-MM-dd HH:mm}";
        PackageDescriptionText.Text = string.IsNullOrWhiteSpace(plan.Manifest.Description) ? "暂无说明信息。" : plan.Manifest.Description;

        FilesSummaryText.Text = $"将导入的文件清单（共 {plan.Entries.Count} 项，含 {plan.ConflictingDllCount} 个覆盖备份，{plan.ConflictingConfigCount} 个配置保护）：";
        EntriesListView.ItemsSource = plan.Entries;

        PopulateProfileOptions();
    }

    private void PopulateProfileOptions()
    {
        var options = new List<ProfileTargetOption>();
        var activeId = _profileService.GetActiveProfileId();
        var existingProfiles = _profileService.GetProfiles();

        ProfileTargetOption? selectedOption = null;

        foreach (var p in existingProfiles)
        {
            var isActive = p.Id == activeId;
            var opt = new ProfileTargetOption
            {
                DisplayName = isActive ? $"{p.Name} (当前活跃)" : p.Name,
                ProfileId = p.Id,
                IsCreateNew = false
            };
            options.Add(opt);
            if (isActive)
                selectedOption = opt;
        }

        var newOption = new ProfileTargetOption
        {
            DisplayName = $"新建独立方案：“{_plan.Manifest.Name}”",
            ProfileId = null,
            IsCreateNew = true
        };
        options.Add(newOption);

        ProfileComboBox.ItemsSource = options;
        ProfileComboBox.SelectedItem = selectedOption ?? (options.Count > 0 ? options[0] : null);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = ProfileComboBox.SelectedItem as ProfileTargetOption;
        var options = new ModPackageImportOptions
        {
            TargetProfileId = selected?.ProfileId,
            CreateNewProfile = selected?.IsCreateNew ?? false,
            NewProfileName = _plan.Manifest.Name,
            PreserveLocalConfig = PreserveConfigCheckBox.IsChecked == true,
            BackupConflictingDlls = true
        };

        Result = _profileService.ImportPackageWithOptions(_plan.PackagePath, options);
        if (Result.Success)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            System.Windows.MessageBox.Show(
                this,
                $"导入失败：{Result.Message}",
                "模组包导入错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
