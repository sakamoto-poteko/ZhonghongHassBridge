using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Client;
using ZhongHong.BridgeService.Exceptions;
using ZhongHong.Common.Models;

namespace ZhongHong.BridgeService.Services.Implementations;

public partial class HassMqttService : IHostedService
{
    private readonly Settings.Mqtt _mqttSettings;
    private readonly MqttFactory _mqttFactory;
    private readonly IZhongHongService _zhonghongService;
    private readonly IAcUnitManager _acUnitManager;
    private readonly IMqttPathResolver _mqttPathResolver;
    private readonly ILogger<HassMqttService> _logger;
    private readonly EventWaitHandle _reconnectMqttSignal = new(false, EventResetMode.AutoReset);

    private Task? _zhonghongVrfPullerTask;
    private Task? _mqttConnectionMaintainerTask;
    private CancellationTokenSource? _stoppingCts;

    private static readonly Regex UnitIdRegex = GetUnitIdRegex();
    private IMqttClient? _mqttClient;

    public HassMqttService(
        IMqttPathResolver mqttPathResolver,
        IOptions<Settings.Mqtt> mqttSetting,
        IZhongHongService zhonghongService,
        IAcUnitManager acUnitManager,
        ILogger<HassMqttService> logger)
    {
        _mqttPathResolver = mqttPathResolver;
        _mqttFactory = new MqttFactory();
        _mqttSettings = mqttSetting.Value;
        _zhonghongService = zhonghongService;
        _acUnitManager = acUnitManager;
        _logger = logger;
    }

    #region Setup

