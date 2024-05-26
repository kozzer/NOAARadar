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
        services.AddSingleton<AnimationService>();

        ServiceProvider = services.BuildServiceProvider();
    }

    static void Main()
    {
        var radarService = ServiceProvider.GetRequiredService<RadarImageService>();
        var animationService = ServiceProvider.GetRequiredService<AnimationService>();

        Task.WaitAll([
            Task.Run(() => generateCONUSAnimation(radarService, animationService)),
            Task.Run(() => generateKLOTAnimation(radarService, animationService))
        ]);
    }

    static async Task generateCONUSAnimation(RadarImageService radarService, AnimationService animationService)
    {
        List<RadarImage> conusFiles = await radarService.GetCONUSRadarImages();
        Console.WriteLine($"CONUS: Found {conusFiles.Count} CONUS radar image files");

        await animationService.GenerateAnimation(conusFiles);
        Console.WriteLine($"CONUS: Animation generated");
    }

    static async Task generateKLOTAnimation(RadarImageService radarService, AnimationService animationService)
    {
        List<RadarImage> klotFiles = await radarService.GetKLOTRadarImages();
        Console.WriteLine($"KLOT: Found {klotFiles.Count} KLOT radar image files");

        await animationService.GenerateAnimation(klotFiles);
        Console.WriteLine($"KLOT: Animation generated");
    }
}
