using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using Xunit;

namespace UnturnedModManager.Tests;

[Collection("WpfThemeCollection")]
public sealed class MotionControlsTests
{
    [Fact]
    public void MotionControls_DictionaryLoadsAndProvidesImplicitToggleSwitchStyle()
    {
        RunOnSta(() =>
        {
            var application = System.Windows.Application.Current ?? new System.Windows.Application();
            var assemblyName = typeof(App).Assembly.GetName().Name;
            var source = new Uri($"pack://application:,,,/{assemblyName};component/Themes/Motion.Controls.xaml", UriKind.Absolute);
            var dictionary = new ResourceDictionary { Source = source };
            var key = typeof(ToggleSwitch);
            var style = Assert.IsType<Style>(dictionary[key]);
            Assert.Equal(key, style.TargetType);

            // 测试宿主可能是无应用资源的普通 Application；临时合并后检查隐式样式查找。
            var merged = application.Resources.MergedDictionaries;
            merged.Add(dictionary);
            try
            {
                Assert.Same(style, application.TryFindResource(key));

                var toggle = new ToggleSwitch { Style = style };
                Assert.True(toggle.ApplyTemplate());
                Assert.IsType<Grid>(toggle.Template.FindName("RootGrid", toggle));
                Assert.IsType<ContentPresenter>(toggle.Template.FindName("ContentPresenter", toggle));
            }
            finally
            {
                merged.Remove(dictionary);
            }
        });
    }

    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
