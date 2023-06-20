namespace ZhongHong.Common.Models;

public static class ZhongHongModelExtensions
{
    public static string ToHassAction(this ZhongHongUnitMode mode)
    {
        return mode switch
        {
            ZhongHongUnitMode.Cooling => "cooling",
            ZhongHongUnitMode.Drying => "drying",
            ZhongHongUnitMode.FanOnly => "fan",
            ZhongHongUnitMode.Heating => "heating",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    public static string ToHassFanMode(this ZhongHongFanMode fanMode)
    {
        return fanMode switch
        {
            ZhongHongFanMode.Auto => "auto",
            ZhongHongFanMode.High => "high",
            ZhongHongFanMode.Medium => "medium",
            ZhongHongFanMode.Low => "low",
            _ => "auto" // All unknwon fan mode treated as auto
        };
    }

    public static string ToHassMode(this ZhongHongUnitMode mode)
    {
        return mode switch
        {
            ZhongHongUnitMode.Cooling => "cool",
            ZhongHongUnitMode.Drying => "dry",
            ZhongHongUnitMode.FanOnly => "fan_only",
            ZhongHongUnitMode.Heating => "heat",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }
}