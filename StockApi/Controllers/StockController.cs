using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace StockAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        // 使用 static 確保所有請求共用 Proxy 池，避免重複抓取清單
        private static List<string> _proxyPool = new();
        private readonly Random _random = new();

        public StockController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            // 1. 如果 Proxy 池是空的，先去抓一份清單
            if (!_proxyPool.Any())
            {
                await RefreshProxyPool();
            }

            // 2. 嘗試抓取股票資料（包含重試邏輯）
            string jsonResult = await FetchWithRetry(id, 0);

            if (string.IsNullOrEmpty(jsonResult))
            {
                return StatusCode(500, new { message = "Proxy 連線全數失敗，請稍後再試" });
            }

            return Content(jsonResult, "application/json");
        }

        private async Task RefreshProxyPool()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                // 從 ProxyScrape 抓取免費的 HTTP Proxy 清單
                var response = await client.GetStringAsync("https://api.proxyscrape.com/v2/?request=displayproxies&protocol=http&timeout=5000&country=all&ssl=yes");
                _proxyPool = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            catch
            {
                _proxyPool = new List<string>();
            }
        }

        private async Task<string> FetchWithRetry(string stockId, int retryCount)
        {
            // 限制最多重試 3 次，避免無限迴圈
            if (retryCount >= 3 || !_proxyPool.Any()) return null;

            string selectedProxy = _proxyPool[_random.Next(_proxyPool.Count)];

            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy(selectedProxy),
                UseProxy = true,
                // 忽略免費 Proxy 常見的憑證問題
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using var proxyClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };

            try
            {
                string url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch=tse_{stockId}.tw";
                proxyClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                return await proxyClient.GetStringAsync(url);
            }
            catch
            {
                // 失敗時，從池中移除失效的 Proxy 並進行下一次重試
                _proxyPool.Remove(selectedProxy);
                return await FetchWithRetry(stockId, retryCount + 1);
            }
        }
    }
}
