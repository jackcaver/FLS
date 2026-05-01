using FLS.Models.Config;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace FLS.Implementation
{
    public class Server
    {
        private readonly TcpListener Listener;
        private readonly List<Client> Clients;
        public readonly Guid ServerID;
        public readonly ServerConfig Config;
        private readonly ServerCommunication ServerComm;
        public readonly SessionManager SessionManager;

        public Server(string configPath)
        {
            Config = ServerConfig.GetFromFile(configPath);
            ServerID = Guid.NewGuid();
            Clients = [];
            SessionManager = new();
            ServerComm = new(this);
            Listener = new(new IPEndPoint(IPAddress.Any, Config.Port));
        }

        public void Start()
        {
            Listener.Start();
            ServerComm.Start();
            Log.Information($"Started listening on {Listener.LocalEndpoint}");
        }

        public void Stop()
        {
            foreach (var client in Clients)
                client.Disconnect();

            Listener.Stop();
            ServerComm.Stop();
        }

        public void CheckClients()
        {
            if (Listener.Pending())
            {
                var client = Listener.AcceptTcpClient();
                Clients.Add(new Client(client, this));
            }
        }

        public void SendToPlayer(string message, int PlayerID)
        {
            var client = Clients.FirstOrDefault(match => match.PlayerID == PlayerID);
            client?.Send(message);
        }
    }
}
