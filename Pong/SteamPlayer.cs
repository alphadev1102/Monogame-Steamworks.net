using Steamworks;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pong
{
     class SteamPlayer
    {
        public CSteamID UserID;
        public string UserName;
        public Vector2 Position;
        public SteamPlayer()
        {
            UserID = new CSteamID(0);
            UserName = "Player";
            Position = Vector2.Zero;
        }
        public SteamPlayer(CSteamID userID, string userName = "Player")
        {
            UserID = userID;
            UserName = userName;
        }
        public SteamPlayer(CSteamID userID,string userName, Vector2 position)
        {
            UserID = userID;
            UserName = userName;
            Position = position;
        }
    }
}
