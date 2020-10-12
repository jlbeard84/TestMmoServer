using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TestMmoServer
{
    public class PlayerGroup
    {
        public ConcurrentDictionary<string, Player> Players { get; private set; } = new ConcurrentDictionary<string, Player>();

        public string AddNewPlayer()
        {
            var player = new Player
            {
                Id = Guid.NewGuid().ToString(),
                X = -1,
                Y = -1
            };

            Players.TryAdd(player.Id, player);

            return player.Id;
        }

        public void UpdatePlayerPosition(
            string playerId, 
            float x,
            float y)
        {
            if (Players.TryGetValue(playerId, out var player)) {
                player.X = x;
                player.Y = y;

                Players[playerId] = player;
            }
        }
    }
}