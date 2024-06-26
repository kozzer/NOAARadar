﻿using ImageMagick;
using Microsoft.Extensions.Configuration;
using System.IO.Compression;

namespace NOAARadar;

public class RadarImageService
{
    public int AnimationDurationInHours { get; } = 6;
    private readonly HttpClient _httpClient;

    private readonly string _radarRootURL;

    private readonly string _conusRadarFileToken = @"href=""CONUS";
    private readonly string _conusRadarImageURL;
    private readonly string _conusImageListURL;

    private readonly string _klotRadarFileToken = @"href=""KLOT";
    private readonly string _klotNexRadImageURL;
    private readonly string _klotImageListURL;

    private readonly string _localRadarFolder;

    public RadarImageService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;

        // https://mrms.ncep.noaa.gov/data/RIDGEII -- got from https://opengeo.ncep.noaa.gov/geoserver/www/index.html
        _radarRootURL = config["RadarURLs:RadarDataRootURL"] ?? throw new Exception("Unable to get 'RadarURLs:RadarDataRootURL' from Configuration");

        var conusSubPath = config["RadarURLs:CONUS_Path"] ?? throw new Exception("Unable to get 'RadarURLs:CONUS_Path' from Configuration");
        _conusRadarImageURL = $"{_radarRootURL}/{conusSubPath}";
        _conusImageListURL = $"{_conusRadarImageURL}/";

        var klotSubPath = config["RadarURLs:KLOT_Path"] ?? throw new Exception("Unable to get 'RadarURLs:KLOT_Path' from Configuration");
        _klotNexRadImageURL = $"{_radarRootURL}/{klotSubPath}";
        _klotImageListURL = $"{_klotNexRadImageURL}/";

