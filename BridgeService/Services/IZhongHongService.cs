using ZhongHong.Common;
using ZhongHong.Common.Models;

namespace ZhongHong.BridgeService.Services;

public interface IZhongHongService
{
    public Task<List<ZhongHongUnit>> ListUnitsAsync();
    public Task UpdateUnitAsync(ZhongHongUnit unit);
}