using TikTokDownloader.Options;
using TikTokDownloader.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TikTokDownloaderOptions>(builder.Configuration.GetSection("TikTokDownloader"));
builder.Services.AddHttpClient<TikTokDownloadService>();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();

app.Run();
