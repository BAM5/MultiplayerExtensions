﻿using LiteNetLib.Utils;
using MultiplayerExtensions.HarmonyPatches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace MultiplayerExtensions.Networking
{
    public class ExtendedSessionManager : IInitializable
    {
        [Inject]
        private IMultiplayerSessionManager _multiplayerSessionManager = null!;

        private NetworkPacketSerializer<ExtendedSessionManager.MessageType, IConnectedPlayer> _packetSerializer = new NetworkPacketSerializer<ExtendedSessionManager.MessageType, IConnectedPlayer>();
        public readonly Dictionary<string, ExtendedPlayer> players = new Dictionary<string, ExtendedPlayer>();

        public event Action<ExtendedPlayer>? playerConnectedEvent;
        public event Action<ExtendedPlayer>? playerDisconnectedEvent;
        public event Action<ExtendedPlayer>? playerStateChangedEvent;

        public void Initialize()
        {
            Plugin.Log?.Info("Setting up SessionManager");

            _multiplayerSessionManager.RegisterSerializer((MultiplayerSessionManager.MessageType)4, _packetSerializer);

            _multiplayerSessionManager.connectedEvent += connectedEvent;
            _multiplayerSessionManager.connectionFailedEvent += connectionFailedEvent;
            _multiplayerSessionManager.disconnectedEvent += disconnectedEvent;

            _multiplayerSessionManager.playerConnectedEvent += HandlePlayerConnected;
            _multiplayerSessionManager.playerDisconnectedEvent += HandlePlayerDisconnected;
            _multiplayerSessionManager.playerStateChangedEvent += HandlePlayerStateChanged;

            _multiplayerSessionManager.SetLocalPlayerState("modded", true);
            _multiplayerSessionManager.SetLocalPlayerState("customsongs", Plugin.Config.CustomSongs);
            _multiplayerSessionManager.SetLocalPlayerState("enforcemods", Plugin.Config.EnforceMods);
        }

        public ExtendedPlayer GetExtendedPlayer(IConnectedPlayer player)
        {
            return players[player.userId];
        }

        private void HandlePlayerConnected(IConnectedPlayer player)
        {
            Plugin.Log?.Info($"Player '{player.userId}' joined");
            var extendedPlayer = new ExtendedPlayer(player);
            players[player.userId] = extendedPlayer;
            playerConnectedEvent?.Invoke(extendedPlayer);
        }

        private void HandlePlayerDisconnected(IConnectedPlayer player)
        {
            Plugin.Log?.Info($"Player '{player.userId}' disconnected");
            var extendedPlayer = players[player.userId];
            playerDisconnectedEvent?.Invoke(extendedPlayer);
            players.Remove(player.userId);
        }

        private void HandlePlayerStateChanged(IConnectedPlayer player)
        {
            if (player.userId != _multiplayerSessionManager.localPlayer.userId)
            {
                if (player.isConnectionOwner)
                {
                    UI.GameplaySetupPanel.instance.SetCustomSongs(player.HasState("customsongs"));
                    UI.GameplaySetupPanel.instance.SetEnforceMods(player.HasState("enforcemods"));
                }

                var extendedPlayer = players[player.userId];
                playerStateChangedEvent?.Invoke(extendedPlayer);
            }
        }

        public void RegisterCallback<T>(ExtendedSessionManager.MessageType serializerType, Action<T, ExtendedPlayer> callback, Func<T> constructor) where T : INetSerializable
        {
            Action<T, IConnectedPlayer> extendedCallback = delegate (T packet, IConnectedPlayer player)
            {
                ExtendedPlayer extendedPlayer = GetExtendedPlayer(player);
                callback(packet, extendedPlayer);
            };

            _packetSerializer.RegisterCallback(serializerType, extendedCallback, constructor);
        }

        public void RegisterSerializer(ExtendedSessionManager.MessageType serializerType, INetworkPacketSubSerializer<IConnectedPlayer> subSerializer)
        {
            _packetSerializer.RegisterSubSerializer(serializerType, subSerializer);
        }

        public void UnregisterCallback<T>(ExtendedSessionManager.MessageType serializerType) where T : INetSerializable
        {
            _packetSerializer.UnregisterCallback<T>(serializerType);
        }

        public void UnregisterSerializer(ExtendedSessionManager.MessageType serializerType, INetworkPacketSubSerializer<IConnectedPlayer> subSerializer)
        {
            _packetSerializer.RegisterSubSerializer(serializerType, subSerializer);
        }

        public event Action? connectedEvent;
        public event Action<ConnectionFailedReason>? connectionFailedEvent;
        public event Action<DisconnectedReason>? disconnectedEvent;

        public IConnectedPlayer localPlayer => _multiplayerSessionManager.localPlayer;
        public bool isConnectionOwner => _multiplayerSessionManager.isConnectionOwner;
        public float syncTime => _multiplayerSessionManager.syncTime;
        public bool isSyncTimeInitialized => _multiplayerSessionManager.isSyncTimeInitialized;
        public float syncTimeDelay => _multiplayerSessionManager.syncTimeDelay;
        public int connectedPlayerCount => _multiplayerSessionManager.connectedPlayerCount;
        public bool isConnectingOrConnected => _multiplayerSessionManager.isConnectingOrConnected;
        public bool isConnected => _multiplayerSessionManager.isConnected;
        public bool isConnecting => _multiplayerSessionManager.isConnecting;
        public bool isSpectating => _multiplayerSessionManager.isSpectating;
        public IConnectedPlayer connectionOwner => _multiplayerSessionManager.connectionOwner;

        public void SetLocalPlayerState(string state, bool hasState) => _multiplayerSessionManager.SetLocalPlayerState(state, hasState);
        public IConnectedPlayer GetPlayer(string userId) => _multiplayerSessionManager.GetConnectedPlayerByUserId(userId);

        public void Send<T>(T message) where T : INetSerializable => _multiplayerSessionManager.Send(message);
        public void SendUnreliable<T>(T message) where T : INetSerializable => _multiplayerSessionManager.SendUnreliable(message);

        public enum MessageType : Byte
        {
            PlayerUpdate,
            PreviewBeatmapUpdate
        }
    }
}
