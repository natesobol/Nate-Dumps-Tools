namespace TikTokDownloader.Options;

public class TikTokDownloaderOptions
{
    public string ApiEndpoint { get; set; } = "https://api.example.com/tiktok/download";
    public string ApiKey { get; set; } = "YOUR_API_KEY";
    public string DefaultFileName { get; set; } = "tiktok-video.mp4";
}
