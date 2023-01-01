/**
 * This is for powerup object setting.
 */
using Newtonsoft.Json;
using SnakeGame;

namespace GameWorld
{
    [JsonObject(MemberSerialization.OptIn)]
    public class PowerUp
    {
        [JsonProperty(PropertyName = "power")]
        public int Power;

        [JsonProperty(PropertyName = "loc")]
        public Vector2D Location;

        [JsonProperty(PropertyName = "died")]
        public bool Died { get; set; } = false;

        // Constructor
        public PowerUp(int id, double x, double y)
        {
            Power = id;
            Location = new Vector2D(x, y);
        }
    }
}