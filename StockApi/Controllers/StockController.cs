using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Web; // 用於 HttpUtility.UrlEncode

namespace StockAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public StockController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                // 必須模擬真實瀏覽器，否則 Web Proxy 可能會拒絕服務
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                // 1. 準備目標網址
                string targetUrl = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch=tse_{id}.tw";
                string encodedUrl = HttpUtility.UrlEncode(targetUrl);

                // 2. 構造 FilterBypass 請求 (參數 k 是目標網址)
                // b=4 代表伺服器節點，可以根據穩定度調整
                string proxyUrl = $"https://www.filterbypass.me/s.php?k={encodedUrl}&b=4";

                Console.WriteLine($"[Stock:{id}] 透過 FilterBypass 發起請求...");

                var response = await client.GetAsync(proxyUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    // 3. 關鍵：從 HTML 內容中提取 JSON
                    // 證交所資料開頭固定是 {"msgArray"
                    int jsonStart = content.IndexOf("{\"msgArray\"");
                    if (jsonStart >= 0)
                    {
                        // 找到 JSON 的結尾
                        int jsonEnd = content.LastIndexOf("}");
                        string pureJson = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                        
                        Console.WriteLine($"[Stock:{id}] 成功提取資料");
                        return Content(pureJson, "application/json");
                    }
                    
                    Console.WriteLine($"[Stock:{id}] 回傳內容中找不到 JSON (可能遇到驗證碼)");
                }

                return StatusCode(500, new { message = "透過 FilterBypass 抓取失敗" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
