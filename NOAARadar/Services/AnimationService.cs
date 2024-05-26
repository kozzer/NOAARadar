using ImageMagick;
using Microsoft.Extensions.Configuration;

namespace NOAARadar;

public class AnimationService
{
    private readonly IConfiguration _configuration;
    public AnimationService(IConfiguration config) => _configuration = config;

    public async Task<string> GenerateAnimation(List<RadarImage> images)
    {
        if (images.IsNullOrEmpty())
            throw new ArgumentException($"{nameof(images)} is null or an empty list");

        int i = 0;
        var animation = new MagickImageCollection();
        foreach (RadarImage image in images)
        {
            Console.WriteLine($"\tPreparing frame {++i,3} of {images.Count,-3}: {image.FilePath}");

            var magickImage = new MagickImage(image.FilePath!)
            {
                AnimationDelay = 1,
                BackgroundColor = MagickColors.Transparent,
                GifDisposeMethod = GifDisposeMethod.Previous
            };

            magickImage.Resize(1920, 0);

            animation.Add(magickImage);
        }

        var radarType = images[0].RadarType;
        var animationFileName = Path.Combine(_configuration["LocalBaseFilePath"]!, $"{radarType}_{DateTime.Now:yyyyMMdd_hhmm}.gif");

        await animation.WriteAsync(animationFileName);

        Console.WriteLine($"{radarType} animation exported to {animationFileName}");

        return animationFileName;
    }
}
