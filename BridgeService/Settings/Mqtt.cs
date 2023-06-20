namespace ZhongHong.BridgeService.Settings;

public class Mqtt
{
    public string Broker { get; set; }
    public bool HasCredential { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}