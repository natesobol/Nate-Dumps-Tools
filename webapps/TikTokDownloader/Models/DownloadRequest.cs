using System.ComponentModel.DataAnnotations;

namespace TikTokDownloader.Models;

public class DownloadRequest
{
    [Required]
    [Url]
    [Display(Name = "TikTok video URL")]
    public string VideoUrl { get; set; } = string.Empty;

    [Display(Name = "Remove watermark (where supported)")]
    public bool RemoveWatermark { get; set; }

    [Required]
    [Display(Name = "Resolution")]
    public string Resolution { get; set; } = "720p";
}
