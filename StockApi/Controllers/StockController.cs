using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;

namespace StockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StockController> _logger;

    // 透過 DI 注入 HttpClient 與 Logger
    public StockController(HttpClient httpClient, ILogger<StockController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// 取得股票資訊
    /// 前端傳入範例: api/Stock/2330,2317
    /// </summary>
    [HttpGet("{codes}")]
    public async Task<IActionResult> GetStock(string codes)
    {
        if (string.IsNullOrWhiteSpace(codes))
        {
            return BadRequest("請提供有效的股票代碼");
        }

        // 1. 將前端傳來的 "2330,2317" 轉換成 "tse_2330.tw|tse_2317.tw"
        var formattedCodes = string.Join("|", codes.Split(',')
            .Select(c => $"tse_{c.Trim()}.tw"));

        // 2. 準備證交所 API URL
        var url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch={formattedCodes}";

        try
        {
            // 3. 建立 HttpRequestMessage 並設定標頭
            // 這是避免 502 Bad Gateway (被判定為爬蟲) 的關鍵
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            // 模擬一般 Chrome 瀏覽器的請求標頭
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            request.Headers.Add("Referer", "https://mis.twse.com.tw/");
            request.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9,en-US;q=0.8,en;q=0.7");
            request.Headers.Add("Connection", "keep-alive");

            // 4. 發送請求
            var response = await _httpClient.SendAsync(request);

            // 5. 檢查回應狀態
            if (!response.IsSuccessStatusCode)
            {
                // 如果證交所回傳 502 或 403，我們會抓到詳細代碼而不是直接崩潰
                _logger.LogWarning($"證交所 API 請求失敗，狀態碼: {response.StatusCode}");
                return StatusCode((int)response.StatusCode, $"證交所 API 拒絕請求，請檢查是否被封鎖 IP。狀態碼: {response.StatusCode}");
            }

            // 6. 讀取內容並回傳
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "網路連線異常");
            return StatusCode(503, $"無法連線至證交所 API: {httpEx.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發生未預期的錯誤");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
