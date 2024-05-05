using Microsoft.Extensions.Configuration;
using System.IO.Compression;

namespace NOAARadar;

public class RadarImageService
{
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

        var conusSubPath    = config["RadarURLs:CONUS_Path"] ?? throw new Exception("Unable to get 'RadarURLs:CONUS_Path' from Configuration");
        _conusRadarImageURL = $"{_radarRootURL}/{conusSubPath}";
        _conusImageListURL  = $"{_conusRadarImageURL}/";

        var klotSubPath     = config["RadarURLs:KLOT_Path"] ?? throw new Exception("Unable to get 'RadarURLs:KLOT_Path' from Configuration");
        _klotNexRadImageURL = $"{_radarRootURL}/{klotSubPath}";
        _klotImageListURL   = $"{_klotNexRadImageURL}/";

        _localRadarFolder = config["LocalBaseFilePath"] ?? throw new Exception("Unable to get 'LocalBaseFilePath' from Configuration");
    }

    public async Task<List<RadarImage>> GetCONUSRadarImages()
    {
        var fileList        = await getFileImageList(RadarType.CONUS);
        var downloadedFiles = await downloadImageFiles(fileList, RadarType.CONUS);
        var extractedFiles  = extractZippedImages(downloadedFiles);

        return extractedFiles;
    }

    public async Task<List<RadarImage>> GetKLOTRadarImages()
    {
        var fileList        = await getFileImageList(RadarType.KLOT);
        var downloadedFiles = await downloadImageFiles(fileList, RadarType.KLOT);
        var extractedFiles  = extractZippedImages(downloadedFiles);

        return extractedFiles;
    }

    private async Task<List<RadarImage>> getFileImageList(RadarType radarType)
    {
        var images = new List<RadarImage>();

        var imageToken   = radarType == RadarType.CONUS ? _conusRadarFileToken : _klotRadarFileToken;
        var imageListURL = radarType == RadarType.CONUS ? _conusImageListURL   : _klotImageListURL;

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
            var radarImage = new RadarImage(fileUrl, radarType);
            images.Add(radarImage);

            imageListHtml = imageListHtml[fileNameEnd..];
            imageIndex = imageListHtml.IndexOf(imageToken);
        }

        // Get images covering last hour
        images = images.Where(i => i.FileDate > DateTime.Now.AddHours(-1))
                       .OrderByDescending(i => i.FileDate)
                       .ToList();

        return images;
    }

    private async Task<List<RadarImage>> downloadImageFiles(List<RadarImage> imageFileList, RadarType radarType)
    {
        var localFileList = new List<RadarImage>();

        var imageURL = radarType == RadarType.CONUS ? _conusRadarImageURL : _klotNexRadImageURL;

        var i = 0;
        foreach (var imageFileItem in imageFileList)
        {
            Console.WriteLine($"Downloading file {++i} / {imageFileList.Count}: {imageFileItem}");

            var fileURL = $"{imageURL}/{imageFileItem.OriginalFileName}";
            var localPath = $"{_localRadarFolder}/{radarType}/{imageFileItem.OriginalFileName}";

            using var downloadStream = await _httpClient.GetStreamAsync(fileURL);
            using var fileStream = new FileStream(localPath, FileMode.Create);

            downloadStream.CopyTo(fileStream);

            imageFileItem.ZippedFilePath = localPath;
            localFileList.Add(imageFileItem);
        }

        return localFileList;
    }

    private static List<RadarImage> extractZippedImages(List<RadarImage> images)
    {
        var extractedFiles = new List<RadarImage>();

        foreach (var image in images)
        {
            var extracted = extractImage(image);
            if (extracted is null)
                continue;

            extractedFiles.Add(extracted);
            File.Delete(image.ZippedFilePath!);
            image.ZippedFilePath = null; 
        }

        return extractedFiles;  
    }

    private static RadarImage? extractImage(RadarImage image)
    {
        if (string.IsNullOrEmpty(image.ZippedFilePath))
            return null;

        var fileToExtract = new FileInfo(image.ZippedFilePath);
        using FileStream zippedStream = fileToExtract.OpenRead();

        string currentFileName = fileToExtract.FullName;
        string extractedFileName = currentFileName.Remove(currentFileName.Length - fileToExtract.Extension.Length);

        using FileStream extractedStream = File.Create(extractedFileName);
        using GZipStream gzipStream = new(zippedStream, CompressionMode.Decompress);

        gzipStream.CopyTo(extractedStream);
        image.FilePath = extractedFileName;

        Console.WriteLine($"Extracted: {fileToExtract.Name}");

        return image;
    }

}
