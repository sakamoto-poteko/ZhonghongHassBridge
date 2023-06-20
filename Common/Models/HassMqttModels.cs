using System.Text.Json.Serialization;

namespace ZhongHong.Common.Models;

public class HassMqttModels
{
}

public class MqttAutoDiscoveryConfigDevice
{
    [JsonPropertyName("identifiers")] public IList<string> Identifiers { get; set; }
    [JsonPropertyName("manufacturer")] public string Manufacture { get; set; } = "ZhongHong via Bridge";
    [JsonPropertyName("model")] public string Model { get; set; } = "WIP";
    [JsonPropertyName("name")] public string Name { get; set; } = "VRF Bridge (WIP)";
    [JsonPropertyName("sw_version")] public string SoftwareVersion { get; set; } = "WIP";
}

public class ClimateMqttAutoDiscoveryConfig
{
    [JsonPropertyName("unique_id")] public string UniqueId { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("device")] public MqttAutoDiscoveryConfigDevice Device { get; set; }
    
    [JsonPropertyName("~")] public string BaseTopic { get; set; }

    [JsonPropertyName("action_topic")] public string ActionTopic { get; set; } = "~/action/set";

    [JsonPropertyName("fan_mode_state_topic")]
    public string FanModeStateTopic { get; set; } = "~/fan_mode/get";

    [JsonPropertyName("fan_mode_command_topic")]
    public string FanModeCommandTopic { get; set; } = "~/fan_mode/set";

    [JsonPropertyName("fan_modes")]
    public List<string> FanModes { get; set; } = new()
    {
        "auto",
        "low",
        "medium",
        "high"
    };

    [JsonPropertyName("mode_state_topic")] public string ModeStateTopic { get; set; } = "~/mode/get";

    [JsonPropertyName("mode_command_topic")]
    public string ModeCommandTopic { get; set; } = "~/mode/set";

    [JsonPropertyName("modes")]
    public List<string> Modes { get; set; } = new()
    {
        // BUG: "off" requires special handling, since it converts to running states in VRF gateway
        "off", "cool", "heat", "dry", "fan_only"
    };
    
    [JsonPropertyName("current_temperature_topic")]
    public string CurrentTemperatureTopic { get; set; } = "~/env_temp/get";

    [JsonPropertyName("temperature_command_topic")]
    public string TemperatureCommandTopic { get; set; } = "~/temp/set";

    [JsonPropertyName("temperature_state_topic")]
    public string TemperatureStateTopic { get; set; } = "~/temp/get";

    [JsonPropertyName("temp_step")] public float TempStep { get; set; } = 1.0f;

    [JsonPropertyName("temperature_unit")] public string TemperatureUnit { get; set; } = "C";
}