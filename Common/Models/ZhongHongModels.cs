using System.Text.Json.Serialization;

namespace ZhongHong.Common.Models;

public class ZhongHongUnitList
{
    [JsonPropertyName("err")]
    public int Err { get; set; }

    [JsonPropertyName("unit")]
    public IList<ZhongHongUnit> Unit { get; set; }
}

public class ZhongHongUnit
{
    [JsonPropertyName("oa")]
    public int OutdoorUnitId { get; set; }
    
    [JsonPropertyName("ia")]
    public int IndoorUnitId { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("on")]
    public ZhongHongUnitRunningState RunningState { get; set; }
    
    [JsonPropertyName("mode")]
    public ZhongHongUnitMode Mode { get; set; }
    
    [JsonPropertyName("tempSet")]
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public float SetPointTemperature { get; set; }
    
    [JsonPropertyName("tempIn")]
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public float EnvironmentTemperature { get; set; }
    
    [JsonPropertyName("fan")]
    public ZhongHongFanMode FanMode { get; set; }

    [JsonPropertyName("idx")]
    public int Index { get; set; }
    
    public string GetUniqueId()
    {
        return $"zhonghong_br_ac_{OutdoorUnitId}_{IndoorUnitId}";
    }
    
    public static string GetUniqueId(int outdoorUnitId, int indoorUnitId)
    {
        return $"zhonghong_br_ac_{outdoorUnitId}_{indoorUnitId}";
    }
}

public enum ZhongHongUnitRunningState
{
    Off = 0,
    On = 1,
}

public enum ZhongHongUnitMode
{
    Cooling = 0x01,
    Drying = 0x02,
    FanOnly = 0x04,
    Heating = 0x08,
}

public enum ZhongHongFanMode
{
    Auto = 0x00,
    High = 0x01,
    Medium = 0x02,
    Low = 0x04,
}