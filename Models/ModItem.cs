using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UnturnedModManager.Models;

public class ModItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _fileName = string.Empty;
    private string _installTime = string.Empty;
    private bool _isEnabled;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(); }
    }

    public string InstallTime
    {
        get => _installTime;
        set { _installTime = value; OnPropertyChanged(); }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}