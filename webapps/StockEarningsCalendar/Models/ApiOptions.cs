namespace StockEarningsCalendar.Models;

public class ApiOptions
{
    public string BaseUrl { get; set; } = "https://financialmodelingprep.com/api/";
    public string? ApiKey { get; set; }
    public int SectorTickerLimit { get; set; } = 50;
}
