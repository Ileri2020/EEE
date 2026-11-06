using Android.Content;
using Android.OS;
using Android.Provider;

namespace EEE.Platforms.Android;

internal static class AndroidDownloadSaver
{
    public static async Task SaveAsync(string sourcePath, string fileName)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
            throw new PlatformNotSupportedException("Downloads folder saving requires Android 10 or later.");

        var resolver = global::Android.App.Application.Context.ContentResolver
            ?? throw new InvalidOperationException("Android content resolver is unavailable.");
        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
        values.Put(MediaStore.IMediaColumns.MimeType, GetMimeType(fileName));
        values.Put(MediaStore.IMediaColumns.RelativePath, $"{global::Android.OS.Environment.DirectoryDownloads}/EEEngine");
        values.Put(MediaStore.IMediaColumns.IsPending, 1);

        var uri = resolver.Insert(MediaStore.Downloads.ExternalContentUri, values)
            ?? throw new IOException("Android could not create the file in Downloads/EEEngine.");

        try
        {
            await using (var input = File.OpenRead(sourcePath))
            await using (var output = resolver.OpenOutputStream(uri) ?? throw new IOException("Android could not open the output file."))
                await input.CopyToAsync(output);

            var publishedValues = new ContentValues();
            publishedValues.Put(MediaStore.IMediaColumns.IsPending, 0);
            resolver.Update(uri, publishedValues, null, null);
        }
        catch
        {
            resolver.Delete(uri, null, null);
            throw;
        }
    }

    private static string GetMimeType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".eee" or ".eesim" or ".json" => "application/json",
        ".py" => "text/x-python",
        ".cir" or ".sp" => "text/plain",
        ".kicad_sch" => "application/x-kicad-schematic",
        _ => "application/octet-stream"
    };
}
