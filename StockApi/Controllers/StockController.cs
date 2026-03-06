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

    public StockController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [HttpGet("{codes}")]
    public async Task<IActionResult> GetStock(string codes)
    {
        // 1. 格式化代碼
        var formattedCodes = string.Join("|", codes.Split(',')
            .Select(c => $"tse_{c.Trim()}.tw"));

        // 2. 組成原始網址
        var url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch={formattedCodes}";
        
        try
        {
            // 3. 直接使用 GetStringAsync，不添加任何 HttpRequestMessage 或 Header
            // 這是你之前可以成功運行的最簡化寫法
            var response = await _httpClient.GetStringAsync(url);

            // 4. 將抓到的 JSON 直接丟回給前端
            return Content(response, "application/json");
        }
        catch (Exception ex)
        {
            // 捕捉任何連線或是逾時錯誤
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
