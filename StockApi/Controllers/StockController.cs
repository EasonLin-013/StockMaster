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
        if (string.IsNullOrWhiteSpace(codes)) return BadRequest("請輸入代碼");

        // 格式化代碼：將 "2330,2317" 轉換成 "tse_2330.tw|tse_2317.tw"
        var formattedCodes = string.Join("|", codes.Split(',')
            .Select(c => $"tse_{c.Trim()}.tw"));

        var url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch={formattedCodes}";

        // --- 嘗試 1：乾淨無標頭 ---
        try
        {
            _logger.LogInformation("嘗試 1：使用無標頭模式...");
            var response = await _httpClient.GetStringAsync(url);
            return Content(response, "application/json");
        }
        catch (Exception ex1)
        {
            _logger.LogWarning($"嘗試 1 失敗: {ex1.Message}，準備切換偽裝模式...");

            // --- 嘗試 2：自動加入偽裝標頭 ---
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                // 模擬現代 Chrome 瀏覽器標頭
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Referer", "https://mis.twse.com.tw/");

                var response2 = await _httpClient.SendAsync(request);

                if (response2.IsSuccessStatusCode)
                {
                    var content = await response2.Content.ReadAsStringAsync();
                    _logger.LogInformation("嘗試 2 成功！證交所接受偽裝標頭。");
                    return Content(content, "application/json");
                }
                else
                {
                    // 如果兩次都失敗，回報最終狀態碼
                    return StatusCode((int)response2.StatusCode, 
                        $"[最終失敗] 證交所拒絕連線。狀態碼: {response2.StatusCode}。這代表 Render 的 IP 可能已被完全封鎖。");
                }
            }
            catch (Exception ex2)
            {
                return StatusCode(500, $"[全線崩潰] 兩次嘗試皆失敗。錯誤: {ex2.Message}");
            }
        }
    }
}
