using ZhongHong.BridgeService.Services.Implementations;
using ZhongHong.BridgeService.Services;

namespace ZhongHong.BridgeService;

public class Program
{
    public static void Main(string[] args)
    {
        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                var configuration = hostContext.Configuration;
                services.AddTransient<IHttpPointNineClient, HttpPointNineClient>();
                services.AddTransient<IMqttPathResolver, MqttPathResolver>();
                services.AddSingleton<IZhongHongService, ZhongHongService>();
                services.AddSingleton<HassMqttService>();
                services.AddSingleton<IAcUnitManager, AcUnitManager>();
                services.AddHostedService<HassMqttService>();
                services.Configure<Settings.ZhongHong>(configuration.GetSection("ZhongHong"));
                services.Configure<Settings.Mqtt>(configuration.GetSection("Mqtt"));
            })
            .Build();

        host.Run();
    }
}