using Newtonsoft.Json;

namespace NeteaseResourcesInstaller;

public class JsonConfig
{
    [JsonProperty("BedrockPath")]
    public string bedrockPath;
    [JsonProperty("BedrockVersion")]
    public string selectedBedrockVersion;
    [JsonProperty("Channel")]
    public string channel;
}