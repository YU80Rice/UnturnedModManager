using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace UnturnedModManager.Controls;
public partial class StatePresenter : System.Windows.Controls.UserControl
{
    public StatePresenter() => InitializeComponent();
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(StatePresenter),
        new PropertyMetadata("", OnMessageChanged));
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol), typeof(SymbolRegular), typeof(StatePresenter),
        new PropertyMetadata(SymbolRegular.Info24, OnSymbolChanged));
    public SymbolRegular Symbol { get => (SymbolRegular)GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        StateText.Text = Message;
        StateIcon.Symbol = Symbol;
    }

    private static void OnMessageChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is StatePresenter presenter && presenter.StateText is not null)
            presenter.StateText.Text = args.NewValue as string ?? "";
    }

    private static void OnSymbolChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is StatePresenter presenter && presenter.StateIcon is not null && args.NewValue is SymbolRegular symbol)
            presenter.StateIcon.Symbol = symbol;
    }
}
