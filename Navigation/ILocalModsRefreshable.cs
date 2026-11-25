namespace UnturnedModManager.Navigation;

/// <summary>
/// 允许外壳（主窗体的拖拽导入与文件关联向导）在导入完成后刷新"本地插件"视图。
/// 主窗体只依赖该接口，不直接依赖具体页面类型，便于 v3.0 把本地插件并入模组工坊页。
/// </summary>
public interface ILocalModsRefreshable
{
    Task RefreshAfterExternalImportAsync();
}
