using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace StockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly HttpClient _httpClient;
    // 請在此處填入你申請到的 Fugle API Key
    private const string FugleApiKey = "MDU2NjM1MWItYjA2MS00NmQ5LTkwMWItMjZhNDFhNDFiZDQzIDRhYjJmYTAyLWQ4NjAtNDVjOS1hNzNkLTc3OTg4NzBhMGVmZQ==";

    public StockController(HttpClient httpClient) => _httpClient = httpClient;

    [HttpGet("{codes}")]
    public async Task<IActionResult> GetStock(string codes)
    {
        try
        {
            var targetCode = codes.Split(',')[0].Trim();
            
            // 富果 API 網址：抓取盤中快照 (Snapshot)
            var url = $"https://api.fugle.tw/marketdata/v1.0/stock/snapshot/{targetCode}";

            // 必須帶上 API Key 標頭
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Fugle-Api-Key", FugleApiKey);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new { error = $"富果 API 回報錯誤: {response.StatusCode}" });
            }

            var data = await response.Content.ReadFromJsonAsync<FugleSnapshot>();

            if (data == null) return NotFound();

            // 3. 完美拼裝為你的 Blazor 前端格式
            var result = new
            {
                msgArray = new[] {
                    new {
                        c = data.Symbol,           // 代碼
                        n = data.Name,             // 名稱
                        z = data.LastTrade.Price.ToString(), // 成交價
                        h = data.High.ToString(),            // 最高
                        l = data.Low.ToString(),             // 最低
                        y = data.PreviousClose.ToString(),   // 昨收
                        t = data.LastTrade.Time,
                        // 富果的五檔資料拼裝
                        b = string.Join("_", data.Bids.Select(x => x.Price)) + "_",
                        g = string.Join("_", data.Bids.Select(x => x.Size)) + "_",
                        a = string.Join("_", data.Asks.Select(x => x.Price)) + "_",
                        f = string.Join("_", data.Asks.Select(x => x.Size)) + "_",
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

    // --- 富果資料結構 ---
    public class FugleSnapshot {
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal PreviousClose { get; set; }
        public LastTrade LastTrade { get; set; } = new();
        public List<BidAsk> Bids { get; set; } = new();
        public List<BidAsk> Asks { get; set; } = new();
    }
    public class LastTrade { public decimal Price { get; set; } public string Time { get; set; } = ""; }
    public class BidAsk { public decimal Price { get; set; } public int Size { get; set; } }
}
