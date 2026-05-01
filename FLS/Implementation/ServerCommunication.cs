using FLS.Models.Config;
using FLS.Models.ServerCommunication;
using FLS.Models.ServerCommunication.Events;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FLS.Implementation
{
    public class ServerCommunication
    {
        private readonly ClientWebSocket Socket;
        private const int DefaultMessageCapacity = 4096;
        private const string MasterServer = "API";
        private readonly int EncryptedHeaderSize = AesGcm.NonceByteSizes.MaxSize + AesGcm.TagByteSizes.MaxSize;
        private readonly AesGcm Aes;
        private readonly Server Server;
        private ServerConfig Config => Server.Config;
        private SessionManager SessionManager => Server.SessionManager;
        private bool HasKey => !string.IsNullOrEmpty(Config.ServerCommunicationKey);

        public ServerCommunication(Server server)
        {
            Server = server;
            Socket = new();
            Socket.Options.SetRequestHeader("server_id", Server.ServerID.ToString());

            if (HasKey)
                Aes = new AesGcm(Encoding.UTF8.GetBytes(Config.ServerCommunicationKey), AesGcm.TagByteSizes.MaxSize);
        }

        public void Start() 
        {
            var url = Config.ApiUrl.TrimEnd('/');
            url = url.StartsWith("http") ? url.Replace("http", "ws") : url.Replace("https", "wss");
            url += "/api/Gateway";

            Socket.ConnectAsync(new Uri(url), CancellationToken.None).Wait();

            Task.Run(Receive);

            DispatchEvent(GatewayEvents.ServerInfo, new ServerInfo
            {
                Type = ServerType.FLS,
                Address = Config.ExternalIP,
                Port = Config.Port,
                ServerPrivateKey = Config.ServerPrivateKey
            });
        }

        public void Stop()
        {
            Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutdown", CancellationToken.None).Wait();
        }

        private void DispatchEvent(string type, object evt)
        {
            var message = new GatewayMessage
            {
                Type = type,
                From = Server.ServerID.ToString(),
                To = MasterServer,
                Content = JsonConvert.SerializeObject(evt)
            };

            string payload = JsonConvert.SerializeObject(message);
            
            Send(payload).Wait();
        }

        private async Task Send(string message)
        {
            byte[] bytes;

            var type = WebSocketMessageType.Text;
            if (HasKey)
            {
                type = WebSocketMessageType.Binary;
                bytes = Encrypt(message);
            }
            else bytes = Encoding.UTF8.GetBytes(message);

            if (Socket.State == WebSocketState.Open)
                await Socket.SendAsync(bytes, type, true, CancellationToken.None);
        }

        private async Task Receive()
        {
            byte[] scratch = new byte[DefaultMessageCapacity];
            while (Socket.State == WebSocketState.Open)
            {
                var buffer = new List<byte>();
                WebSocketReceiveResult result;
                try
                {
                    do
                    {
                        result = await Socket.ReceiveAsync(scratch, CancellationToken.None);
                        buffer.AddRange(scratch.AsSpan(0, result.Count));
                    }
                    while (!result.EndOfMessage);
                }
                catch (Exception e)
                {
                    Log.Debug($"There was an error receiving message: {e}");
                    continue;
                }

                string payload;
                if (result.MessageType == WebSocketMessageType.Text && !HasKey)
                    payload = Encoding.UTF8.GetString([.. buffer]);
                else if (result.MessageType == WebSocketMessageType.Binary && HasKey)
                {
                    if (buffer.Count < EncryptedHeaderSize)
                    {
                        Log.Debug("Received message with invalid header!");
                        break;
                    }

                    try
                    {
                        payload = Decrypt(CollectionsMarshal.AsSpan(buffer));
                    }
                    catch (Exception e)
                    {
                        Log.Debug($"Failed to decrypt message: {e}");
                        break;
                    }
                }
                else break;

                try
                {
                    var message = JsonConvert.DeserializeObject<GatewayMessage>(payload);
                    if (message != null)
                        OnMessage(message);
                }
                catch (Exception e)
                {
                    Log.Debug($"Failed to process message: {e}");
                }
            }

            if (Socket is { State: WebSocketState.Aborted or WebSocketState.Closed or WebSocketState.CloseSent })
                return;

            try
            {
                await Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            }
            catch (Exception)
            {
                Log.Debug($"There was an error while closing connection");
            }
        }

        private void OnMessage(GatewayMessage message)
        {
            Log.Debug($"Received {message.Type} from {message.From} with content:");
            Log.Debug(message.Content);

            switch (message.Type)
            {
                case GatewayEvents.PlayerSessionCreated:
                {
                    if (ParseMessage(message, out PlayerSessionCreatedEvent info))
                    {
                        SessionManager.SessionCreated(info);
                        return;
                    }
                    break;
                }
                case GatewayEvents.PlayerSessionDestroyed:
                {
                    if (ParseMessage(message, out PlayerSessionDestroyedEvent info))
                    {
                        SessionManager.SessionDestroyed(info);
                        return;
                    }
                    break;
                }
            }
        }

        private static bool ParseMessage<T>(GatewayMessage message, out T evt)
        {
            evt = JsonConvert.DeserializeObject<T>(message.Content);
            if (evt != null) return true;
            Log.Error($"Cannot parse {message.Type}");
            return false;
        }

        private byte[] Encrypt(string message)
        {
            byte[] contents = Encoding.UTF8.GetBytes(message);
            byte[] payload = new byte[EncryptedHeaderSize + contents.Length];

            var nonce = payload.AsSpan(0, AesGcm.NonceByteSizes.MaxSize);
            var tag = payload.AsSpan(AesGcm.NonceByteSizes.MaxSize, AesGcm.TagByteSizes.MaxSize);
            var output = payload.AsSpan(EncryptedHeaderSize, contents.Length);

            RandomNumberGenerator.Fill(nonce);
            Aes.Encrypt(nonce, contents, output, tag);
            return payload;
        }

        private string Decrypt(Span<byte> message)
        {
            ReadOnlySpan<byte> nonce = message[..AesGcm.NonceByteSizes.MaxSize];
            ReadOnlySpan<byte> tag = message.Slice(AesGcm.NonceByteSizes.MaxSize, AesGcm.TagByteSizes.MaxSize);
            ReadOnlySpan<byte> data = message.Slice(EncryptedHeaderSize, message.Length - EncryptedHeaderSize);

            byte[] output = new byte[data.Length];
            Aes.Decrypt(nonce, data, tag, output);
            return Encoding.UTF8.GetString(output);
        }
    }
}
