using FLS.Models.Config;
using Serilog;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace FLS.Implementation
{
    public class Client
    {
        private readonly TcpClient client;
        private readonly Server Server;
        private readonly Aes Aes;
        private ServerConfig Config => Server.Config;
        private SessionManager SessionManager => Server.SessionManager;
        public int PlayerID;

        public Client(TcpClient client, Server server)
        {
            Server = server;
            this.client = client;
            Aes = Aes.Create();
            Aes.Key = Encoding.UTF8.GetBytes(Config.ServerPrivateKey);
            Aes.Mode = CipherMode.ECB;
            Aes.Padding = PaddingMode.None;
            Task.Run(HandleConnection);
        }

        private void Send(byte[] data)
        {
            if (!client.Connected) return;
            var size = new byte[2];
            BinaryPrimitives.WriteInt16BigEndian(size, (short)data.Length);
            try
            {
                var stream = client.GetStream();
                stream.Write(size);
                stream.Write(data);
                stream.Flush();
            }
            catch
            {
                Disconnect();
            }
        }

        public void Send(string message)
        {
            Send(Encrypt(message));
        }

        public void Disconnect()
        {
            client.Close();
            client.Dispose();
            Aes.Dispose();
        }

        private void HandleConnection()
        {
            var remoteEndPoint = client.Client.RemoteEndPoint;
            Log.Debug($"New connection from {remoteEndPoint}");
            while (client.Connected)
            {
                BinaryReader reader;
                byte[] buffer;
                try
                {
                    reader = new BinaryReader(client.GetStream());
                    int size = BinaryPrimitives.ReadInt16BigEndian(reader.ReadBytes(2));
                    buffer = reader.ReadBytes(size);
                }
                catch
                {
                    break;
                }
                Log.Debug($"Received {buffer.Length} bytes from {remoteEndPoint}");

                var messageXML = Decrypt(buffer);
                Log.Debug(messageXML.Trim('\0').Trim('\n'));
                Log.Debug($"null count: {messageXML.Count(match => match == '\0')}");

                var message = new XmlDocument();
                message.LoadXml(messageXML.TrimEnd('\0'));
                ProcessMessage(message, messageXML);
            }
            Disconnect();
            Log.Debug($"{remoteEndPoint} disconnected");
        }

        private void ProcessMessage(XmlDocument message, string messageXML)
        {
            var msg = message.SelectSingleNode("message");
            if (msg == null)
                return;

            if (msg.Attributes["target"].Value == "plyr")
            {
                messageXML = messageXML.Insert(9, $"sender=\"plyr\" senderID=\"{PlayerID}\" ");
                Log.Debug(messageXML);

                if (!int.TryParse(msg.Attributes["targetID"].Value, out int targetID))
                    return;

                Server.SendToPlayer(messageXML, targetID);
            }
            else
            {
                if (msg.InnerXml.StartsWith("<Hello"))
                {
                    var hello = msg.SelectSingleNode("Hello");

                    if (!SessionManager.IsAuthenticated(hello.Attributes["session_uuid"].Value))
                        Disconnect();

                    if (!int.TryParse(msg.Attributes["targetID"].Value, out PlayerID))
                        Disconnect();
                }
                Send(messageXML);
            }
        }
        
        private byte[] Encrypt(string message)
        {
            using var stream = new MemoryStream();
            using var cryptoTransform = Aes.CreateEncryptor(Aes.Key, null);
            using var cryptoStream = new CryptoStream(stream, cryptoTransform, CryptoStreamMode.Write);

            cryptoStream.Write(Encoding.UTF8.GetBytes(message));

            return stream.ToArray();
        }

        private string Decrypt(byte[] message)
        {
            using var stream = new MemoryStream(message);
            using var cryptoTransform = Aes.CreateDecryptor(Aes.Key, null);
            using var cryptoStream = new CryptoStream(stream, cryptoTransform, CryptoStreamMode.Read);
            using var streamReader = new StreamReader(cryptoStream, Encoding.UTF8);

            return streamReader.ReadToEnd();
        }
    }
}
