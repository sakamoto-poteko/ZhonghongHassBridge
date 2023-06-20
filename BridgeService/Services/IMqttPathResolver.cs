using ZhongHong.Common.Models;

namespace ZhongHong.BridgeService.Services;

public interface IMqttPathResolver
{
    public string GetBaseTopicForAirConditioner(ZhongHongUnit acUnit);
}

public class MqttPathResolver : IMqttPathResolver
{
    public string GetBaseTopicForAirConditioner(ZhongHongUnit acUnit)
    {
        return $"homeassistant/climate/zhonghong_br/ac_{acUnit.OutdoorUnitId}_{acUnit.IndoorUnitId}";
    }
}