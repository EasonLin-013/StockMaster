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

            // 不帶 start_date，讓 FinMind 自動回傳最新的一筆資料
            var priceUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockPrice&data_id={targetCode}";
            var bidAskUrl = $"https://api.finmindtrade.com/api/v4/data?dataset=TaiwanStockBestBidAsk&data_id={targetCode}";

            // 執行並發請求
            var priceTask = _httpClient.GetFromJsonAsync<FinMindPriceResponse>(priceUrl);
            var bidAskTask = _httpClient.GetFromJsonAsync<FinMindBidAskResponse>(bidAskUrl);

            await Task.WhenAll(priceTask, bidAskTask);

            var prices = priceTask.Result?.Data;
            var bidAsks = bidAskTask.Result?.Data;

            if (prices == null || !prices.Any()) 
                return NotFound(new { error = $"無法從 FinMind 取得 {targetCode} 的資料" });

            // 取得最新的成交價與五檔
            var lastPrice = prices.Last();
            var lastBA = bidAsks?.LastOrDefault();

            return Ok(new
            {
                msgArray = new[] {
                    new {
                        c = targetCode,
                        n = "Stock",
                        z = lastPrice.Close.ToString(),
                        h = lastPrice.Max.ToString(),
                        l = lastPrice.Min.ToString(),
                        y = lastPrice.Open.ToString(),
                        t = lastPrice.Date,
                        // 呼叫拼裝方法
                        b = FormatBidAsk(lastBA, "b"),
                        g = FormatBidAsk(lastBA, "g"),
                        a = FormatBidAsk(lastBA, "a"),
                        f = FormatBidAsk(lastBA, "f")
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"API 呼叫失敗: {ex.Message}" });
        }
    }

    private string FormatBidAsk(BidAskData? data, string type)
    {
        if (data == null) return "-_-_-_-_-";
        return type switch
        {
            "b" => $"{data.BidPrice1}_{data.BidPrice2}_{data.BidPrice3}_{data.BidPrice4}_{data.BidPrice5}_",
            "g" => $"{data.BidVolume1}_{data.BidVolume2}_{data.BidVolume3}_{data.BidVolume4}_{data.BidVolume5}_",
            "a" => $"{data.AskPrice1}_{data.AskPrice2}_{data.AskPrice3}_{data.AskPrice4}_{data.AskPrice5}_",
            "f" => $"{data.AskVolume1}_{data.AskVolume2}_{data.AskVolume3}_{data.AskVolume4}_{data.AskVolume5}_",
            _ => "-_-_-_-_-"
        };
    }

    public class FinMindPriceResponse { public List<PriceData>? Data { get; set; } }
    public class PriceData { 
        public string Date { get; set; } = ""; 
        public decimal Open { get; set; } 
        public decimal Max { get; set; } 
        public decimal Min { get; set; } 
        public decimal Close { get; set; } 
    }

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
