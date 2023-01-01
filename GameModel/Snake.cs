/**
 * This code is for snakes of the world
 */

using Newtonsoft.Json;
using SnakeGame;

namespace GameWorld
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Snake
    {
        //snake's unique ID
        [JsonProperty(PropertyName = "snake")]
        public long UniqueID { get; private set; }
        //player's name
        [JsonProperty(PropertyName = "name")]
        public string Name { get; private set; }
        [JsonProperty(PropertyName = "body")]
        public List<Vector2D> Body { get; private set; }
        [JsonProperty(PropertyName = "dir")]
        public Vector2D Dir { get;  set; }
        [JsonProperty(PropertyName = "score")]
        public int Score { get; set; }
        [JsonProperty(PropertyName = "died")]
        public bool Died { get; set; } = false;
        [JsonProperty(PropertyName = "alive")]
        public bool Alive { get; set; } = true;
        [JsonProperty(PropertyName = "dc")]
        public bool Disconnected { get; set; } = false;
        [JsonProperty(PropertyName = "join")]
        public bool Join { get; private set; } = false;

        // Direction change checker
        public bool DirChange { get; set; } = false;
        // Show the current direction of the snake
        public string currDIR { get; set; }
        // Frame checker for respawn
        private int frameCount = -1;
        // Checker if snake gets score
        public bool getScore { get; set; } = false;

        // Snake constructor
        public Snake(long id, string name, int score)
        {
            this.UniqueID = id;
            this.Name = name;
            this.Body = new List<Vector2D>();
            this.Dir = new Vector2D(0, -1);
            this.Score = score;
            this.currDIR = "up";
        }

        // Count frame for grow-up length with default setting.
        public void Count(int snakeGrowth)
        {
            frameCount += 1;
            if (frameCount == snakeGrowth)
            {
                getScore = false;
                frameCount = -1;
            }
        }

        /// <summary>
        /// Change direction based on player's command
        /// </summary>
        /// <param name="dir"> Command information from the player </param>
        public void setDIR(string dir)
        {
            // If dir is up
            if (dir.Equals("up") && !currDIR.Equals("down") && !currDIR.Equals("up"))
            {
                Dir = new Vector2D(0, -1);
                currDIR = "up";
                DirChange = true;
            }
            // down
            else if (dir.Equals("down") && !currDIR.Equals("up") && !currDIR.Equals("down"))
            {
                Dir = new Vector2D(0, 1);
                currDIR = "down";
                DirChange = true;
            }
            // left
            else if (dir.Equals("left") && !currDIR.Equals("right") && !currDIR.Equals("left"))
            {
                Dir = new Vector2D(-1, 0);
                currDIR = "left";
                DirChange = true;
            }
            // right
            else if (dir.Equals("right") && !currDIR.Equals("left") && !currDIR.Equals("right"))
            {
                Dir = new Vector2D(1, 0);
                currDIR = "right";
                DirChange = true;
            }
            else // if command is none
                return;
        }
        /// <summary>
        /// This method is invoked when player gets powerup
        /// </summary>
        /// <returns></returns>
        public bool GetScore()
        {
            this.Score += 1;
            return getScore = true;
        }
        /// <summary>
        /// Snake movement in the world
        /// </summary>
        /// <param name="velocity"> Default setting from the server </param>
        /// <param name="worldsize"> Default setting from the Setting XML file </param>
        public void Step(float velocity, int worldsize)
        {
            // If snake is alive, it always moves
            if (Alive)
            {
                // If direction is changed by command of the player, add vertex into the body of the snake.
                if (DirChange)
                {
                    Body.Add(new Vector2D(Body.Last()));
                    DirChange = false;
                }

                // Wraparound world border checker
                if (Body.Last().X >= worldsize / 2)
                {
                    Body.Add(new Vector2D(Body.Last()));
                    Body.Last().X = -worldsize / 2;
                    Body.Add(new Vector2D(Body.Last()));
                }
                else if (Body.Last().Y >= worldsize / 2)
                {
                    Body.Add(new Vector2D(Body.Last()));
                    Body.Last().Y = -worldsize / 2;
                    Body.Add(new Vector2D(Body.Last()));
                }
                else if (Body.Last().X <= -worldsize / 2)
                {
                    Body.Add(new Vector2D(Body.Last()));
                    Body.Last().X = worldsize / 2;
                    Body.Add(new Vector2D(Body.Last()));
                }
                else if (Body.Last().Y <= -worldsize / 2)
                {
                    Body.Add(new Vector2D(Body.Last()));
                    Body.Last().Y = worldsize / 2;
                    Body.Add(new Vector2D(Body.Last()));
                }
                // Body length
                int bodyCount = this.Body.Count;
                // Location of the head
                this.Body[bodyCount-1] += this.Dir * velocity;
                // Get tail's direction
                Vector2D tailDir = (Body[1] - Body[0]);
                tailDir.Normalize();
                // If snake gets score, tail does't move forward until 12 frames
                if(!getScore)
                    this.Body[0] += tailDir * velocity;

                // If tail catch-up next vertex, remove tail.
                // Also if tail reaches to the border of the world, remove it.
                if(bodyCount != 2)
                {
                    if (Body[1].Equals(Body[0]))
                        Body.Remove(Body.First());
                    // For border of the world
                    if (Body[0].X >= worldsize/2 || Body[0].X <= -worldsize/2 || Body[0].Y >= worldsize/2 || Body[0].Y <= -worldsize/2)
                        Body.Remove(Body.First());
                }
            }
        }
    }
}