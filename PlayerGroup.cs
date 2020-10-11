using System;
using System.Collections.Generic;

namespace TestMmoServer
{
    public class PlayerGroup
    {
        public Dictionary<string, Player> Players { get; private set; } = new Dictionary<string, Player>();

        public string AddNewPlayer()
        {
            var player = new Player
            {
                Id = Guid.NewGuid().ToString(),
                X = -1,
                Y = -1
            };

            Players.Add(player.Id, player);

            return player.Id;
        }
    }
}