        _localRadarFolder = config["LocalBaseFilePath"] ?? throw new Exception("Unable to get 'LocalBaseFilePath' from Configuration");
    }

    public async Task<List<RadarImage>> GetCONUSRadarImages()
    {
        deleteOldFiles(RadarType.CONUS);
        var existingImages = getExistingImages(RadarType.CONUS);
        var fileList       = await getFileImageList(RadarType.CONUS);
        var newImages      = await downloadAndExtractNewImageFiles(fileList, RadarType.CONUS);

        var allImages = existingImages;
        allImages.AddRange(newImages);
        allImages = allImages.DistinctBy(img => img.FileDate).OrderBy(img => img.FileDate).ToList();

        return allImages;
    }

    public async Task<List<RadarImage>> GetKLOTRadarImages()
    {
        deleteOldFiles(RadarType.KLOT);
        var existingImages = getExistingImages(RadarType.KLOT);
        var fileList       = await getFileImageList(RadarType.KLOT);
        var newImages      = await downloadAndExtractNewImageFiles(fileList, RadarType.KLOT);

        var allImages = existingImages;
        allImages.AddRange(newImages);
        allImages = allImages.DistinctBy(img => img.FileDate).OrderBy(img => img.FileDate).ToList();

        return allImages;
    }

    private void deleteOldFiles(RadarType radarType)
    {
        var files = Directory.GetFiles(Path.Combine(_localRadarFolder, radarType.ToString()));
        foreach (var file in files)
        {
            var img = new RadarImage(file);
            if (img.FileDate < DateTime.Now.AddHours(AnimationDurationInHours * -1))
                File.Delete(file);
        }
    }

    private List<RadarImage> getExistingImages(RadarType radarType)
    {
        var files = Directory.GetFiles(Path.Combine(_localRadarFolder, radarType.ToString()));
        var imageList = new List<RadarImage>();
        foreach(var file in files)
        {
            imageList.Add(new RadarImage(file));
        }
        return imageList;
    }

    private async Task<List<RadarImage>> getFileImageList(RadarType radarType)
    {
        var images = new List<RadarImage>();

        var imageToken = radarType == RadarType.CONUS ? _conusRadarFileToken : _klotRadarFileToken;
        var imageListURL = radarType == RadarType.CONUS ? _conusImageListURL : _klotImageListURL;

        var imageListHtml = await _httpClient.GetStringAsync(imageListURL);

        var imageIndex = imageListHtml.IndexOf(imageToken);
        while (imageIndex > -1)
        {
            var fileNameStart = imageIndex + 6;                                 // href=" is 6 characters
            var fileNameEnd = imageListHtml[fileNameStart..].IndexOf('"');
            if (fileNameEnd < 0)
                break;
            fileNameEnd += fileNameStart;

            var fileUrl = imageListHtml[fileNameStart..fileNameEnd];
            var radarImage = new RadarImage(fileUrl);
            images.Add(radarImage);

            imageListHtml = imageListHtml[fileNameEnd..];
            imageIndex = imageListHtml.IndexOf(imageToken);
        }

        // Only get images that are half as long as duration (so subsequent runs don't immediate delete frames we can use!)
        images = images.Where(i => i.FileDate > DateTime.Now.AddHours((AnimationDurationInHours / 2) * -1))
                       .OrderBy(i => i.FileDate)
                       .ToList();

        return images;
    }

    private async Task<List<RadarImage>> downloadAndExtractNewImageFiles(List<RadarImage> imageFileList, RadarType radarType)
    {
        var localFileList = new List<RadarImage>();

        var imageURL = radarType == RadarType.CONUS ? _conusRadarImageURL : _klotNexRadImageURL;

        var i = 0;
        foreach (RadarImage imageFileItem in imageFileList)
        {
            Console.WriteLine($"Checking file {++i,3} / {imageFileList.Count,-3}: {imageFileItem}");

            string fileURL = $"{imageURL}/{imageFileItem.OriginalFileName}";
            string localPath = $"{_localRadarFolder}/{radarType}/{imageFileItem.OriginalFileName}";

            // See if this image has already been processed, if so add it to the list and go to next
            string localGifFilePath = StaticMethods.GetGifFilenameFor(localPath);
            if (File.Exists(localGifFilePath))
            {
                Console.WriteLine($"\tFile already exists: {localGifFilePath}");

                imageFileItem.FilePath = localGifFilePath;
                localFileList.Add(imageFileItem);
                continue;
            }

            Console.WriteLine($"\tDownloading: {localGifFilePath}");
            await downloadFileFromServer(fileURL, localPath);

            extractTifImage(imageFileItem, localPath);

            convertToGif(imageFileItem);

            // Add to list
            localFileList.Add(imageFileItem);
        }

        localFileList = localFileList.OrderBy(img => img.FileDate).ToList();
        return localFileList;
    }

    private async Task downloadFileFromServer(string serverFileUrl, string localFilePath)
    {
        // New image, so download and extract
        using var downloadStream = await _httpClient.GetStreamAsync(serverFileUrl);
        using var fileStream = new FileStream(localFilePath, FileMode.Create);

        // Download to file
        downloadStream.CopyTo(fileStream);

        fileStream.Close();
        fileStream.Dispose();
    }

    private static void extractTifImage(RadarImage image, string zippedFilePath)
    {
        if (string.IsNullOrEmpty(zippedFilePath))
            throw new ArgumentNullException(nameof(zippedFilePath));

        Console.WriteLine($"\tExtracting: {zippedFilePath}");

        var fileToExtract = new FileInfo(zippedFilePath);
        using FileStream zippedStream = fileToExtract.OpenRead();
        {
            string currentFileName = fileToExtract.FullName;
            string extractedFileName = currentFileName.Remove(currentFileName.Length - fileToExtract.Extension.Length);

            using FileStream extractedStream = File.Create(extractedFileName);
            using GZipStream gzipStream = new(zippedStream, CompressionMode.Decompress);

            gzipStream.CopyTo(extractedStream);
            image.FilePath = extractedFileName;
        }

        File.Delete(zippedFilePath);
    }

    private static void convertToGif(RadarImage image)
    {
        Console.WriteLine($"\tConverting to gif: {image.FilePath}");

        string gifPath = image.FilePath!.Replace(".tif", ".gif");

        using var magick = new MagickImage(image.FilePath);

        // If CONUS, then make the image 20% taller, as the Equirectangular projection used makes things look squished
        var resizeGeom = new MagickGeometry(7000, 4200) { IgnoreAspectRatio = true };
        if (image.RadarType == RadarType.CONUS)
            magick.Resize(resizeGeom);

        magick.Write(gifPath, MagickFormat.Gif);

        File.Delete(image.FilePath);

        image.FilePath = gifPath;
    }

}
