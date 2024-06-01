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

            if (image.RadarType == RadarType.CONUS && i % 4 != 0)
                continue;

            Console.WriteLine($"\tPreparing {image.RadarType,-5} frame {i,3} of {images.Count,-3}: {image.FilePath}");

            MagickImage magickImage = prepareFrame(image);
            animation.Add(magickImage);
        }

        animation[0].AnimationIterations = 0;
        animation[^1].AnimationDelay = 200;

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

    private static MagickImage prepareFrame(RadarImage image)
    {
        var magickImage = new MagickImage(image.FilePath!)
        {
            AnimationDelay = 1,
            BackgroundColor = MagickColors.Transparent,
            GifDisposeMethod = GifDisposeMethod.Previous,
        };

        magickImage.Resize(1920, 0);
        magickImage = addTimestampToFrame(magickImage, image.FileDate);
        return magickImage;
    }

    private static MagickImage addTimestampToFrame(MagickImage image, DateTime frameTimeStamp)
    {
        var settings = new MagickReadSettings
        {
            Font = "Cascadia Mono",
            FontFamily = "Cascadia Mono",
            FillColor = MagickColors.LightGray,
            StrokeColor = MagickColors.LightGray,
            TextGravity = Gravity.Center,
            BackgroundColor = new MagickColor("#66666699"),
            BorderColor = MagickColors.Black,
            Height = 40, // height of text box
            Width = 175, // width of text box
            FontPointsize = 20
        };

        using (var caption = new MagickImage($"caption:{frameTimeStamp:ddd h:mm tt}", settings))
        image.Composite(caption, Gravity.Southeast, CompositeOperator.Over);

        return image;
    }
}
