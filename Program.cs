using AISpeech;
using AISpeech.Services;
using Microsoft.Extensions.Configuration;

namespace AISpeech;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .Build();

        var settings = new AppSettings();
        configuration.GetSection("AISpeech").Bind(settings);

        var debugMode = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);
        DebugForm? debugForm = debugMode ? new DebugForm() : null;
        debugForm?.Show();

        using var appContext = new TrayApplicationContext(settings, debugForm);
        Application.Run(appContext);
    }
}
