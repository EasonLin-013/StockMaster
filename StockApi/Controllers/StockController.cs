using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;

namespace StockAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private static string _manualBuffer = ""; 

        public StockController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            // 1. 優先檢查手動緩存
            if (!string.IsNullOrEmpty(_manualBuffer))
            {
                var data = _manualBuffer;
                _manualBuffer = ""; 
                return Content(data, "application/json");
            }

            // 2. 透過 Google Apps Script 抓取 (加強穩定版)
            try 
            {
                // 強制啟用重定向跟隨
                var handler = new HttpClientHandler() { 
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 10 
                };

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(20); // 給足 20 秒，避免 Render 機房反應慢

                // 加上 User-Agent，讓 Google 覺得這是正常請求
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                string gasUrl = $"https://script.google.com/macros/s/AKfycbyI-FVA1NPcxwrREPtd_FeuHEDAwZ0--YsHj1yZ0iPa5wLwEYjxohNVfuYUs_jYQ0Rk4g/exec?id={id}";

                // 執行請求
                var response = await client.GetAsync(gasUrl);
                
                // 如果 Google 回傳失敗 (如 404, 500)，直接拋出異常進入 catch
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                // 驗證內容是否為有效的證交所資料
                if (!string.IsNullOrEmpty(content) && content.Contains("msgArray"))
                {
                    Console.WriteLine($"[Stock:{id}] Google 代理抓取成功。");
                    return Content(content, "application/json");
                }
                
                throw new Exception("回傳內容不符合證交所格式");
            }
            catch (Exception ex)
            {
                // 當這裡發生錯誤時，前端就會拿到 502
                Console.WriteLine($"[Stock:{id}] 抓取異常: {ex.Message}");
                return StatusCode(502, new { message = "自動更新失敗，請改用書籤同步。" });
            }
        }

        [HttpPost("manual-update")]
        public IActionResult PostManualData([FromBody] JsonElement data)
        {
            _manualBuffer = data.GetRawText();
            return Ok(new { status = "success" });
        }
    }
}
