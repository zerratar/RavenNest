﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StateDataRefresher
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                FixStateDataExpiry(args[0]);
            }
            else
            {
                FixStateDataExpiry("state-data.json");
            }
        }

        static void FixStateDataExpiry(string input)
        {
            if (!System.IO.File.Exists(input))
            {
                Console.WriteLine(System.IO.Path.GetFullPath(input) + " does not exist!");
                Console.ReadKey();
                return;
            }

            var state = Newtonsoft.Json.JsonConvert.DeserializeObject<GameCacheState>(System.IO.File.ReadAllText(input));
            state.Created = DateTime.UtcNow;
            System.IO.File.WriteAllText(input, Newtonsoft.Json.JsonConvert.SerializeObject(state));
        }
    }


    public struct GameCacheState
    {
        public System.DateTime Created { get; set; }
        public List<GameCachePlayerItem> Players { get; set; }
    }

    public class GameCachePlayerItem
    {
        public TwitchPlayerInfo TwitchUser { get; set; }
        public System.Guid CharacterId { get; set; }
        public string NameTagHexColor { get; set; }
        public int CharacterIndex { get; set; }
    }


    public class TwitchPlayerInfo
    {
        public TwitchPlayerInfo() { }

        public TwitchPlayerInfo(
            string userId,
            string username,
            string displayName,
            string color,
            bool isBroadcaster,
            bool isModerator,
            bool isSubscriber,
            bool isVip,
            string identifier)
        {
            if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
            Username = username.StartsWith("@") ? username.Substring(1) : username;
            UserId = userId;
            DisplayName = displayName;
            Color = color;
            IsBroadcaster = isBroadcaster;
            IsModerator = isModerator;
            IsSubscriber = isSubscriber;
            IsVip = isVip;
            Identifier = identifier;

            if (Identifier != null && Identifier.Length > 0)
            {
                var allowedCharacters = "_=qwertyuiopåasdfghjklöäzxcvbnm1234567890".ToArray();
                Identifier = string.Join("", Identifier.ToArray().Where(x => allowedCharacters.Contains(Char.ToLower(x))));
            }
            //if (Identifier[0] == '󠀀')
            //{
            //}
        }

        public string Username { get; set; }
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public string Color { get; set; }
        public bool IsBroadcaster { get; set; }
        public bool IsModerator { get; set; }
        public bool IsSubscriber { get; set; }
        public bool IsVip { get; set; }
        public string Identifier { get; set; }
    }
}
