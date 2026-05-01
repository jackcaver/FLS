using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;

namespace FLS.Models.Config
{
    public class ServerConfig
    {
        public string ExternalIP = "127.0.0.1";
        public int Port = 1081;
        public string ServerPrivateKey = "MIGrAgEAAiEAq0cOe8L1tOpnc7e+ouVD";
        public string ApiUrl = "http://127.0.0.1:10050";
        public string ServerCommunicationKey = "";

        public static ServerConfig GetFromFile(string path)
        {
            if (File.Exists(path))
            {
                string file = File.ReadAllText(path);
                ServerConfig config = JsonConvert.DeserializeObject<ServerConfig>(file);
                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
                return config;
            }
            else
            {
                Log.Error("No configuration found");
                GenerateExample($"{path}.example");
                Environment.Exit(1);
                return null;
            }
        }

        private static void GenerateExample(string path)
        {
            if (File.Exists(path))
            {
                Log.Information($"Example Configuration already exists at {path}");
            }
            else
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(new ServerConfig(), Formatting.Indented));
                Log.Information($"Generated example configuration at {path}");
            }
        }
    }
}
