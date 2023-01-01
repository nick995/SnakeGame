//Min-Gyu Jung && SangYoon Cho.
// 9th December, 2022

using System.Xml;
using GameWorld;
using System.Runtime.Serialization;
using NetworkUtil;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SnakeGame;

namespace Server
{
    public class Server
    {
        //For storing the command to each client.
        private Dictionary<long, string> CommandStore;

        // List for checking respawn frame of snakes and powerups
        private List<int> respawnFrame;
        private List<int> respawnPowerup;

        private World? theWorld;
        private Random rand = new();

        // The default setting of snakes and powerups
        private float _snakeSpeed = 3;
        private int _startingLength = 120;
        private int _snakeGrowth = 12;
        private int _maxSnake = 50;
        private int _maxPowerups = 20;
        private int _maxPowerupDelay = 200;

        // XML setting
        private static int _universeSize;
        private static long _timePerFrame;
        private static long _framesPerShot;
        private static long _respawnDelay;

        static void Main(string[] args)
        {
            Server server = new Server();
            //Read xaml file and set up datas for setting.
            server.ReadAndSet();

            server.StartServer();

            Console.Read();
        }


        /// <summary>
        /// Server default constructor
        /// </summary>
        public Server()
        {
            CommandStore = new Dictionary<long, string>();
            respawnPowerup = new List<int>() { };
            respawnFrame = new List<int>() { };
        }


        /// <summary>
        /// Start the server and get ready for accepting clients
        /// </summary>
        public void StartServer()
        {
            //Beginning the event loop 
            Networking.StartServer(NewClientConnected, 11000);

            Console.WriteLine("Server is running. Accepting clients");
        }


        /// <summary>
        /// Accept client callback method
        /// </summary>
        /// <param name="state"></param>
        /// 
        private void NewClientConnected(SocketState state)
        {
            try
            {
                //Max Client is 50.
                if (theWorld.SnakePlayers.Count <= _maxSnake)
                {
                    //https://stackoverflow.com/questions/363377/how-do-i-run-a-simple-bit-of-code-in-a-new-thread
                    //Create a new Thread for "one client"                    
                    new Thread(() =>
                    {
                        if (state.ErrorOccurred)
                        {
                            return;
                        }
                        //Change the state's network action to Receivname
                        //for getting client name.
                        state.OnNetworkAction = ReceiveName;

                        Networking.GetData(state);
                    }).Start();
                }
                else
                    throw new Exception();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Server is full");
                return;
            }
        }

        /// <summary>
        /// Receive client's name and save it to the world. 
        /// Also make a new snake for client, and send client's information, player's name and player's unique ID and walls.
        /// </summary>
        /// <param name="state"></param>
        private void ReceiveName(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                return;
            }


            lock (theWorld)
            {
                //get name from state.
                string playerName = state.GetData();
                //remove "\n" since we are handling only one client
                string replacement = Regex.Replace(playerName, @"\t|\n|\r", "");
                //create the new snake by uniqueId and playername.
                Snake playerSnake = SnakeRespawn(state.ID, replacement, 0);
                //Add snake to the world.
                theWorld.SnakePlayers.Add(state.ID, playerSnake);

                respawnFrame.Add(0);
                //remove buffer
                state.RemoveData(0, playerName.Length);

                // Save clinet information into the clients list.
                CommandStore[state.ID] = playerSnake.currDIR;
                // Alert that Client is connected.
                Console.WriteLine("Client " + state.ID + " is connected.");

                //Lock the state when we send the data to the network for Race Condition.
                lock (state)
                {
                    //Send Client Unique Id and universe size.
                    Networking.Send(state.TheSocket!, state.ID + "\n" + _universeSize + "\n");
                    //Send all Walls information that was stored at World.
                    foreach (Wall w in theWorld.Walls.Values)
                        Networking.Send(state.TheSocket, JsonConvert.SerializeObject(w) + "\n");
                }

                // ***********************************************************************************************
                // ***********************************************************************************************
                // ***After this point, the server begins sending the new client the world state on each frame.***
                // ***********************************************************************************************
                // ***********************************************************************************************
            }

            state.OnNetworkAction = ReceiveCommand;


            Run(state);
        }

