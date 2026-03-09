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

            // 2. 自動抓取備援 (雖然目前 Render 被擋 502，但留著當作保險)
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                string url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch=tse_{id}.tw";
                var response = await client.GetStringAsync(url);
                return Content(response, "application/json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Stock:{id}] 自動抓取失敗: {ex.Message}");
                return StatusCode(502, new { message = "伺服器遭封鎖，請使用書籤工具同步。" });
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
