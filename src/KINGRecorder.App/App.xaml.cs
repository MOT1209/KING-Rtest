using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using KINGRecorder.App.ViewModels;

namespace KINGRecorder.App;

public partial class App : Application
{
    private Window? _mainWindow;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var vm = Program.Services.GetRequiredService<MainViewModel>();
        _mainWindow = new MainWindow(vm);
        _mainWindow.Activate();
    }
}
