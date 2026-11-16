using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using KINGRecorder.Presentation.ViewModels;

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
        var viewModel = Program.Services.GetRequiredService<MainViewModel>();
        var window = new MainWindow(viewModel);

        // Unsubscribe the ViewModel from engine events when the window closes so a transient
        // ViewModel cannot keep the singleton engine alive after shutdown.
        window.Closed += (_, _) => viewModel.Dispose();

        _mainWindow = window;
        window.Activate();
        _ = viewModel.InitializeAsync();
    }
}
