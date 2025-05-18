using Avalonia;
using RemnantOverseer.Services;
using RemnantOverseer.Utilities;
using System;

namespace RemnantOverseer;

internal sealed class Program
{
    public static SingleInstanceHelper? InstanceHelper { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        InstanceHelper = new SingleInstanceHelper();

        if (InstanceHelper.IsPrimaryInstance)
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);

            Log.Dispose();
        }
        else
        {
            SingleInstanceHelper.NotifyExistingInstance();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .ConfigureFonts(fontManager =>
            {
                fontManager.AddFontCollection(new MontserratFontCollection());
            })
            .WithInterFont()
            .LogToTrace();
}