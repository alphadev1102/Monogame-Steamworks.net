using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Pong
{
    class SteamLobbySystem
    {
        public CSteamID _lobbyId;
        public  List<SteamPlayer> ConnectedPlayers;
        public int _maxPlayers = 4;
        public string _gameName = "MyGame";
        private string _userName;
        public bool _isCreator;
       
        private int _channel = 0;
        private Callback<LobbyCreated_t> OnLobbyCreatedCallback;
        private Callback<LobbyEnter_t> OnLobbyJoinedCallback;
        private Callback<LobbyChatMsg_t> OnLobbyChatMessageCallback;
        private Callback<LobbyChatUpdate_t> OnLobbyChatUpdateCallback;
        private CallResult<LobbyMatchList_t> OnLobbyListReceivedCallback;
        private Callback<GameLobbyJoinRequested_t> OnGameLobbyJoinRequestedCallback;
        private Callback<SteamNetworkingMessagesSessionRequest_t> OnSteamNetworkingMessagesSessionRequest_t;
        public SteamLobbySystem()
        {
            ConnectedPlayers = new List<SteamPlayer>();
            _userName = "";
            _isCreator = false;
        }

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
            /*OnLobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            OnLobbyJoinedCallback = Callback<LobbyEnter_t>.Create(OnLobbyJoined);
            OnLobbyChatMessageCallback = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
            OnLobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            OnLobbyListReceivedCallback = CallResult<LobbyMatchList_t>.Create(OnLobbyListReceived);
            OnGameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            OnSteamNetworkingMessagesSessionRequest_t = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(SteamNetworkingMessagesSessionRequest);
            */
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

        public void RequestLobbyList()
        {
            
            SteamMatchmaking.AddRequestLobbyListStringFilter("GameName", _gameName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamAPICall_t apiCall = SteamMatchmaking.RequestLobbyList();
            OnLobbyListReceivedCallback.Set(apiCall);
        }
        private void OnLobbyListReceived(LobbyMatchList_t pCallback, bool IOFailure)
        {
            Debug.WriteLine("Found " + pCallback.m_nLobbiesMatching + " lobbies!");

            for (int i = 0; i < pCallback.m_nLobbiesMatching; i++)
            {
                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
                // You can here filter or decide which lobby to join.
            }
        }
        public void CreateLobby()
        {
            _isCreator = true;
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, _maxPlayers);
        }

        public void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.WriteLine("Failed to create lobby: " + callback.m_eResult);
                return;
            }

            _lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(_lobbyId, "GameName", _gameName);

            Debug.WriteLine("Lobby created with ID: " + _lobbyId);
        }

        public void JoinLobby(CSteamID lobbyId)
        {
            SteamMatchmaking.JoinLobby(lobbyId);
        }

        public void OnLobbyJoined(LobbyEnter_t callback)
        {
            if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Console.WriteLine("Failed to join lobby: " + callback.m_EChatRoomEnterResponse);
                return;
            }

            _lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            Debug.WriteLine("Joined lobby with ID: " + _lobbyId);
            
            // Get the list of connected players
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
            for (int i = 0; i < numMembers; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyId, i);
                SteamPlayer player = new SteamPlayer(memberId,string.Format("Player{0}",i+1));
                ConnectedPlayers.Add(player);
            }


        }

        public void LeaveLobby()
        {
            SteamMatchmaking.LeaveLobby(_lobbyId);
        }

        public void OnLobbyLeft(LobbyChatUpdate_t callback)
        {
            if (callback.m_ulSteamIDLobby != _lobbyId.m_SteamID)
                return;

            Debug.WriteLine("Left lobby with ID: " + _lobbyId);

            //SendLobbyMemberUpdate("MemberUpdate");
            // Clear the list of connected players
            ConnectedPlayers.Clear();
        }
        
        public void SendMessageToLobby(string message)
        {
            byte[] messageData = Encoding.ASCII.GetBytes(message);
            SteamMatchmaking.SendLobbyChatMsg(_lobbyId, messageData, messageData.Length + 1);
        }

        public void OnLobbyChatMessage(LobbyChatMsg_t callback)
        {
            if (callback.m_ulSteamIDLobby != _lobbyId.m_SteamID)
                return;

            byte[] messageData = new byte[4096];
            int messageLength;
            EChatEntryType chatEntryType;
            CSteamID senderId;
            CSteamID sId = new CSteamID(callback.m_ulSteamIDLobby);
            messageLength = SteamMatchmaking.GetLobbyChatEntry(sId, (int)callback.m_iChatID, out senderId, messageData, messageData.Length, out chatEntryType);

            string message = Encoding.ASCII.GetString(messageData, 0,messageLength);
            Debug.WriteLine("Received message from " + senderId + ": " + message);

        }
        public void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            if (callback.m_ulSteamIDLobby != _lobbyId.m_SteamID)
                return;
            ConnectedPlayers.Clear();
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
            for (int i = 0; i < numMembers; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyId, i);
                SteamPlayer player = new SteamPlayer(memberId, string.Format("Player{0}", i + 1));
                ConnectedPlayers.Add(player);
            }
        }

        public void InviteFriend(CSteamID lobbyId, CSteamID friendId)
        {
            bool result = SteamMatchmaking.InviteUserToLobby(lobbyId, friendId);

            if (result)
            {
                Debug.WriteLine("Invitation sent successfully");
            }
            else
            {
                Debug.WriteLine("Failed to send invitation");
            }
        }

        public CSteamID GetFriendSteamID(string friendName)
        {
            int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);

            for (int i = 0; i < friendCount; ++i)
            {
                CSteamID friendSteamId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);

                if (friendName == SteamFriends.GetFriendPersonaName(friendSteamId))
                {
                    // Now you have each friend's Steam ID and name
                    Debug.WriteLine("Friend: " + friendName + ", SteamID: " + friendSteamId);
                    return friendSteamId;
                }
            }
            return CSteamID.Nil;
        }

        public void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t pCallback)
        {
            // Automatically join the lobby that the invitation was for.
            //SteamMatchmaking.JoinLobby(pCallback.m_steamIDLobby);
        }


        public void SteamNetworkingMessagesSessionRequest(SteamNetworkingMessagesSessionRequest_t request)
        {

            CSteamID clientId = request.m_identityRemote.GetSteamID(); 

            if (ExpectingClient(clientId))
            {
                
                var client = new SteamNetworkingIdentity();
                client.SetSteamID(clientId);

                SteamNetworkingMessages.AcceptSessionWithUser(ref request.m_identityRemote);
            }
            else
            {
                
                Debug.WriteLine("Unexpected session request from " + clientId);
            }
        }

        public bool ExpectingClient(CSteamID cSteamID)
        {
            bool bret = false;
            foreach(var client in ConnectedPlayers)
            {
                if(cSteamID == client.UserID)
                {
                    bret = true;
                    return bret;
                }
            }
            return bret;
        }
        public void TransmitPosition(Vector2 position)
        {
            byte[] positionBytes = SerializePosition(position);

            foreach (var player in ConnectedPlayers)
            {
                if (player.UserID != SteamUser.GetSteamID())
                {
                    var client = new SteamNetworkingIdentity();
                    client.SetSteamID(player.UserID);
                    IntPtr intPtr = Marshal.AllocHGlobal(positionBytes.Length);
                    Marshal.Copy(positionBytes, 0, intPtr, positionBytes.Length);
                    var result = SteamNetworkingMessages.SendMessageToUser(ref client, intPtr, (uint)positionBytes.Length, Constants.k_nSteamNetworkingSend_Reliable, _channel);
                    Debug.WriteLine("Transmit_Result:" + result.ToString());
                    Marshal.FreeHGlobal(intPtr);
                }
                
            }
        }
        public void ReceivePosition()
        {
            IntPtr[] messageHandles = new IntPtr[5];

            int numMessages = SteamNetworkingMessages.ReceiveMessagesOnChannel(_channel, messageHandles, 5);

            for (int i = 0; i < numMessages; i++)
            {
                SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messageHandles[i]);

                bool isReliable = (message.m_nFlags & Constants.k_nSteamNetworkingSend_Reliable) != 0;
                bool isControl = (message.m_nFlags & Constants.k_nSteamNetworkingSend_NoNagle) != 0;
                if (!isReliable || isControl)
                {
                    continue;
                }

                CSteamID senderSteamId = message.m_identityPeer.GetSteamID();
                int messageSize = message.m_cbSize;
                byte[] positionBytes = new byte[messageSize];
                Marshal.Copy(message.m_pData, positionBytes, 0, messageSize);
                Vector2 receivedPosition = DeserializePosition(positionBytes);
                foreach(var player in ConnectedPlayers)
                {
                    if (player.UserID == senderSteamId)
                    {
                        player.Position = receivedPosition;
                        Debug.WriteLine(receivedPosition);
                        break;
                    }
                }

                SteamNetworkingMessage_t.Release(messageHandles[i]);
            }
        }

        private byte[] SerializePosition(Vector2 position)
        {
            byte[] positionBytes = new byte[sizeof(float) * 2];
            Buffer.BlockCopy(BitConverter.GetBytes(position.X), 0, positionBytes, 0, sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(position.Y), 0, positionBytes, sizeof(float), sizeof(float));
            return positionBytes;
        }
        private Vector2 DeserializePosition(byte[] positionBytes)
        {
            float x = BitConverter.ToSingle(positionBytes, 0);
            float y = BitConverter.ToSingle(positionBytes, sizeof(float));
            return new Vector2(x, y);
        }
        public CSteamID GetOwnerSteamID()
        {
            return SteamUser.GetSteamID();
        }
    }
}
