using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TikTokDownloader.Models;
using TikTokDownloader.Services;

namespace TikTokDownloader.Pages;

public class IndexModel : PageModel
{
    private readonly TikTokDownloadService _downloadService;

    public IndexModel(TikTokDownloadService downloadService)
    {
        _downloadService = downloadService;
    }

    [BindProperty]
    public DownloadRequest Request { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please provide a valid TikTok URL and select a resolution.";
            return Page();
        }

        var result = await _downloadService.DownloadAsync(Request, HttpContext.RequestAborted);
        if (!result.Success || result.Content is null)
        {
            ErrorMessage = result.Error ?? "Something went wrong while preparing your download.";
            return Page();
        }

        return File(result.Content, result.ContentType, result.FileName);
    }
}
