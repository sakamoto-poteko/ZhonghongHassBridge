using Microsoft.Extensions.Options;
using ZhongHong.BridgeService.Exceptions;
using ZhongHong.Common.Models;

namespace ZhongHong.BridgeService.Services.Implementations;

public class ZhongHongService : IZhongHongService
{
    private readonly IHttpPointNineClient _httpClient;
    private readonly IOptions<Settings.ZhongHong> _zhongHongSettings;
    private readonly ILogger<ZhongHongService> _logger;
    private readonly string _authorization;
    private readonly Uri _gatewayUrl;

    public ZhongHongService(IHttpPointNineClient httpClient, IOptions<Settings.ZhongHong> zhongHongSettings,
        ILogger<ZhongHongService> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = new Uri(zhongHongSettings.Value.GatewayUrl);
        _authorization =
            Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{zhongHongSettings.Value.UserName}:{zhongHongSettings.Value.Password}"));
        _zhongHongSettings = zhongHongSettings;
        _logger = logger;
    }

    public async Task<List<ZhongHongUnit>> ListUnitsAsync()
    {
        int p = 0;
        var units = new List<ZhongHongUnit>();
        do
        {
            try
            {
                var response =
                    await _httpClient.GetJsonAsync<ZhongHongUnitList>(
                        new Uri(_gatewayUrl, $"cgi-bin/api.html?f=17&p={p}"), _authorization);
                if (response.Unit.Count > 0)
                {
                    units.AddRange(response.Unit);
                    ++p;
                }
                else
                {
                    return units;
                }
            }
            catch (ZhongHongException e)
            {
                _logger.LogError(e, "zhonghong exception");
                return units;
            }
        } while (true);
    }

    public async Task UpdateUnitAsync(ZhongHongUnit unit)
    {
        var response = await _httpClient.GetAsync(
            new Uri(_gatewayUrl,
                $"cgi-bin/api.html?f=18&on={(int)unit.RunningState}&mode={(int)unit.Mode}&tempSet={(int)unit.SetPointTemperature}&fan={(int)unit.FanMode}&idx={unit.Index}"),
            _authorization);
        _logger.LogInformation("Updated AC unit {unit}", $"{unit.OutdoorUnitId}-{unit.IndoorUnitId}");
    }
}