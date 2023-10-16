using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using Myra.Graphics2D.UI;
using Steamworks;
using System.Text;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;

namespace Pong
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private bool _isSteamRunning=false;
        bool _isGameStarted = false;
        private SteamLobbySystem _lobbysystem;

        private Desktop _desktop;
        Texture2D _playerSprite;
        SteamPlayer _ownerPlayer;

        Panel _createPanel;
        Panel _lobbylistPanel;
        Panel _mainPanel;

        private Callback<LobbyCreated_t> OnLobbyCreatedCallback;
        private Callback<LobbyEnter_t> OnLobbyJoinedCallback;
        private Callback<LobbyChatMsg_t> OnLobbyChatMessageCallback;
        private Callback<LobbyChatUpdate_t> OnLobbyChatUpdateCallback;
        private CallResult<LobbyMatchList_t> OnLobbyListReceivedCallback;
        private Callback<SteamNetworkingMessagesSessionRequest_t> OnSteamNetworkingMessagesSessionRequest_t;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            _lobbysystem = new SteamLobbySystem();
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            if(!_lobbysystem.Initialize()) 
            {
                return;
            }
            
            OnLobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            OnLobbyJoinedCallback = Callback<LobbyEnter_t>.Create(OnLobbyJoined);
            OnLobbyChatMessageCallback = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
            OnLobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            OnLobbyListReceivedCallback = CallResult<LobbyMatchList_t>.Create(OnLobbyListReceived);
            OnSteamNetworkingMessagesSessionRequest_t = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(SteamNetworkingMessagesSessionRequest);
            _isSteamRunning = true;
            RequestLobbyList(null,null);

            base.Initialize();
            
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _playerSprite = Content.Load<Texture2D>("sprite");

            Myra.MyraEnvironment.Game = this;
            _desktop = new Desktop();
            _createPanel = new Panel();
            _createPanel.Width = 500;  // Width in pixels
            _createPanel.Height = 300;
            var createBtn = new TextButton { Width=100, Height=50,Top=0,Left=0, Text = "CreateGame" };
            createBtn.Click += CreateGame;
            _createPanel.AddChild(createBtn);

            var refreshBtn = new TextButton { Width = 100, Height = 50, Top = 55, Left = 0, Text = "Refresh" };
            refreshBtn.Click += RequestLobbyList;
            _createPanel.AddChild(refreshBtn);

            _lobbylistPanel = new Panel { Width = 300, Height = 300,Top=0, Left=100 };
            _createPanel.AddChild(_lobbylistPanel);
            _desktop.Root = _createPanel;
            
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();


            _lobbysystem.RunCallbacks();
 
            if(_isSteamRunning && _isGameStarted)
            {
                // Handle input for player movement
                var keyboardState = Keyboard.GetState();
                if (keyboardState.IsKeyDown(Keys.W))
                {
                    _ownerPlayer.Position += new Vector2(0, -5);
                    _lobbysystem.TransmitPosition(_ownerPlayer.Position);
                }
                if (keyboardState.IsKeyDown(Keys.S))
                {
                    _ownerPlayer.Position += new Vector2(0, 5);
                    _lobbysystem.TransmitPosition(_ownerPlayer.Position);
                }
                if (keyboardState.IsKeyDown(Keys.A))
                {
                    _ownerPlayer.Position += new Vector2(-5, 0);
                    _lobbysystem.TransmitPosition(_ownerPlayer.Position);
                }
                if (keyboardState.IsKeyDown(Keys.D))
                {
                    _ownerPlayer.Position += new Vector2(5, 0);
                    _lobbysystem.TransmitPosition(_ownerPlayer.Position);
                }

                _lobbysystem.ReceivePosition();
            }
            

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _desktop.Render();

            if (_isSteamRunning && _isGameStarted)
            {
                _spriteBatch.Begin();
                _spriteBatch.Draw(_playerSprite, _ownerPlayer.Position, Color.Red);
                foreach (var player in _lobbysystem.ConnectedPlayers)
                {
                    if (player.UserID == _lobbysystem.GetOwnerSteamID())
                        continue;
                    
                    _spriteBatch.Draw(_playerSprite, player.Position, Color.Red);
                }
                _spriteBatch.End();
            }
            base.Draw(gameTime);
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            // Cleanup Steamworks
            _lobbysystem.CleanSteamworks();

            base.OnExiting(sender, args);
        }

        private void CreateGame(object sender, EventArgs e)
        {
            _lobbysystem.CreateLobby();
        }
        private void StarteGame(object sender, EventArgs e)
        {
            if (_lobbysystem.ConnectedPlayers.Count < 2)
                return;

            SendMessage(null, null, "*_StartGame_*");
            //_mainPanel.Visible = false;
            //_isGameStarted = true;
        }
        private void RequestLobbyList(object sender, EventArgs e)
        {

            if (SteamAPI.IsSteamRunning())
            {
                SteamMatchmaking.AddRequestLobbyListStringFilter("GameName", _lobbysystem._gameName, ELobbyComparison.k_ELobbyComparisonEqual);
                SteamAPICall_t apiCall = SteamMatchmaking.RequestLobbyList();
                OnLobbyListReceivedCallback.Set(apiCall);
            }
            
            
        }

        private void OnLobbyListReceived(LobbyMatchList_t pCallback, bool IOFailure)
        {
            Debug.WriteLine("Found " + pCallback.m_nLobbiesMatching + " lobbies!");
            var lobbies = _lobbylistPanel.GetChildren();
            foreach (var lobbie in lobbies)
            {
                lobbie.RemoveFromParent();
            }
            for (int i = 0; i < pCallback.m_nLobbiesMatching; i++)
            {
                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
                
                var joinBtn = new TextButton { Width = 200, Height = 50, Top = 50 * i, Left = 200, Text = "JoinGame:" + lobbyId.m_SteamID.ToString() };
                joinBtn.Click += new EventHandler((sender, e) => JoinLobby(sender, e, lobbyId));
                _lobbylistPanel.AddChild(joinBtn);
                              
            }
        }
        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.WriteLine("Failed to create lobby: " + callback.m_eResult);
                return;
            }

            _lobbysystem._lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(_lobbysystem._lobbyId, "GameName", _lobbysystem._gameName);

            Debug.WriteLine("Lobby created with ID: " + _lobbysystem._lobbyId);

            
        }
        private void JoinLobby(object sender, EventArgs e, CSteamID lobbyId)
        {
            SteamMatchmaking.JoinLobby(lobbyId);
        }
        private void OnLobbyJoined(LobbyEnter_t callback)
        {
            if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Debug.WriteLine("Failed to join lobby: " + callback.m_EChatRoomEnterResponse);
                return;
            }

            _lobbysystem._lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            Debug.WriteLine("Joined lobby with ID: " + _lobbysystem._lobbyId);

            // Get the list of connected players
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(_lobbysystem._lobbyId);
            for (int i = 0; i < numMembers; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(_lobbysystem._lobbyId, i);
                SteamPlayer player = new SteamPlayer(memberId, string.Format("Player{0}", i + 1));
                _lobbysystem.ConnectedPlayers.Add(player);
            }

            CreateMainPanel();
            if (!_lobbysystem._isCreator)
            {
                UpdatePlayerListInMainPanel();
            }
        }

        private void OnLobbyLeft(LobbyChatUpdate_t callback)
        {
            if (callback.m_ulSteamIDLobby != _lobbysystem._lobbyId.m_SteamID)
                return;

            Debug.WriteLine("Left lobby with ID: " + _lobbysystem._lobbyId);

            //SendLobbyMemberUpdate("MemberUpdate");
            // Clear the list of connected players
            _lobbysystem.ConnectedPlayers.Clear();
        }
        private void OnLobbyChatMessage(LobbyChatMsg_t callback)
        {
            if (callback.m_ulSteamIDLobby != _lobbysystem._lobbyId.m_SteamID)
                return;

            byte[] messageData = new byte[4096];
            int messageLength;
            EChatEntryType chatEntryType;
            CSteamID senderId;
            CSteamID sId = new CSteamID(callback.m_ulSteamIDLobby);
            messageLength = SteamMatchmaking.GetLobbyChatEntry(sId, (int)callback.m_iChatID, out senderId, messageData, messageData.Length, out chatEntryType);

            string message = Encoding.ASCII.GetString(messageData, 0, messageLength);
            Debug.WriteLine("Received message from " + senderId + ": " + message);

            message = message.Replace("\0", string.Empty);

            //start game
            if (message == "*_StartGame_*")
            {
                foreach (var player in _lobbysystem.ConnectedPlayers)
                {
                    if (player.UserID == _lobbysystem.GetOwnerSteamID())
                    {
                        _ownerPlayer = player;
                        break;
                    }
                }
                _mainPanel.Visible = false;
                _isGameStarted = true;
            }
            else
            {
                DisplayReceiveMessage(message);
            }
        }
        private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            if (callback.m_ulSteamIDLobby != _lobbysystem._lobbyId.m_SteamID)
                return;
            _lobbysystem.ConnectedPlayers.Clear();
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(_lobbysystem._lobbyId);
            for (int i = 0; i < numMembers; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(_lobbysystem._lobbyId, i);
                SteamPlayer player = new SteamPlayer(memberId, string.Format("Player{0}", i + 1));
                _lobbysystem.ConnectedPlayers.Add(player);
            }
            
            UpdatePlayerListInMainPanel();
        }

        public void SteamNetworkingMessagesSessionRequest(SteamNetworkingMessagesSessionRequest_t request)
        {

            CSteamID clientId = request.m_identityRemote.GetSteamID();

            if (_lobbysystem.ExpectingClient(clientId))
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
        void CreateMainPanel()
        {
            _createPanel.Visible = false;

            _mainPanel = new Panel();
            _mainPanel.Width = 800;
            _mainPanel.Height = 480;

            var label1 = new Label { Width = 100, Height = 50, Top = 0, Left = 0, Text = "Player1:" + _lobbysystem._lobbyId.m_SteamID.ToString(), Id = "Player1", TextColor = Color.Green };
            _mainPanel.AddChild(label1);
            var label2 = new Label { Width = 100, Height = 50, Top = 55, Left = 0, Text = "", Id = "Player2", TextColor = Color.Green };
            _mainPanel.AddChild(label2);
            var label3 = new Label { Width = 100, Height = 50, Top = 110, Left = 0, Text = "", Id = "Player3", TextColor = Color.Green };
            _mainPanel.AddChild(label3);
            var label4 = new Label { Width = 100, Height = 50, Top = 165, Left = 0, Text = "", Id = "Player4", TextColor = Color.Green };
            _mainPanel.AddChild(label4);

            var receiveMessage = new Label { Width = 500, Height = 200, Top = 100, Left = 400, Text = "", Id = "ReceiveMessage", TextColor = Color.Green };
            _mainPanel.AddChild(receiveMessage);

            var textbox = new TextBox { Width = 500, Height = 200, Top = 200, Left = 0, Id = "SendMessage", TextColor = Color.White };
            _mainPanel.AddChild(textbox);

            var button = new TextButton { Width = 100, Height = 70, Top = 200, Left = 550, Text = "SendMessage", Id = "SendMessageBtn", TextColor = Color.Green };
            button.Click += new EventHandler((sender, e) => SendMessage(sender, e, "message"));
            _mainPanel.AddChild(button);

            if (_lobbysystem._isCreator)
            {
                var start = new TextButton { Width = 100, Height = 50, Top = 420, Left = 400, Text = "StartGame", Id = "StartGame", TextColor = Color.Green };
                start.Click += StarteGame;
                _mainPanel.AddChild(start);
            }
            _desktop.Root = _mainPanel;
        }

        void UpdatePlayerListInMainPanel()
        {
            foreach(var player in _lobbysystem.ConnectedPlayers)
            {
                var label = (Label)_mainPanel.FindWidgetById(player.UserName);
                label.Text=player.UserName+":"+player.UserID.m_SteamID.ToString();
            }
        }

        void DisplayReceiveMessage(string message)
        {
            var messagebox = (Label)_mainPanel.FindWidgetById("ReceiveMessage");
            messagebox.Text = message;
        }

        private void SendMessage(object sender, EventArgs e, string key="message")
        {
            if (key == "message")
            {
                var message = (TextBox)_mainPanel.FindChildById("SendMessage");
                if (message.Text != null)
                {
                    _lobbysystem.SendMessageToLobby(message.Text);
                }
            }
            else {
                //notification to all player "Game is just Started"
                _lobbysystem.SendMessageToLobby(key);
            }
        }
    }
    
}