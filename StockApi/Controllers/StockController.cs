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

    // 透過建構函式注入 HttpClient
    public StockController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [HttpGet("{codes}")]
    public async Task<IActionResult> GetStock(string codes)
    {
        if (string.IsNullOrWhiteSpace(codes))
        {
            return BadRequest("請提供有效的股票代碼");
        }

        try
        {
            // 1. 格式化代碼：將 "2330,2317" 轉換成 "tse_2330.tw|tse_2317.tw"
            var formattedCodes = string.Join("|", codes.Split(',')
                .Select(c => $"tse_{c.Trim()}.tw"));

            // 2. 組成證交所原始即時 API 網址
            var url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch={formattedCodes}";

            // 3. 直接發送請求 (無自訂標頭版本)
            // 如果在 Render 上執行，請注意這行可能會因為 IP 被擋而導致 Exception
            var response = await _httpClient.GetStringAsync(url);

            // 4. 回傳原始 JSON 內容
            return Content(response, "application/json");
        }
        catch (HttpRequestException httpEx)
        {
            // 捕捉網路連線錯誤 (例如 403, 502 等)
            return StatusCode(500, $"證交所連線異常: {httpEx.Message}");
        }
        catch (Exception ex)
        {
            // 捕捉其他程式邏輯錯誤
            return StatusCode(500, $"伺服器內部錯誤: {ex.Message}");
        }
    }
}