        /// <summary>
        /// This method is invoked by OnNetworkAction of state.
        /// This is where actually game started.
        /// 
        /// if client have disconnection issue, make snake inactive.
        /// </summary>
        /// <param name="state"></param>
        /// 
        public void Run(SocketState state)
        {
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

            watch.Start();

            while (true)
            {
                if (state.ErrorOccurred)
                {
                    Console.WriteLine("Client " + state.ID + " is disconnected.");
                    //if snake is disconnected, died and disconnected should be true on one frame.
                    theWorld.SnakePlayers[state.ID].Disconnected = true;
                    return;
                }
                //Busy Loop. It is okay for our purposes.
                while (watch.ElapsedMilliseconds < _timePerFrame)
                { }
                watch.Restart();
                //Update the world.
                //lock the world while updating.
                lock (theWorld)
                {
                    Update(state);
                }
                //send the data to the network and lock the world for not modified while we are sending data. 
                lock (theWorld)
                {
                    //if state is still connected, send it.

                    //Send "all of snakes" information in the world 
                    foreach (Snake s in theWorld.SnakePlayers.Values)
                    {
                        //We need to send the at least once to let client know if there is disconnected snake or not.
                        if (s.Disconnected)
                        {
                            s.Died = true;
                            s.Alive = false;
                            Networking.Send(state.TheSocket, JsonConvert.SerializeObject(s) + "\n");
                        }
                        else
                        {
                            Networking.Send(state.TheSocket, JsonConvert.SerializeObject(s) + "\n");
                        }

                    }
                    //Send "all of powerup" information in the world. 
                    foreach (PowerUp p in theWorld.PowerUps.Values)
                        Networking.Send(state.TheSocket, JsonConvert.SerializeObject(p) + "\n");
                }
                //Save disconnected snake from the world.
                IEnumerable<long> playersToRemove = theWorld.SnakePlayers.Values.Where(x => x.Disconnected).Select(x => x.UniqueID);
                //remove all of disconnected snakes.
                foreach (long i in playersToRemove)
                {
                    theWorld.SnakePlayers.Remove(i);
                }

                Networking.GetData(state);

            }
        }

