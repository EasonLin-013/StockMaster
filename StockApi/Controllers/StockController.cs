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

    public StockController(HttpClient httpClient, ILogger<StockController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [HttpGet("{codes}")]
    public async Task<IActionResult> GetStock(string codes)
    {
        // 【檢查點 1】如果畫面上看得到這行字，代表你的程式「真的有跑」且「更新成功」
        // 測試網址：你的網址/api/Stock/2330
        // return Ok($"Debug: 程式已啟動，收到代碼: {codes}。正在嘗試連線證交所...");

        if (string.IsNullOrWhiteSpace(codes))
        {
            return BadRequest("請提供有效的股票代碼");
        }

        var formattedCodes = string.Join("|", codes.Split(',')
            .Select(c => $"tse_{c.Trim()}.tw"));

        var url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch={formattedCodes}";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            // 模擬最完整的瀏覽器標頭
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            request.Headers.Add("Referer", "https://mis.twse.com.tw/");
            request.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9,en-US;q=0.8,en;q=0.7");

            // 設定連線逾時，避免 Render 網關等到起肖
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"證交所拒絕連線！狀態碼: {response.StatusCode}");
                // 如果這裡回傳了，前端應該會看到這串中文字
                return StatusCode((int)response.StatusCode, $"【自訂錯誤】證交所不給資料，代碼: {response.StatusCode}。這通常是 Render IP 被擋。");
            }

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, "連線證交所逾時（Timeout），對方可能正在擋 Render IP。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發生錯誤");
            return StatusCode(500, $"程式執行異常: {ex.Message}");
        }
    }
}
