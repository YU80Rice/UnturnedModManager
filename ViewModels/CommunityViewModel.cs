using System.Collections.ObjectModel;
using System.Windows.Input;
using UnturnedModManager.Models;
using UnturnedModManager.Services;

namespace UnturnedModManager.ViewModels;

public sealed class CommunityViewModel : ViewModelBase, IDisposable
{
    private readonly CommunityApiClient _api;
    private CancellationTokenSource? _loadCts, _searchCts;
    private string _searchText = "";
    private CommunityCategory? _selectedCategory;
    private string _sort = "newest";
    private ViewState _state = ViewState.Idle;
    private string _errorMessage = "";
    private string _connectionText = "等待连接";
    private bool _initialized;

    public ObservableCollection<CommunityMod> Mods { get; } = [];
    public ObservableCollection<CommunityCategory> Categories { get; } = [];
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) DebounceSearch(); } }
    public CommunityCategory? SelectedCategory { get => _selectedCategory; set { if (SetProperty(ref _selectedCategory, value) && _initialized) _ = LoadAsync(); } }
    public string Sort { get => _sort; set { if (SetProperty(ref _sort, value) && _initialized) _ = LoadAsync(); } }
    public ViewState State { get => _state; private set { if (SetProperty(ref _state, value)) { OnPropertyChanged(nameof(IsLoading)); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(HasError)); } } }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsLoading => State == ViewState.Loading;
    public bool IsEmpty => State == ViewState.Empty;
    public bool HasError => State == ViewState.Error;
    public int ResultCount => Mods.Count;
    public string ConnectionText { get => _connectionText; private set => SetProperty(ref _connectionText, value); }
    public ICommand RefreshCommand { get; }
    public event Action<int>? OpenModRequested;

    public CommunityViewModel() : this(new CommunityApiClient()) { }
    public CommunityViewModel(CommunityApiClient api)
    {
        _api = api;
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(forceRefresh: true));
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            Categories.Add(new CommunityCategory { Key = "", NameZh = "全部分类" });
            foreach (var category in (await _api.GetCategoriesAsync()).OrderBy(c => c.DisplayName)) Categories.Add(category);
            if (Categories.Count > 0)
            {
                _selectedCategory = Categories[0];
                OnPropertyChanged(nameof(SelectedCategory));
            }
        }
        catch (Exception ex) { SetError(ex.Message); }
        await LoadAsync();
    }

    public void OpenSelected(CommunityMod? mod) { if (mod is not null) OpenModRequested?.Invoke(mod.Id); }

    public async Task LoadAsync(bool forceRefresh = false)
    {
        _loadCts?.Cancel(); _loadCts = new CancellationTokenSource();
        State = ViewState.Loading; ErrorMessage = "";
        try
        {
            var mods = await _api.GetModsAsync(SelectedCategory?.Key, SearchText, Sort, _loadCts.Token, forceRefresh);
            Mods.Clear(); foreach (var mod in mods) Mods.Add(mod);
            OnPropertyChanged(nameof(ResultCount));
            ConnectionText = _api.LastResponseWasCached
                ? "来自本地缓存 · 点击刷新获取最新数据"
                : "已连接 unmod.online";
            State = Mods.Count == 0 ? ViewState.Empty : ViewState.Loaded;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetError(ex.Message); }
    }

    private void DebounceSearch()
    {
        if (!_initialized) return;
        _searchCts?.Cancel(); _searchCts = new CancellationTokenSource();
        _ = DebouncedLoadAsync(_searchCts.Token);
    }
    private async Task DebouncedLoadAsync(CancellationToken token) { try { await Task.Delay(350, token); await LoadAsync(); } catch (OperationCanceledException) { } }
    private void SetError(string message) { ErrorMessage = message; ConnectionText = "社区暂时不可用"; State = ViewState.Error; }
    public void Dispose() { _loadCts?.Cancel(); _searchCts?.Cancel(); _api.Dispose(); }
}
