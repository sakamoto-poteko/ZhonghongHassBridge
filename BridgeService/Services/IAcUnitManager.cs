using ZhongHong.Common.Models;

namespace ZhongHong.BridgeService.Services;

public interface IAcUnitManager
{
    public bool TryGetAcUnit(int outdoorId, int indoorId, out (ZhongHongUnit unit, DateTime updated)? _);
    public bool TryGetAcUnit(string uniqueId, out (ZhongHongUnit unit, DateTime updated)? _);
    public  void UpdateAcUnit(ZhongHongUnit acUnit);
}

public class AcUnitManager : IAcUnitManager
{
    private readonly Dictionary<string, (ZhongHongUnit unit, DateTime updated)> _acUnits = new();

    public bool TryGetAcUnit(int outdoorId, int indoorId, out  (ZhongHongUnit unit, DateTime updated)? val)
    {
        return TryGetAcUnit(ZhongHongUnit.GetUniqueId(outdoorId, indoorId), out val);
    }

    public bool TryGetAcUnit(string uniqueId, out (ZhongHongUnit unit, DateTime updated)? val)
    {
        if (_acUnits.TryGetValue(uniqueId, out var value))
        {
            val = value;
            return true;
        }
        else
        {
            val = null;
            return false;
        }       
    }

    public void UpdateAcUnit(ZhongHongUnit acUnit)
    {
        _acUnits[acUnit.GetUniqueId()] = (acUnit, DateTime.UtcNow);
    }
}