using FLS.Models.PlayerData;
using FLS.Models.ServerCommunication.Events;
using System.Collections.Concurrent;

namespace FLS.Implementation
{
    public class SessionManager
    {
        private readonly ConcurrentDictionary<string, SessionInfo> Sessions = [];

        public void SessionCreated(PlayerSessionCreatedEvent info)
        {
            if (info.SessionPlatform != Platform.PSP) return;
            Sessions.TryAdd(info.SessionUuid, new SessionInfo 
            {
                Username = info.Username,
                PlayerConnectId = info.PlayerConnectId,
                Issuer = info.Issuer
            });
        }

        public void SessionDestroyed(PlayerSessionDestroyedEvent info)
        {
            if (Sessions.ContainsKey(info.SessionUuid))
                Sessions.TryRemove(info.SessionUuid, out _);
        }

        public bool IsAuthenticated(string id)
        {
            return Sessions.ContainsKey(id);
        }
    }
}
