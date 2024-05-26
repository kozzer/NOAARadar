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
        using var animation = new MagickImageCollection();
        foreach (RadarImage image in images)
        {
            i++;

            if (images[0].RadarType == RadarType.CONUS && i % 4 != 0)
                continue;

            Console.WriteLine($"\tPreparing frame {i,3} of {images.Count,-3}: {image.FilePath}");

            MagickImage magickImage = prepareFrame(image, i == 1, i >= images.Count - 1);
            animation.Add(magickImage);
        }

        var radarType = images[0].RadarType;
        var publishedFileName = $"{radarType}.gif";
        var publishFilePath = Path.Combine(_configuration["LocalBaseFilePath"]!, publishedFileName);

        var exportingFileName = publishedFileName.Replace(".gif", "___exporting.gif");
        var exportFilePath = Path.Combine(_configuration["LocalBaseFilePath"]!, exportingFileName);

        await animation.WriteAsync(exportFilePath);

        File.Delete(publishFilePath);
        File.Move(exportFilePath, publishFilePath);

        Console.WriteLine($"{radarType} animation exported to {publishFilePath}");

        return exportFilePath;
    }

    private static MagickImage prepareFrame(RadarImage image, bool isFirstImage, bool isLastImage)
    {
        var magickImage = new MagickImage(image.FilePath!)
        {
            BackgroundColor = MagickColors.Transparent,
            GifDisposeMethod = GifDisposeMethod.Previous,
        };

        if (isFirstImage)
            magickImage.AnimationIterations = 0;

        magickImage.AnimationDelay = isLastImage ? 200 : 1;

        magickImage.Resize(1920, 0);
        return magickImage;
    }
}
