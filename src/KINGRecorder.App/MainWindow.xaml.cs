using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using KINGRecorder.Presentation.ViewModels;

namespace KINGRecorder.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        this.InitializeComponent();
        ViewModel = viewModel;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Title = "KING RECORDER — Professional Screen Recording Studio";
        appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 820));
    }
}