    private async Task ConnectHassMqttAsync()
    {
        try
        {
            if (_mqttClient == null)
            {
                var mqttSubscribeOptions = new MqttFactory().CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(f => { f.WithTopic("homeassistant/climate/zhonghong_br/+/mode/set"); })
                    .WithTopicFilter(f => { f.WithTopic("homeassistant/climate/zhonghong_br/+/fan_mode/set"); })
                    .WithTopicFilter(f => { f.WithTopic("homeassistant/climate/zhonghong_br/+/temp/set"); })
                    .Build();

                var mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_mqttSettings.Broker);

                if (_mqttSettings.HasCredential)
                {
                    mqttClientOptionsBuilder =
                        mqttClientOptionsBuilder.WithCredentials(_mqttSettings.Username, _mqttSettings.Password);
                }

                var mqttClient = _mqttFactory.CreateMqttClient();
                await mqttClient.ConnectAsync(mqttClientOptionsBuilder.Build(), CancellationToken.None);

                mqttClient.ApplicationMessageReceivedAsync += MqttClientOnApplicationMessageReceivedAsync;
                mqttClient.DisconnectedAsync += MqttClientOnDisconnectedAsync;
                await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);

                _mqttClient = mqttClient;
                // decrease semaphore to hold off reconnecting
            }
            else
            {
                _logger.LogError("MQTT client already created");
            }
        }
        finally
        {
            // post the semaphore when there's an exception so it can proceed with reconnecting
        }
    }

    private async Task DisconnectHassMqttAsync()
    {
        if (_mqttClient != null)
        {
            await _mqttClient.DisconnectAsync();
            _mqttClient.Dispose();
            _mqttClient = null;
        }
    }

    private Task MqttClientOnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
    {
        _logger.LogWarning("Hass MQTT subscriber disconnected. Try reconnecting ...");
        // signaling the reconnection
        _reconnectMqttSignal.Set();
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var connectTask =
            _mqttConnectionMaintainerTask = RunMqttConnectionMaintainer(_stoppingCts.Token);

        _zhonghongVrfPullerTask = RunZhonghongPuller(_stoppingCts.Token);
        return connectTask;
    }

    private async Task RunMqttConnectionMaintainer(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Connecting mqtt...");
                await ConnectHassMqttAsync();
                _logger.LogInformation("Hass MQTT subscriber connected");
                // wait until reconnecting signal
                // if failed connecting, exception will be thrown hence no wait
                _logger.LogDebug("Wait for reconnecting signal...");
                WaitHandle.WaitAny(new[] { _reconnectMqttSignal, cancellationToken.WaitHandle });
                _logger.LogDebug("Reconnecting signal received. Disconnecting...");
                await DisconnectHassMqttAsync();
            }
            catch (Exception ex) when (ex is MqttConnectingFailedException or SocketException)
            {
                _logger.LogError("Failed to connecting MQTT: {ErrorMessage}", ex.Message);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception occurred when connecting/disconnecting MQTT");
            }

            // TODO: configurable delay
            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(3000), cancellationToken);
            }
        }
    }

    private async Task RunZhonghongPuller(CancellationToken cancellationToken)
    {
        var lastConfigPublish = new Dictionary<(int, int), DateTime>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var list = await _zhonghongService.ListUnitsAsync();
                _logger.LogDebug("Enumerated {CountDevices} devices", list.Count);

                foreach (var acUnit in list)
                {
                    _acUnitManager.UpdateAcUnit(acUnit);

                    var key = (acUnit.OutdoorUnitId, acUnit.IndoorUnitId);
                    // publish auto config every 2s
                    // longer is fine, say your delay is >2s
                    if (!lastConfigPublish.ContainsKey(key) ||
                        DateTime.Now - lastConfigPublish[key] > TimeSpan.FromSeconds(2))
                    {
                        await PublishAirConditionerAutoDiscoverPayloadAsync(acUnit);
                        _logger.LogDebug("Published auto discover config for {DeviceUniqueId}", acUnit.GetUniqueId());
                        lastConfigPublish[key] = DateTime.Now;
                    }

                    await PublishAirConditionerStatesAsync(acUnit);
                    _logger.LogDebug("Published states for {DeviceUniqueId}", acUnit.GetUniqueId());
                }
            }
            catch (ZhongHongException e)
            {
                _logger.LogError(e, "ZhongHong failure occured in pulling task");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "A general failure occured in pulling task");
            }

            // TODO: configurable delay. minimum 500
            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // this method is for the sake of completeness. who the hell would stop it? 
            var tasks = new List<Task>();
            _logger.LogDebug("Stopping HassMqttService...");
            if (_zhonghongVrfPullerTask != null)
            {
                tasks.Add(_zhonghongVrfPullerTask);
            }

            if (_mqttConnectionMaintainerTask != null)
            {
                tasks.Add(_mqttConnectionMaintainerTask);
            }

            await Task.WhenAll(tasks);
            _logger.LogDebug("Tasks stopped. Disconnecting mqtt...");
            await DisconnectHassMqttAsync();
            _logger.LogDebug("HassMqttService stopped");
        }
        catch (TaskCanceledException e)
        {
            _logger.LogDebug("Task cancelled: {Error}", e.ToString());
        }
    }

    #endregion


    #region Subscriber

    private async Task MqttClientOnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        string? topic = arg.ApplicationMessage.Topic;
        string payload = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
        _logger.LogInformation("Received HVAC update command: {Topic} {Payload}", topic, payload);
        var match = UnitIdRegex.Match(topic);
        if (match.Groups["oa"].Success && match.Groups["ia"].Success)
        {
            if (int.TryParse(match.Groups["oa"].Value, out int oa) &&
                int.TryParse(match.Groups["ia"].Value, out int ia))
            {
                var t = topic switch
                {
                    _ when topic.EndsWith("/mode/set") => ProcessModeSetMessageAsync(oa, ia, payload),
                    _ when topic.EndsWith("/fan_mode/set") => ProcessFanModeSetMessageAsync(oa, ia, payload),
                    _ when topic.EndsWith("/temp/set") => ProcessTemperatureSetMessageAsync(oa, ia, payload),
                    _ => throw new ZhongHongException("MQTT exception is unknown")
                };

                await t;
            }
        }

        arg.IsHandled = true;
    }

    private async Task ProcessTemperatureSetMessageAsync(int oa, int ia, string payload)
    {
        _logger.LogInformation("Processing temp set for ({Oa}, {Ia}): {Payload}", oa, ia, payload);

        await SendUpdatesToZhonghong(oa, ia, payload, unit =>
        {
            if (float.TryParse(payload, out float temperature))
            {
                unit.SetPointTemperature = temperature;
            }
        });
    }

    private async Task ProcessFanModeSetMessageAsync(int oa, int ia, string payload)
    {
        _logger.LogInformation("Processing fan mode set for ({Oa}, {Ia}): {Payload}", oa, ia, payload);

        await SendUpdatesToZhonghong(oa, ia, payload, unit =>
        {
            unit.FanMode = payload switch
            {
                "auto" => ZhongHongFanMode.Auto,
                "high" => ZhongHongFanMode.High,
                "medium" => ZhongHongFanMode.Medium,
                "low" => ZhongHongFanMode.Low,
                _ => throw new ArgumentOutOfRangeException(nameof(payload), payload, null)
            };
        });
    }

    private async Task ProcessModeSetMessageAsync(int oa, int ia, string payload)
    {
        _logger.LogInformation("Processing mode set for ({Oa}, {Ia}): {Payload}", oa, ia, payload);

        await SendUpdatesToZhonghong(oa, ia, payload, unit =>
        {
            unit.Mode = payload switch
            {
                "cool" => ZhongHongUnitMode.Cooling,
                "dry" => ZhongHongUnitMode.Drying,
                "heat" => ZhongHongUnitMode.Heating,
                "fan_only" => ZhongHongUnitMode.FanOnly,
                "off" => unit.Mode,
                _ => throw new ArgumentOutOfRangeException(nameof(payload), payload, null)
            };

            unit.RunningState = payload == "off" ? ZhongHongUnitRunningState.Off : ZhongHongUnitRunningState.On;
        });
    }

    private async Task SendUpdatesToZhonghong(int oa, int ia, string payload, Action<ZhongHongUnit> updateAction)
    {
        if (_acUnitManager.TryGetAcUnit(oa, ia, out (ZhongHongUnit unit, DateTime lastUpdated)? val))
        {
            if (val != null && DateTime.UtcNow - val.Value.lastUpdated < TimeSpan.FromSeconds(5))
            {
                var unit = val.Value.unit;
                updateAction(unit);
                await _zhonghongService.UpdateUnitAsync(unit);
            }
            else
            {
                _logger.LogError("Received update for stale unit ({Oa}, {Ia}), last updated on {LastUpdated}", oa, ia, val.HasValue ? val.Value.lastUpdated.ToString("O") : "N/A");
            }
        }
        else
        {
            _logger.LogError("Requested to send update to ({Oa}, {Ia}) but it's not found in unit manager", oa, ia);
        }
    }

    [GeneratedRegex("homeassistant/climate/zhonghong_br/ac_(?<oa>\\d+)_(?<ia>\\d+)/.*$", RegexOptions.Compiled)]
    private static partial Regex GetUnitIdRegex();

    #endregion


    #region Publisher

    private async Task PublishAirConditionerStatesAsync(ZhongHongUnit acUnit)
    {
        // fan_mode_state_topic
        var fanModeMessage = new MqttApplicationMessage()
        {
            Topic = $"{_mqttPathResolver.GetBaseTopicForAirConditioner(acUnit)}/fan_mode/get",
            PayloadSegment = Encoding.UTF8.GetBytes(acUnit.FanMode.ToHassFanMode()),
        };

        // mode_state_topic
        var modeMessage = new MqttApplicationMessage()
        {
            Topic = $"{_mqttPathResolver.GetBaseTopicForAirConditioner(acUnit)}/mode/get",
            PayloadSegment = Encoding.UTF8.GetBytes(acUnit.RunningState == ZhongHongUnitRunningState.Off
                ? "off"
                : acUnit.Mode.ToHassMode()),
        };

        // current_temperature_topic
        var envTempMessage = new MqttApplicationMessage()
        {
            Topic = $"{_mqttPathResolver.GetBaseTopicForAirConditioner(acUnit)}/env_temp/get",
            PayloadSegment =
                Encoding.UTF8.GetBytes(acUnit.EnvironmentTemperature.ToString(CultureInfo.InvariantCulture)),
        };

        // temperature_state_topic
        var setPointTempMessage = new MqttApplicationMessage()
        {
            Topic = $"{_mqttPathResolver.GetBaseTopicForAirConditioner(acUnit)}/temp/get",
            PayloadSegment = Encoding.UTF8.GetBytes(acUnit.SetPointTemperature.ToString(CultureInfo.InvariantCulture)),
        };

        // BUG: _mqttClient synchronize
        if (_mqttClient is { IsConnected: true })
        {
            // TODO: logs
            await _mqttClient.PublishAsync(fanModeMessage);
            await _mqttClient.PublishAsync(modeMessage);
            await _mqttClient.PublishAsync(envTempMessage);
            await _mqttClient.PublishAsync(setPointTempMessage);
        }
    }

    private async Task PublishAirConditionerAutoDiscoverPayloadAsync(ZhongHongUnit acUnit)
    {
        var discoveryTopic = $"{_mqttPathResolver.GetBaseTopicForAirConditioner(acUnit)}/config";

        var configPayload = new ClimateMqttAutoDiscoveryConfig
        {
            UniqueId = acUnit.GetUniqueId(),
            Name = acUnit.Name ?? $"AC {acUnit.OutdoorUnitId}.{acUnit.IndoorUnitId}",
            BaseTopic = _mqttPathResolver.GetBaseTopicForAirConditioner(acUnit),
            Device = new MqttAutoDiscoveryConfigDevice
            {
                Identifiers = new List<string> { acUnit.GetUniqueId() },
                Name = $"VRF Bridge {acUnit.OutdoorUnitId}.{acUnit.IndoorUnitId}",
            }
        };

        var autoDiscoveryMessage = new MqttApplicationMessage
        {
            Topic = discoveryTopic,
            PayloadSegment = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configPayload)),
        };

        if (_mqttClient is { IsConnected: true })
        {
            // TODO: logs
            await _mqttClient.PublishAsync(autoDiscoveryMessage);
        }
    }

    #endregion

}