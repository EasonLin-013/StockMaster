using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace StockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly HttpClient _httpClient;
    // 建議去 FinMind 官網註冊一個免費用戶並拿到 Token 填在這裡
    private const string FinMindToken = ""; 

    public StockController(HttpClient httpClient) => _httpClient = httpClient;

    [HttpGet("{codes}")]
    public async Task<IActionResult> GetStock(string codes)
    {
        try
        {
            var targetCode = codes.Split(',')[0].Trim();
            var today = DateTime.Now.ToString("yyyy-MM-dd");

            // 1. 抓取當前成交價 (Tick)
            var tickUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockPriceTick&data_id={targetCode}&date={today}";
            
            // 2. 抓取最佳五檔 (BestBidAsk)
            var bidAskUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockBestBidAsk&data_id={targetCode}&date={today}";

            // 並行發送請求提高效率
            var tickTask = _httpClient.GetFromJsonAsync<FinMindTickResponse>(tickUrl);
            var bidAskTask = _httpClient.GetFromJsonAsync<FinMindBidAskResponse>(bidAskUrl);

            await Task.WhenAll(tickTask, bidAskTask);

            var ticks = tickTask.Result?.Data;
            var bidAsks = bidAskTask.Result?.Data;

            if (ticks == null || !ticks.Any()) return NotFound("找不到今日成交資料");

            var lastTick = ticks.Last();
            var lastBA = bidAsks?.LastOrDefault();

            // 3. 偽裝成證交所原始格式
            // 證交所格式範例: "b": "價1_價2_價3_價4_價5_", "g": "量1_量2_量3_量4_量5_"
            var result = new
            {
                msgArray = new List<object> {
                    new {
                        c = targetCode,
                        z = lastTick.DealPrice.ToString(),
                        h = ticks.Max(x => x.DealPrice).ToString(),
                        l = ticks.Min(x => x.DealPrice).ToString(),
                        y = ticks.First().DealPrice.ToString(),
                        t = lastTick.Time,
                        // 處理買進五檔
                        b = lastBA != null ? $"{lastBA.BidPrice1}_{lastBA.BidPrice2}_{lastBA.BidPrice3}_{lastBA.BidPrice4}_{lastBA.BidPrice5}_" : "-_-_-_-_-",
                        g = lastBA != null ? $"{lastBA.BidVolume1}_{lastBA.BidVolume2}_{lastBA.BidVolume3}_{lastBA.BidVolume4}_{lastBA.BidVolume5}_" : "-_-_-_-_-",
                        // 處理賣出五檔
                        a = lastBA != null ? $"{lastBA.AskPrice1}_{lastBA.AskPrice2}_{lastBA.AskPrice3}_{lastBA.AskPrice4}_{lastBA.AskPrice5}_" : "-_-_-_-_-",
                        f = lastBA != null ? $"{lastBA.AskVolume1}_{lastBA.AskVolume2}_{lastBA.AskVolume3}_{lastBA.AskVolume4}_{lastBA.AskVolume5}_" : "-_-_-_-_-",
                    }
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"抓取失敗: {ex.Message}" });
        }
    }

    // --- 資料結構定義 ---
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
