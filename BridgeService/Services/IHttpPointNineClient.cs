using System.Text.Json.Serialization;

namespace ZhongHong.BridgeService.Services;

public interface IHttpPointNineClient
{
    public Task<string> GetAsync(Uri uri, string auth);
    public Task<T> GetJsonAsync<T>(Uri uri, string auth);
}