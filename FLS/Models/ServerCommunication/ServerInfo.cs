using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FLS.Models.ServerCommunication
{
    public class ServerInfo
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ServerType Type { get; set; }
        public string Address { get; set; }
        public int Port { get; set; }
        public string ServerPrivateKey { get; set; }
    }
}
