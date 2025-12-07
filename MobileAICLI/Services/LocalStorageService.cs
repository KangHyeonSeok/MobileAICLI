using Microsoft.JSInterop;
using System.Text.Json;

namespace MobileAICLI.Services;

/// <summary>
/// 브라우저 LocalStorage에 데이터를 저장/로드하는 서비스
/// </summary>
public class LocalStorageService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<LocalStorageService> _logger;

    public LocalStorageService(IJSRuntime jsRuntime, ILogger<LocalStorageService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// LocalStorage에 데이터 저장
    /// </summary>
    public async Task SetItemAsync<T>(string key, T value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save item to LocalStorage: {Key}", key);
        }
    }

    /// <summary>
    /// LocalStorage에서 데이터 로드
    /// </summary>
    public async Task<T?> GetItemAsync<T>(string key)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
            if (string.IsNullOrEmpty(json))
            {
                return default;
            }
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load item from LocalStorage: {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// LocalStorage에서 항목 제거
    /// </summary>
    public async Task RemoveItemAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove item from LocalStorage: {Key}", key);
        }
    }

    /// <summary>
    /// LocalStorage 전체 삭제
    /// </summary>
    public async Task ClearAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.clear");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear LocalStorage");
        }
    }
}
