using Microsoft.AspNetCore.Mvc;

namespace StockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public StockController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [HttpGet("{codes}")] // 這裡的 codes 現在會接收 "2330,2317"
    public async Task<IActionResult> GetStock(string codes)
    {
        // 1. 將前端傳來的 "2330,2317" 轉換成 "tse_2330.tw|tse_2317.tw"
        var formattedCodes = string.Join("|", codes.Split(',')
            .Select(c => $"tse_{c.Trim()}.tw"));

        // 2. 呼叫證交所 API (注意後面帶入 formattedCodes)
        var url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch={formattedCodes}";
        
        try
        {
            // 3. 建立 Request 並加入偽裝標頭 (User-Agent)
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            // 4. 發送請求並取得回應
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
