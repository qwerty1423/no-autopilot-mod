using System.Collections.Generic;
using UnityEngine;

namespace Mirage
{
    public class NetworkBehaviour : MonoBehaviour { public bool IsClientOnly { get; } }
    public class NetworkClient : MonoBehaviour
    {
        public bool Active { get; }
        public bool IsHost { get; }
    }
    public class NetworkServer : MonoBehaviour
    {
        public bool Active { get; }
        public IReadOnlyCollection<INetworkPlayer> AllPlayers { get; }
    }
    public interface INetworkPlayer
    {
    }
    public class NetworkPlayer
    {
    }
}
