namespace NOAARadar;

public class RadarImage
{
    public string OriginalFileName { get; }
    public DateTime FileDate { get; }
    public RadarType RadarType { get; } 

    public string? FilePath { get; set; } = null;

    public RadarImage(string fileName)
    {
        OriginalFileName = fileName;            // ex. CONUS_L2_CREF_QCD_20240504_230241.tif.gz
        RadarType = OriginalFileName.Contains("CONUS") ? RadarType.CONUS : RadarType.KLOT;

        fileName = fileName.Replace(".tif.gz", "");
        var timeOfDay = fileName[(fileName.LastIndexOf('_') + 1)..];

        fileName = fileName.Replace($"_{timeOfDay}", "");
        var fileDate = fileName[(fileName.LastIndexOf('_') + 1)..];

        FileDate = DateTime.Parse($"{fileDate[..4]}-{fileDate[4..6]}-{fileDate[6..8]} {timeOfDay[..2]}:{timeOfDay[2..4]}:{timeOfDay[4..6]}");

        // Correct for GMT --> CDT
        FileDate = FileDate.AddHours(-5);
    }

    public override string ToString() => $"{RadarType}  |  {FileDate:MM/dd/yyyy hh:mm:ss tt}  |  {OriginalFileName}";
}