        /// <summary>
        /// This method is invoked by OnNetworkAction of state.
        /// This receives player's input command for direction of snake.
        /// </summary>
        /// <param name="state"></param>
        private void ReceiveCommand(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                return;
            }
            //lock the state while we are receiving command.
            lock (state)
            {
                string commandData = state.GetData();

                string[] parts = Regex.Split(commandData, @"(?<=[\n])");

                foreach (string p in parts)
                {
                    try
                    {
                        //Convert JSON type to the JToken to get "moving"
                        JToken move = JObject.Parse(p)["moving"].ToString();
                        //If moving is none, we do not need to change direction.
                        if (!move.ToString().Equals("none") && theWorld is not null && !CommandStore[state.ID].Equals("none"))
                        {
                            //store the move to the commandStore to change direction.
                            CommandStore[state.ID] = move.ToString();
                        }
                        state.RemoveData(0, p.Length);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }


        /// <summary>
        /// This method is for collision between snake and wall.
        /// 
        /// It is used separately when a snake is respawned and when snake hits a wall.
        /// </summary>
        /// <param name="s"> Snake object of the player </param>
        /// <param name="w"> Wall object in the world </param>
        /// <param name="respawn"> Respawn checker </param>
        /// <returns> if collision is occurred, return true
        ///                                     o.w. false </returns>
        private bool checkSWCollision(Snake s, Wall w, bool respawn)
        {
            // Range of X of walls collision
            double xUpperVertex = 0;
            double xlowerVertex = 0;
            // Range of Y of walls collision
            double yUpperVertex = 0;
            double ylowerVertex = 0;

            // When p1's and p2's X-coordinate is same
            if (w.Point1.X == w.Point2.X)
            {
                // Range of X is fixed by one of X.
                xUpperVertex = w.Point1.X + 25.0;
                xlowerVertex = w.Point1.X - 25.0;


                // When p1's Y is larger than p2's Y
                if (w.Point1.Y > w.Point2.Y)
                {
                    // Set upper range to p1's Y
                    yUpperVertex = w.Point1.Y + 25.0;
                    ylowerVertex = w.Point2.Y - 25.0;
                }
                else// When p1's Y is larger than p2's Y
                {
                    // Set upper range to p1's Y
                    yUpperVertex = w.Point2.Y + 25.0;
                    ylowerVertex = w.Point1.Y - 25.0;
                }
            }
            // When p1's and p2's Y-coordinate is same, act as above.
            else if (w.Point1.Y == w.Point2.Y)
            {
                yUpperVertex = w.Point1.Y + 25.0;
                ylowerVertex = w.Point1.Y - 25.0;

                if (w.Point1.X > w.Point2.X)
                {
                    xUpperVertex = w.Point1.X + 25.0;
                    xlowerVertex = w.Point2.X - 25.0;
                }
                else
                {
                    xUpperVertex = w.Point2.X + 25.0;
                    xlowerVertex = w.Point1.X - 25.0;
                }
            }
            // If it is used as collision of snakes
            if (!respawn)
            {
                // Check if snake's head is reached to the range of the walls collision box.
                if (s.Body.Last().X >= xlowerVertex && s.Body.Last().X <= xUpperVertex)
                    if (s.Body.Last().Y >= ylowerVertex && s.Body.Last().Y <= yUpperVertex)
                        return true;
            }            // If it is used as possibility of respawning
            else
            {
                // Check head's coordinates if respawn's coordinate cannot be used because it is inside of the walls collision box.
                if (s.Body.Last().X >= xlowerVertex - 125 && s.Body.Last().X <= xUpperVertex + 125)
                    if (s.Body.Last().Y >= ylowerVertex - 125 && s.Body.Last().Y <= yUpperVertex + 125)
                        return true;
                // Also check tail's coordinates if respawn's coordinate cannot be used because it is inside of the walls collision box.
                if (s.Body.First().X >= xlowerVertex - 125 && s.Body.First().X <= xUpperVertex + 125)
                    if (s.Body.First().Y >= ylowerVertex - 125 && s.Body.First().Y <= yUpperVertex + 125)
                        return true;
            }

            // Otherwise, it means snake's coordinate doesn't collide with walls
            return false;
        }

        /// <summary>
        /// This method is for collision between snake and powerups.
        /// </summary>
        /// <param name="s"> Snake object of the player </param>
        /// <param name="o"> Powerup object of the world </param>
        /// <returns> if collision is occurred, return true
        ///                                     o.w. false </returns>
        private bool checkSPCollision(Snake s, PowerUp o)
        {
            // The length between snake's head coordinates and powerup's coordinates
            double SnakePointLength = (o.Location - s.Body.Last()).Length();

            if (SnakePointLength < 15.0)
                return true;

            return false;
        }

        /// <summary>
        /// This method is for collision between snakes.
        /// </summary>
        /// <param name="s"> Snake object </param>
        /// <param name="s2"> The other snake object </param>
        /// <returns> if collision is occurred, return true
        ///                                     o.w. false </returns>
        private bool checkSSCollision(Snake s, Snake s2)
        {
            // Check every snake's body(vertices)
            // It compare its vertex with other vertex which is right next to its vertex.
            for (int i = 0; i < s2.Body.Count - 1; i++)
            {
                // Same as walls as above.
                double xUpperVertex = 0;
                double xlowerVertex = 0;
                double yUpperVertex = 0;
                double ylowerVertex = 0;


                if (s2.Body[i].X == s2.Body[i + 1].X)
                {
                    xUpperVertex = s2.Body[i].X + 5.0;
                    xlowerVertex = s2.Body[i].X - 5.0;

                    // Check coordinate which one is larger
                    if (s2.Body[i].Y > s2.Body[i + 1].Y)
                    {
                        yUpperVertex = s2.Body[i].Y + 5.0;
                        ylowerVertex = s2.Body[i + 1].Y - 5.0;
                    }
                    else
                    {
                        yUpperVertex = s2.Body[i + 1].Y + 5.0;
                        ylowerVertex = s2.Body[i].Y - 5.0;
                    }
                }
                // Same as above
                else if (s2.Body[i].Y == s2.Body[i + 1].Y)
                {
                    yUpperVertex = s2.Body[i].Y + 5.0;
                    ylowerVertex = s2.Body[i].Y - 5.0;

                    if (s2.Body[i].X > s2.Body[i + 1].X)
                    {
                        xUpperVertex = s2.Body[i].X + 5.0;
                        xlowerVertex = s2.Body[i + 1].X - 5.0;
                    }
                    else
                    {
                        xUpperVertex = s2.Body[i + 1].X + 5.0;
                        xlowerVertex = s2.Body[i].X - 5.0;
                    }
                }

                // Check if snake's head coordinate hits other snake's body
                if (s.Body.Last().X >= xlowerVertex && s.Body.Last().X <= xUpperVertex)
                    if (s.Body.Last().Y >= ylowerVertex && s.Body.Last().Y <= yUpperVertex)
                        return true;
            }

            return false;
        }

        /// <summary>
        /// This method is for self-collision.
        /// </summary>
        /// <param name="s"> self </param>
        /// <param name="s2"> self </param>
        /// <returns> if collision is occurred, return true
        ///                                     o.w. false </returns>
        private bool checkSSSelfCollision(Snake s)
        {
            // It would only check if snake makes 5 vertices since it would not be collided if snake's body is less than 5.
            if (s.Body.Count >= 5)
            {
                // And check only after fourth's vertex of the body.
                // Lines created by head, first, second, and third vertex from the head is never collided with head.
                for (int i = 0; i < s.Body.Count - 4; i++)
                {
                    double xUpperVertex = 0;
                    double xlowerVertex = 0;
                    double yUpperVertex = 0;
                    double ylowerVertex = 0;


                    if (s.Body[i].X == s.Body[i + 1].X)
                    {
                        xUpperVertex = s.Body[i].X + 5.0;
                        xlowerVertex = s.Body[i].X - 5.0;

                        if (s.Body[i].Y > s.Body[i + 1].Y)
                        {
                            yUpperVertex = s.Body[i].Y + 5.0;
                            ylowerVertex = s.Body[i + 1].Y - 5.0;
                        }
                        else
                        {
                            yUpperVertex = s.Body[i + 1].Y + 5.0;
                            ylowerVertex = s.Body[i].Y - 5.0;
                        }
                    }
                    else if (s.Body[i].Y == s.Body[i + 1].Y)
                    {
                        yUpperVertex = s.Body[i].Y + 5.0;
                        ylowerVertex = s.Body[i].Y - 5.0;

                        if (s.Body[i].X > s.Body[i + 1].X)
                        {
                            xUpperVertex = s.Body[i].X + 5.0;
                            xlowerVertex = s.Body[i + 1].X - 5.0;
                        }
                        else
                        {
                            xUpperVertex = s.Body[i + 1].X + 5.0;
                            xlowerVertex = s.Body[i].X - 5.0;
                        }
                    }

                    // Check if snake's head coordinate hits its body box.
                    if (s.Body.Last().X >= xlowerVertex && s.Body.Last().X <= xUpperVertex)
                        if (s.Body.Last().Y >= ylowerVertex && s.Body.Last().Y <= yUpperVertex)
                            return true;
                }
            }

            return false;
        }

        /// <summary>
        /// This is for respawning of powerups. Check if powerups is overlaped with walls.
        /// </summary>
        /// <param name="p"> PowerUp object </param>
        /// <param name="w"> Walls object </param>
        /// <returns> if collision is occurred, return true
        ///                                     o.w. false </returns>
        private bool checkPWCollision(Vector2D p, Wall w)
        {
            // Check wall's box.
            double xUpperVertex = 0;
            double xlowerVertex = 0;
            double yUpperVertex = 0;
            double ylowerVertex = 0;


            if (w.Point1.X == w.Point2.X)
            {
                xUpperVertex = w.Point1.X + 25.0;
                xlowerVertex = w.Point1.X - 25.0;

                if (w.Point1.Y > w.Point2.Y)
                {
                    yUpperVertex = w.Point1.Y + 25.0;
                    ylowerVertex = w.Point2.Y - 25.0;
                }
                else
                {
                    yUpperVertex = w.Point2.Y + 25.0;
                    ylowerVertex = w.Point1.Y - 25.0;
                }
            }
            else if (w.Point1.Y == w.Point2.Y)
            {
                yUpperVertex = w.Point1.Y + 25.0;
                ylowerVertex = w.Point1.Y - 25.0;

                if (w.Point1.X > w.Point2.X)
                {
                    xUpperVertex = w.Point1.X + 25.0;
                    xlowerVertex = w.Point2.X - 25.0;
                }
                else
                {
                    xUpperVertex = w.Point2.X + 25.0;
                    xlowerVertex = w.Point1.X - 25.0;
                }
            }

            // If Powerups is on the walls collision box, return true.
            if (p.X <= xlowerVertex - 45 && p.X >= xUpperVertex + 45)
                if (p.Y >= ylowerVertex - 45 && p.Y <= yUpperVertex + 45)
                    return true;

            return false;
        }

        /// <summary>
        /// This is for respawning snakes.
        /// It checks collision if there're something under the snake.
        /// </summary>
        /// <param name="id"> Player's unique ID </param>
        /// <param name="name"> Player's name </param>
        /// <param name="score"> Player's score. It would be zero </param>
        /// <returns> Create Snake with new settings </returns>
        private Snake SnakeRespawn(long id, string name, int score)
        {
            // Make a new snake
            Snake s = new Snake(id, name, score);

            // For dir setting of the snake.
            int dir = rand.Next(4);

            // Set coordinates of the snake
            double HeadX = 0;
            double HeadY = 0;
            double TailX = 0;
            double TailY = 0;

            // respawn checker
            bool canRespawn = true;

            // If snake can't respawn on random coordinates, reset the coordinates.
            // And it makes loop until find appropriate coordinates
            while (canRespawn)
            {
                // Positive or Negative
                if (rand.Next(2) % 2 == 0)
                    HeadX = rand.Next(_universeSize / 2 - 50);
                else
                    HeadX = rand.Next(_universeSize / 2 - 50) * (-1);
                // Positive or Negative
                if (rand.Next(2) % 2 == 0)
                    HeadY = rand.Next(_universeSize / 2 - 50);
                else
                    HeadY = rand.Next(_universeSize / 2 - 50) * (-1);

                // Set the coordinates
                Vector2D p1 = new Vector2D(HeadX, HeadY);

                // Dir setting
                if (dir % 4 == 0)
                {   // Up direction
                    s.Dir = new Vector2D(0, -1);
                    s.currDIR = "up";
                    TailX = HeadX;
                    TailY = HeadY + _startingLength;
                }
                else if (dir % 4 == 1)
                {   // Down direction
                    s.Dir = new Vector2D(0, 1);
                    s.currDIR = "down";
                    TailX = HeadX;
                    TailY = HeadY - _startingLength;
                }
                else if (dir % 4 == 2)
                {   // Left direction
                    s.Dir = new Vector2D(-1, 0);
                    s.currDIR = "left";
                    TailY = HeadY;
                    TailX = HeadX + _startingLength;
                }
                else
                {   // Right direction
                    s.Dir = new Vector2D(1, 0);
                    s.currDIR = "right";
                    TailY = HeadY;
                    TailX = HeadX - _startingLength;
                }

                // Also make tail's coordinates
                Vector2D p2 = new Vector2D(TailX, TailY);

                // Add two vector values into the body list.
                s.Body.Add(p2); s.Body.Add(p1);

                // Collision checker between every objects of the world
                foreach (PowerUp p in theWorld.PowerUps.Values)
                    canRespawn = checkSPCollision(s, p);
                foreach (Wall w in theWorld.Walls.Values)
                    canRespawn = checkSWCollision(s, w, true);
                foreach (Snake snake in theWorld.SnakePlayers.Values)
                    if (!s.Equals(snake))
                        canRespawn = checkSSCollision(s, snake);
            }

            return s;
        }

        /// <summary>
        /// This is for respawning powerups.
        /// It checks collision if there're something under the powerups.
        /// </summary>
        /// <param name="id"> powerup's unique ID </param>
        /// <returns> New powerup object with new coordinates </returns>
        private PowerUp powerUpRespawn(int id)
        {
            // Same as above, snakerespawn.
            bool canRespawn = true;

            double HeadX = 0;
            double HeadY = 0;


            while (canRespawn)
            {

                if (rand.Next(2) % 2 == 0)
                    HeadX = rand.Next(_universeSize / 2 - 50);
                else
                    HeadX = rand.Next(_universeSize / 2 - 50) * (-1);

                if (rand.Next(2) % 2 == 0)
                    HeadY = rand.Next(_universeSize / 2 - 50);
                else
                    HeadY = rand.Next(_universeSize / 2 - 50) * (-1);

                // Set the coordinates of the powerup
                Vector2D v = new Vector2D(HeadX, HeadY);

                // Collision check
                foreach (Wall w in theWorld.Walls.Values)
                    canRespawn = checkPWCollision(v, w);
            }

            PowerUp p = new PowerUp(id, HeadX, HeadY);
            return p;
        }

        /// <summary>
        /// This method is invoked by Run() method.
        /// 
        /// Updating every action of objects in the world. 
        /// If some actions are happened, update informations in the world object with a new information.
        /// </summary>
        /// <param name="state"></param>
        public void Update(SocketState state)
        {
            // Respawn beginner. If player dies and don't alive yet, check time fitting by setting respawn time.
            // If player was disconnected, don't check respawn time.
            if (!theWorld.SnakePlayers[state.ID].Alive && !theWorld.SnakePlayers[state.ID].Disconnected)
            {
                // Check respawn time of the player as player's ID
                respawnFrame[(int)state.ID] += 1;
                if (respawnFrame[(int)state.ID] == _respawnDelay)
                {
                    // Respawn player's snake with setting new snake.
                    respawnFrame[(int)state.ID] = 0;
                    theWorld.SnakePlayers[state.ID].Alive = true;
                    theWorld.SnakePlayers[state.ID] = SnakeRespawn(state.ID, theWorld.SnakePlayers[state.ID].Name, 0);
                }
            }

            // Get direction information by receiving command data from the client.
            theWorld.SnakePlayers[state.ID].setDIR(CommandStore[state.ID]);

            // Respawn powerups. It would act same as respawning players.
            for (int i = 0; i < 20; i++)
            {
                if (theWorld.PowerUps[i].Died)
                {
                    respawnPowerup[i] += 1;
                    if (respawnPowerup[i] == _maxPowerupDelay)
                    {
                        theWorld.PowerUps[i] = powerUpRespawn(i);
                    }
                }
            }

            // Update movement of the player
            theWorld.SnakePlayers[state.ID].Step(_snakeSpeed, _universeSize);

            // Check collision of the walls
            foreach (Wall w in theWorld.Walls.Values)
                if (checkSWCollision(theWorld.SnakePlayers[state.ID], w, false))
                {
                    // If collision was occurred, set the player died.
                    theWorld.SnakePlayers[state.ID].Alive = false;
                    theWorld.SnakePlayers[state.ID].Died = true;
                }
            // Check collision of the snake
            foreach (Snake s2 in theWorld.SnakePlayers.Values)
            {
                // Check collision of itself
                if (checkSSSelfCollision(theWorld.SnakePlayers[state.ID]))
                {
                    // If collision was occurred, set the player died.
                    theWorld.SnakePlayers[state.ID].Alive = false;
                    theWorld.SnakePlayers[state.ID].Died = true;
                }
                else
                {
                    // Check collision of other snakes
                    if (!theWorld.SnakePlayers[state.ID].Equals(s2) && s2.Alive)
                    {
                        if (checkSSCollision(theWorld.SnakePlayers[state.ID], s2))
                        {
                            // If collision was occurred, set the player died.
                            theWorld.SnakePlayers[state.ID].Alive = false;
                            theWorld.SnakePlayers[state.ID].Died = true;
                        }
                    }
                }
            }
            // Check collision of the powerups
            foreach (PowerUp p in theWorld.PowerUps.Values)
            {
                if (checkSPCollision(theWorld.SnakePlayers[state.ID], p) && !p.Died)
                {
                    // If collision was occurred, add snake's length according to the setting
                    theWorld.SnakePlayers[state.ID].GetScore();
                    p.Died = true;
                }
            }
            // Logic of adding length of the snake. Invoke it.
            if (theWorld.SnakePlayers[state.ID].getScore)
                theWorld.SnakePlayers[state.ID].Count(_snakeGrowth);
        }

        public void ReadAndSet()
        {

            DataContractSerializer ser = new(typeof(GameSettings));

            XmlReader reader = XmlReader.Create(@"..\..\..\settings.xml");

            GameSettings gameSettings = (GameSettings)ser.ReadObject(reader);

            _universeSize = gameSettings.UniverseSize;
            _framesPerShot = gameSettings.FramesPerShot;
            _respawnDelay = gameSettings.RespawnRate;
            _timePerFrame = gameSettings.MSPerFrame;

            theWorld = new World(gameSettings.UniverseSize);
            //Add all walls information to the world.
            foreach (Wall wall in gameSettings.Walls)
            {
                theWorld.Walls.Add(wall.WallID, wall);
            }
            for (int i = 0; i < _maxPowerups; i++)
            {
                theWorld.PowerUps[i] = powerUpRespawn(i);
                respawnPowerup.Add(0);
            }
        }
    }
}