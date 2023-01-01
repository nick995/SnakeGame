/**
 * This code is for game world
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameWorld
{
    public class World
    {
        public Dictionary<long, Snake> SnakePlayers;
        public Dictionary<long, PowerUp> PowerUps;
        public Dictionary<long, Wall> Walls;
        public int Size { get; private set; }

        // Default world setting
        public World(int _size)
        {
            SnakePlayers = new Dictionary<long, Snake>();
            PowerUps = new Dictionary<long, PowerUp>();
            Walls = new Dictionary<long, Wall>();
            Size = _size;
        }
    }
}