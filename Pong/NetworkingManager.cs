using Steamworks;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Diagnostics;
using System;
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;

namespace Pong
{
    internal class NetworkingManager
    {
        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<LobbyEnter_t> _lobbyEntered;
        private Callback<P2PSessionRequest_t> _sessionRequest;
        private Callback<P2PSessionConnectFail_t> _sessionConnectFail;

        private CSteamID _lobbyId;
        public Dictionary<CSteamID, Vector2> _playerPositions;

        public bool Initialize()
        {
            try
            {
                if (!SteamAPI.Init())
                {
                    Debug.WriteLine("SteamAPI.Init() failed!");
                    return false;
                }
                else
                {
                    //isSteamRunning = true;
                }
            }
            catch (DllNotFoundException e)
            { // We check this here as it will be the first instance of it.
                Debug.WriteLine(e);
                return false;
            }

            // Register callbacks
            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _sessionRequest = Callback<P2PSessionRequest_t>.Create(OnSessionRequest);
            _sessionConnectFail = Callback<P2PSessionConnectFail_t>.Create(OnSessionConnectFail);

            _playerPositions = new Dictionary<CSteamID, Vector2>();

            return true;
        }

        public void RunCallbacks()
        {
            SteamAPI.RunCallbacks();
        }
        public void CleanSteamworks()
        {
            SteamAPI.Shutdown();
        }
        public void CreateLobby(int maxPlayers=4)
        {
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
        }
        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Console.WriteLine("Failed to create lobby!");
                return;
            }

            _lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            Console.WriteLine("Lobby created: " + _lobbyId);

            // Set lobby data or invite friends here
        }
        public void JoinLobby(CSteamID lobbyId)
        {
            SteamMatchmaking.JoinLobby(lobbyId);
        }
        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Console.WriteLine("Failed to join lobby: " + callback.m_EChatRoomEnterResponse);
                return;
            }

            _lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            Console.WriteLine("Lobby joined: " + _lobbyId);

            // Get lobby data or start the game here
        }
        public void TransmitPosition(Vector2 position)
        {
            var data = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(position.X), 0, data, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.Y), 0, data, 4, 4);

            foreach (var player in _playerPositions.Keys)
            {
                var client = new SteamNetworkingIdentity();
                client.SetSteamID(player);

                IntPtr intPtr = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, intPtr, data.Length);

                SteamNetworkingMessages.SendMessageToUser(
                    ref client,
                    intPtr,
                    8,
                    (int)EP2PSend.k_EP2PSendReliable,
                    0
                );
                Marshal.FreeHGlobal(intPtr);
            }
        }
        private void ReceivePosition(CSteamID player, byte[] data)
        {
            var position = new Vector2(
                BitConverter.ToSingle(data, 0),
                BitConverter.ToSingle(data, 4)
            );

            if (_playerPositions.ContainsKey(player))
                _playerPositions[player] = position;
            else
                _playerPositions.Add(player, position);
        }
        private void OnSessionRequest(P2PSessionRequest_t callback)
        {
            SteamNetworking.AcceptP2PSessionWithUser(callback.m_steamIDRemote);
            Console.WriteLine("Accepted session request from: " + callback.m_steamIDRemote);
        }

        private void OnSessionConnectFail(P2PSessionConnectFail_t callback)
        {
            Console.WriteLine("Failed to connect session with: " + callback.m_steamIDRemote);
        }
    }
}
