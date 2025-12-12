namespace TikTokDownloader.Models;

public class DownloadResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string FileName { get; init; } = "video.mp4";
    public string ContentType { get; init; } = "video/mp4";
    public byte[]? Content { get; init; }

    public static DownloadResult Failure(string error) => new() { Success = false, Error = error };

    public static DownloadResult Completed(string fileName, byte[] content, string contentType = "video/mp4") => new()
    {
        Success = true,
        FileName = fileName,
        Content = content,
        ContentType = contentType
    };
}
