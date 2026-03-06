[HttpGet("{codes}")]
public async Task<IActionResult> GetStock(string codes)
{
    var formattedCodes = string.Join("|", codes.Split(',')
        .Select(c => $"tse_{c.Trim()}.tw"));

    var url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch={formattedCodes}";
    
    try
    {
        // 1. 嘗試抓取
        var response = await _httpClient.GetStringAsync(url);
        return Content(response, "application/json");
    }
    catch (HttpRequestException e)
    {
        // 2. 如果是網路請求錯誤 (例如證交所回傳 403 或 502)
        return StatusCode(500, $"證交所連線失敗: {e.StatusCode} - {e.Message}");
    }
    catch (TaskCanceledException)
    {
        // 3. 如果是逾時
        return StatusCode(500, "證交所回應逾時 (Timeout)");
    }
    catch (Exception ex)
    {
        // 4. 其他未知的程式錯誤
        return StatusCode(500, $"發生未預期錯誤: {ex.GetType().Name} - {ex.Message}");
    }
}
