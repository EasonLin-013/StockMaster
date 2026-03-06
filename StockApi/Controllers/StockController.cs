using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace StockApi.Controllers; // 請確認這裡的 Namespace 是否與你的專案一致

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public StockController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [HttpGet("{codes}")]
    public async Task<IActionResult> GetStock(string codes)
    {
        try
        {
            // 取得第一個代號 (例如 0050)
            var code = codes.Split(',')[0].Trim();
            
            // 1. 優先嘗試「上市」 (TPE)，強制要求繁體中文 hl=zh-TW
            var url = $"https://www.google.com/finance/quote/{code}:TPE?hl=zh-TW";
            var html = await FetchHtmlAsync(url);

            // 2. 如果 HTML 裡面找不到價格特徵，嘗試「上櫃」 (TWO)
            if (!IsPriceDataInHtml(html))
            {
                url = $"https://www.google.com/finance/quote/{code}:TWO?hl=zh-TW";
                html = await FetchHtmlAsync(url);
            }

            // --- 開始解析資料 ---

            // A. 抓取價格：優先尋找 data-last-price 屬性 (最準)
            var priceMatch = Regex.Match(html, @"data-last-price=""([\d,.]+)""");
            if (!priceMatch.Success)
            {
                // 次要方案：抓取 class="YMlS1d" 的內容
                priceMatch = Regex.Match(html, @"class=""YMlS1d"">\$?([\d,.]+)<");
            }

            // B. 抓取股票名稱 (class="zzDe0e")
            var nameMatch = Regex.Match(html, @"class=""zzDe0e"">([^<]+)<");

            // C. 抓取昨收價 (class="P66Qp")
            var prevMatch = Regex.Match(html, @"class=""P66Qp"">\$?([\d,.]+)<");

            // 判斷是否成功抓到關鍵價格
            if (!priceMatch.Success)
            {
                // 在 Render Logs 留下紀錄以便排錯
                Console.WriteLine($"[Error] 無法從 HTML 解析代碼 {code} 的價格");
                return NotFound(new { error = "無法解析價格，請檢查代號是否正確" });
            }

            var currentPrice = priceMatch.Groups[1].Value.Replace(",", "");
            var name = nameMatch.Success ? nameMatch.Groups[1].Value : "台股股票";
            var prevClose = prevMatch.Success ? prevMatch.Groups[1].Value.Replace(",", "") : currentPrice;

            // 3. 拼裝回傳符合你前端需要的 msgArray 結構
            return Ok(new
            {
                msgArray = new[] {
                    new {
                        c = code,
                        n = name,
                        z = currentPrice,           // 成交價
                        y = prevClose,              // 昨收價
                        h = "-", l = "-",           // 爬蟲版暫不提供最高/最低
                        t = DateTime.Now.ToString("HH:mm:ss"),
                        // 補足五檔佔位符，確保 Blazor 前端不報錯
                        b = "-_-_-_-_-", g = "-_-_-_-_-", a = "-_-_-_-_-", f = "-_-_-_-_-"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Render 執行錯誤: " + ex.Message });
        }
    }

    // 輔助方法：統一設定 Header 並抓取網頁
    private async Task<string> FetchHtmlAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        // 模擬真實瀏覽器，避免被 Google 封鎖
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9,en-US;q=0.8");
        
        var response = await _httpClient.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    // 輔助方法：判斷 HTML 是否包含價格資訊
    private bool IsPriceDataInHtml(string html)
    {
        return html.Contains("data-last-price") || html.Contains("YMlS1d");
    }
}
