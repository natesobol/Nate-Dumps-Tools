using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockEarningsCalendar.Models;
using StockEarningsCalendar.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("FinancialModelingPrep"));
builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<ApiOptions>>().Value;
    options.ApiKey ??= builder.Configuration["FMP_API_KEY"];
    return options;
});

builder.Services.AddHttpClient<FinancialModelingPrepClient>();
builder.Services.AddScoped<EarningsService>();
builder.Services.AddSingleton<CalendarExporter>();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/docs"));

app.MapGet("/docs", () => Results.Json(new
{
    message = "Stock Earnings Calendar Downloader API",
    routes = new Dictionary<string, string>
    {
        ["GET /api/earnings"] = "Fetch earnings for tickers or sectors",
        ["GET /api/export/csv"] = "Download earnings watchlist as CSV",
        ["GET /api/export/ics"] = "Download earnings calendar as ICS"
    },
    environment = "Set FMP_API_KEY to call the Financial Modeling Prep API"
}));

app.MapGet("/api/earnings", async ([FromQuery] string? tickers, [FromQuery] string? sectors, [FromQuery] string? sortBy, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, EarningsService service, CancellationToken cancellationToken) =>
{
    var request = EarningsRequest.FromQuery(tickers, sectors, sortBy, from, to);
    var events = await service.GetEarningsAsync(request, cancellationToken);
    return Results.Ok(events);
});

app.MapGet("/api/export/csv", async ([FromQuery] string? tickers, [FromQuery] string? sectors, [FromQuery] string? sortBy, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, EarningsService service, CalendarExporter exporter, CancellationToken cancellationToken) =>
{
    var request = EarningsRequest.FromQuery(tickers, sectors, sortBy, from, to);
    var events = await service.GetEarningsAsync(request, cancellationToken);
    var csv = exporter.ToCsv(events);
    return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "earnings.csv");
});

app.MapGet("/api/export/ics", async ([FromQuery] string? tickers, [FromQuery] string? sectors, [FromQuery] string? sortBy, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? calendarName, EarningsService service, CalendarExporter exporter, CancellationToken cancellationToken) =>
{
    var request = EarningsRequest.FromQuery(tickers, sectors, sortBy, from, to);
    var events = await service.GetEarningsAsync(request, cancellationToken);
    var ics = exporter.ToIcs(events, string.IsNullOrWhiteSpace(calendarName) ? "Earnings Watchlist" : calendarName);
    return Results.File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar", "earnings.ics");
});

app.Run();
