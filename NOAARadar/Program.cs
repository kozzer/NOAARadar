using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NOAARadar;

// CONUS google map URL https://www.google.com/maps/@37.5,-95,5z
//                      https://www.google.com/maps/@?api=1&map_action=map&center=37.5%2C-95&zoom=4

// KLOT google map URL https://www.google.com/maps/@41.604275,-88.084275,7z


internal class Program
{
    public static readonly IServiceProvider ServiceProvider;
    
    static Program()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build());
        services.AddSingleton<HttpClient>();
        services.AddSingleton<RadarImageService>();

        ServiceProvider = services.BuildServiceProvider();
    }

    static async Task Main()
    {
        var radarService = ServiceProvider.GetRequiredService<RadarImageService>();

        var conusFiles = await radarService.GetCONUSRadarImages();
        var klotFiles  = await radarService.GetKLOTRadarImages();

        Console.WriteLine($"\n\n\n\nFound {conusFiles.Count} CONUS radar image files");
        Console.WriteLine($"Found {klotFiles.Count} KLOT radar image files");
    }
}
