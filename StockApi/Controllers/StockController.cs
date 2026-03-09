using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace StockAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        // 靜態變數，儲存手動同步回來的最新資料
        private static string _manualBuffer = "";

        public StockController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            // 1. 優先檢查緩存中是否有手動同步的資料
            if (!string.IsNullOrEmpty(_manualBuffer))
            {
                var data = _manualBuffer;
                _manualBuffer = ""; // 取用後清除，確保下次抓取是新鮮的
                Console.WriteLine($"[Stock:{id}] 使用手動同步緩存資料回傳。");
                return Content(data, "application/json");
            }

            // 2. 透過 corsproxy.io 備援抓取
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5); // 增加一點超時時間，因為經過 Proxy 會稍慢

                // 構造原始證交所網址
                string targetUrl = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch=tse_{id}.tw";
                
                // 構造 Proxy 網址，使用 Uri.EscapeDataString 確保網址內的特殊字元被正確編碼
                string proxyUrl = $"https://corsproxy.io/?{Uri.EscapeDataString(targetUrl)}";

                var response = await client.GetStringAsync(proxyUrl);

                // 檢查回傳內容是否包含基本 JSON 格式
                if (response.Contains("msgArray"))
                {
                    Console.WriteLine($"[Stock:{id}] 透過 CORS Proxy 抓取成功。");
                    return Content(response, "application/json");
                }
                
                throw new Exception("Proxy 回傳內容不完整");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Stock:{id}] 自動抓取失敗 (含 Proxy): {ex.Message}");
                // 若 Proxy 也失敗，則回傳 502 讓前端引導使用者點擊書籤
                return StatusCode(502, new { message = "伺服器與代理皆遭封鎖，請使用書籤工具手動同步。" });
            }
        }

        // 書籤小工具 POST 資料的入口
        [HttpPost("manual-update")]
        public IActionResult PostManualData([FromBody] JsonElement data)
        {
            _manualBuffer = data.GetRawText();
            Console.WriteLine("--- 收到來自客戶端的同步資料 ---");
            return Ok(new { status = "success" });
        }
    }
}
