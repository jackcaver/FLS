using Newtonsoft.Json;

namespace FLS.Models.ServerCommunication.Events;

public class PlayerSessionDestroyedEvent
{
    [JsonProperty("uuid")] public string SessionUuid { get; set; } = string.Empty;
}