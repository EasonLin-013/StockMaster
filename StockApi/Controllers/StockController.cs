using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace StockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public StockController(HttpClient httpClient) => _httpClient = httpClient;

    [HttpGet("{codes}")]
    public async Task<IActionResult> GetStock(string codes)
    {
        try
        {
            var targetCode = codes.Split(',')[0].Trim();
            
            // 嘗試抓取最近三天的資料 (避免週末或剛開盤沒資料的問題)
            var startDate = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd");

            // 1. 抓取成交明細 (Tick)
            var tickUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockPriceTick&data_id={targetCode}&start_date={startDate}";
            
            // 2. 抓取五檔資料 (BestBidAsk)
            var bidAskUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockBestBidAsk&data_id={targetCode}&start_date={startDate}";

            // 並行發送
            var tickTask = _httpClient.GetFromJsonAsync<FinMindTickResponse>(tickUrl);
            var bidAskTask = _httpClient.GetFromJsonAsync<FinMindBidAskResponse>(bidAskUrl);

            await Task.WhenAll(tickTask, bidAskTask);

            var ticks = tickTask.Result?.Data;
            var bidAsks = bidAskTask.Result?.Data;

            if (ticks == null || !ticks.Any()) 
                return NotFound(new { error = $"找不到 {targetCode} 的近期成交資料" });

            // 取得最後一筆成交與五檔
            var lastTick = ticks.Last();
            var lastBA = bidAsks?.LastOrDefault();

            // 3. 拼裝成前端需要的格式
            var result = new
            {
                msgArray = new List<object> {
                    new {
                        c = targetCode,
                        n = "股票名稱", // FinMind 此接口不含名稱，可自行補齊
                        z = lastTick.DealPrice.ToString(),
                        h = ticks.Max(x => x.DealPrice).ToString(),
                        l = ticks.Min(x => x.DealPrice).ToString(),
                        y = ticks.First().DealPrice.ToString(),
                        t = lastTick.Time,
                        // 拼裝買賣五檔，確保前端 Split('_') 正常
                        b = FormatBidAsk(lastBA, "bid_price"),
                        g = FormatBidAsk(lastBA, "bid_volume"),
                        a = FormatBidAsk(lastBA, "ask_price"),
                        f = FormatBidAsk(lastBA, "ask_volume")
                    }
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"系統異常: {ex.Message}" });
        }
    }

    private string FormatBidAsk(BidAskData? data, string type)
    {
        if (data == null) return "-_-_-_-_-";
        return type switch
        {
            "bid_price" => $"{data.BidPrice1}_{data.BidPrice2}_{data.BidPrice3}_{data.BidPrice4}_{data.BidPrice5}_",
            "bid_volume" => $"{data.BidVolume1}_{data.BidVolume2}_{data.BidVolume3}_{data.BidVolume4}_{data.BidVolume5}_",
            "ask_price" => $"{data.AskPrice1}_{data.AskPrice2}_{data.AskPrice3}_{data.AskPrice4}_{data.AskPrice5}_",
            "ask_volume" => $"{data.AskVolume1}_{data.AskVolume2}_{data.AskVolume3}_{data.AskVolume4}_{data.AskVolume5}_",
            _ => "-_-_-_-_-"
        };
    }

    public class FinMindTickResponse { public List<TickData>? Data { get; set; } }
    public class TickData { public string Time { get; set; } = ""; public decimal DealPrice { get; set; } }
    public class FinMindBidAskResponse { public List<BidAskData>? Data { get; set; } }
    public class BidAskData {
        public decimal BidPrice1 { get; set; } public decimal BidPrice2 { get; set; }
        public decimal BidPrice3 { get; set; } public decimal BidPrice4 { get; set; }
        public decimal BidPrice5 { get; set; }
        public int BidVolume1 { get; set; } public int BidVolume2 { get; set; }
        public int BidVolume3 { get; set; } public int BidVolume4 { get; set; }
        public int BidVolume5 { get; set; }
        public decimal AskPrice1 { get; set; } public decimal AskPrice2 { get; set; }
        public decimal AskPrice3 { get; set; } public decimal AskPrice4 { get; set; }
        public decimal AskPrice5 { get; set; }
        public int AskVolume1 { get; set; } public int AskVolume2 { get; set; }
        public int AskVolume3 { get; set; } public int AskVolume4 { get; set; }
        public int AskVolume5 { get; set; }
    }
}
