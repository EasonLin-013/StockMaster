using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

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
            var code = codes.Split(',')[0].Trim();
            
            // 建立請求，模擬瀏覽器行為
            var url = $"https://www.google.com/finance/quote/{code}:TPE";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9");

            var response = await _httpClient.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            // 如果上市 (TPE) 找不到，嘗試上櫃 (TWO)
            if (!html.Contains("YMlS1d") && !html.Contains("data-last-price"))
            {
                url = $"https://www.google.com/finance/quote/{code}:TWO";
                html = await _httpClient.GetStringAsync(url);
            }

            // --- 核心邏輯：從你上傳的那堆 HTML 中提取數據 ---
            
            // 1. 抓取價格 (Regex 針對 Google Finance 結構)
            var priceMatch = Regex.Match(html, @"class=""YMlS1d"">\$?([\d,.]+)<");
            // 2. 抓取股票名稱
            var nameMatch = Regex.Match(html, @"class=""zzDe0e"">([^<]+)<");
            // 3. 抓取昨收價 (用於計算漲跌)
            var prevMatch = Regex.Match(html, @"class=""P66Qp"">\$?([\d,.]+)<");

            if (!priceMatch.Success) 
                return NotFound(new { error = "無法解析價格，請檢查代號是否正確" });

            var currentPrice = priceMatch.Groups[1].Value.Replace(",", "");
            var prevClose = prevMatch.Success ? prevMatch.Groups[1].Value.Replace(",", "") : currentPrice;

            // 拼裝成你前端需要的格式
            return Ok(new
            {
                msgArray = new[] {
                    new {
                        c = code,
                        n = nameMatch.Success ? nameMatch.Groups[1].Value : "台股股票",
                        z = currentPrice,           // 當前成交價
                        y = prevClose,              // 昨收價
                        h = "-", l = "-",           // 網頁版較難精準抓到最高/最低，先以 - 代替
                        t = DateTime.Now.ToString("HH:mm:ss"),
                        // 補足五檔佔位符，確保 Blazor 前端不報錯
                        b = "-_-_-_-_-", g = "-_-_-_-_-", a = "-_-_-_-_-", f = "-_-_-_-_-"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Render 抓取錯誤: " + ex.Message });
        }
    }
}
