namespace NOAARadar;

public static class StaticMethods
{
    public static string GetGifFilenameFor(string zippedFileName)
    {
        return zippedFileName.Replace(".tif.gz", ".gif");
    }

    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? collection)
    {
        return collection is null || collection.Any() == false;
    }
}
