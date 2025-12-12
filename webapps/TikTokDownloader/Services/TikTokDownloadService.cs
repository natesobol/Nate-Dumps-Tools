using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TikTokDownloader.Models;
using TikTokDownloader.Options;

namespace TikTokDownloader.Services;

public class TikTokDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly TikTokDownloaderOptions _options;

    public TikTokDownloadService(HttpClient httpClient, IOptions<TikTokDownloaderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.VideoUrl))
        {
            return DownloadResult.Failure("A TikTok video URL is required.");
        }

        var payload = new
        {
            url = request.VideoUrl,
            resolution = request.Resolution,
            removeWatermark = request.RemoveWatermark
        };

        using var apiRequest = new HttpRequestMessage(HttpMethod.Post, _options.ApiEndpoint)
        {
            Content = JsonContent.Create(payload)
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            apiRequest.Headers.Add("X-API-Key", _options.ApiKey);
        }

        using var apiResponse = await _httpClient.SendAsync(apiRequest, cancellationToken);
        if (!apiResponse.IsSuccessStatusCode)
        {
            return DownloadResult.Failure($"The TikTok API rejected the request ({(int)apiResponse.StatusCode}).");
        }

        using var apiStream = await apiResponse.Content.ReadAsStreamAsync(cancellationToken);
        var apiPayload = await JsonSerializer.DeserializeAsync<ApiResponse>(apiStream, cancellationToken: cancellationToken);

        if (apiPayload is null || string.IsNullOrWhiteSpace(apiPayload.DownloadUrl))
        {
            return DownloadResult.Failure("The TikTok API did not return a download link.");
        }

        using var downloadResponse = await _httpClient.GetAsync(apiPayload.DownloadUrl, cancellationToken);
        if (!downloadResponse.IsSuccessStatusCode)
        {
            return DownloadResult.Failure("Unable to fetch the video from the download link provided by the API.");
        }

        var videoBytes = await downloadResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        var fileName = !string.IsNullOrWhiteSpace(apiPayload.FileName)
            ? apiPayload.FileName
            : _options.DefaultFileName;

        return DownloadResult.Completed(fileName, videoBytes, downloadResponse.Content.Headers.ContentType?.MediaType ?? "video/mp4");
    }

    private sealed class ApiResponse
    {
        public string? DownloadUrl { get; set; }
        public string? FileName { get; set; }
    }
}
