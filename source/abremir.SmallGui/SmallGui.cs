using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using abremir.MicroUISharp;
using AvaloniaLayout = Avalonia.Layout;

namespace abremir.SmallGui;

public static class SmallGui
{
    private static Window? SmallGuiWindow;

    public static void Run(
        string title = "Small GUI",
        int width = 800,
        int height = 600,
        MuStyle? style = null,
        bool fullScreen = false,
        Action<MuContext>? context = null)
    {
        style ??= new();

        void AppMain(Application application, string[] args)
        {
            MicroUiControl control = new(context ?? throw new ArgumentNullException(nameof(context)), style)
            {
                HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Stretch,
            };

            SmallGuiWindow = new()
            {
                Title = title,
                Width = width,
                Height = height,
                Content = control,
                Background = new SolidColorBrush(style.Colors[MuColorType.WindowBg].ToColor()),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowState = fullScreen ? WindowState.FullScreen : WindowState.Normal,
            };

            SmallGuiWindow.Show();
            application.Run(SmallGuiWindow);
        }

        AppBuilder.Configure<Application>()
            .UsePlatformDetect()
            .LogToTrace()
            .Start(AppMain, []);
    }

    public static void Clear(Color color) =>
        SmallGuiWindow?.Background = new SolidColorBrush(color);
}
