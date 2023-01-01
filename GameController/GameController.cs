/**
 * This class is for Controller of MVC.
 * This controls every data needed to be updated and connection between Server and the Client.
 * When server sends data to the controller, controller class updates information for View through Handler Delegate.
 * Also, when CLient inputs direction information, controller get this information and send it to the server,
 * and then get data again from the server, and update View.
 */

//using GameServer;
using GameWorld;
using NetworkUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using static System.Net.WebRequestMethods;
using System;


namespace GameSystem
{   
    public class GameController
    {
        /// <summary>
        /// This should contain logic for parsing the data received by the server, updating
        /// the model accordingly, and anything elsoe you think belongs here.
        /// Key press handlers in your View should be "landing points"only,
        /// and should invoke controller method that contain the heavy logic.
        /// </summary>


        //Input user name.
        private string? UserName { get; set; }
        // we need to handle the JSON file in here.
        //  We will get Wall, Snake info from the server.
        
        // This is for getting information of unique ID and world size.
        private bool FirstSend = true;

        private int UniqueID { get; set; }

        // getter
        public int getUniqueID()
        {
            return this.UniqueID;
        }

        private int WorldSize = -1;

        // World object variable
        public World? World { get; set; }

        /// <summary>
        /// A delegate and event to fire when the controller has received and processed new info from the server
        /// </summary>
        public delegate void DataHandler();
        public event DataHandler? DatasArrived;

        /// <summary>
        /// A delegate and event to fire when the controller has error and display alert about error.
        /// </summary>
        /// <param name="error"></param>
        public delegate void ErrorHandler(string error);
        public event ErrorHandler? Error;

        /// <summary>
        /// A delegate and event to fire when the controller has error and display alert about error.
        /// </summary>
        public delegate void ConnectedHandler();
        public event ConnectedHandler? Connected;

        /// <summary>
        /// A delegate and event to fire when the controller makes the game world and to initialize the world.
        /// </summary>
        public delegate void WorldCreated();
        public event WorldCreated? WorldCreate;

        // Server Object Variable
        private SocketState? theServer;

        /// <summary>
        /// Begins the process of connecting to the server
        /// </summary>
        /// <param name="address"> The address where client wants to connect </param>
        /// <param name="userName"> Client ID in the game </param>
        public void Connect(string address, string userName)
        {
            // 1. Establish a socket connection to the server on port 11000.
            Networking.ConnectToServer(OnConnect, address, 11000);
            // Set the username from what user inputs.
            this.UserName = userName;
        }

        /// <summary>
        /// Method to be invoked by the networking library when a connection is made
        /// </summary>
        /// <param name="state"> Server socketstate </param>
        private void OnConnect(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                Error?.Invoke("Error connecting to server");
                return;
            }
            // Initialize socket state variable in the gamecontroller class.
            theServer = state;

            if (theServer is not null)
            {
                // Send username information to the server
                Networking.Send(theServer.TheSocket, UserName + "\n");
            }

            //2. Upon connection, send a single '\n' terminated string representing the player's name
            //send user name to the socket
            Connected?.Invoke();

            // Start an event loop to receive messages from the server
            if(theServer is not null)
                theServer.OnNetworkAction = ReceiveData;

            //at this point getting wall info
            Networking.GetData(state);

        }

        /// <summary>
        /// Method to be invoked by the networking library when data is available        
        /// /// </summary>
        /// <param name="state"> Server socketstate </param>
        private void ReceiveData(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                Error?.Invoke("Lost connection to server");
                return;
            }
            // Lock the state for receving data safely. Try not to make race condition.
            lock (state)
            {
                ProcessData(state);
            }
            // Get new data from server.
            Networking.GetData(state);
        }

        /// <summary>
        /// Process any buffered data separated by '\n'
        /// and send buffered data to ParsingData method
        /// </summary>
        /// <param name="state"> Server socketstate </param>
        private void ProcessData(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                Error?.Invoke("Lost connection to server");
                return;
            }

            string totalData = state.GetData();
            //  3. The server will then send two strings representing integer numbers, and each
            //  terminated by a "\n". The first number is the player's unique ID.
            //  The second is the size of the world, representing both the width
            //  the width and height. All game worlds are square.

            string[] parts = Regex.Split(totalData, @"(?<=[\n])");

            foreach (string p in parts)
            {
                // Ignore empty strings added by the regex splitter
                if (p.Length == 0)
                    continue;
                // The regex splitter will include the last string even
                // if it doesn't end with a '\n',
                // So we need to ignore it if this happens.
                if (p[p.Length - 1] != '\n')
                    break;
                // Then remove it from the SocketState's growable buffer
                state.RemoveData(0, p.Length);

                ParsingData(p);

            }
            //inform to view that datas are updated.
            DatasArrived?.Invoke();
        }

        /// <summary>
        /// This method is an actual method that parses every data in the world.
        /// Deserialize JSON file, and store in a Dictionary that matches each key values.
        /// </summary>
        /// <param name="s"> JSON file data </param>
        private void ParsingData(string s)
        {
            if (theServer is not null)
            {
                if (theServer.ErrorOccurred)
                {
                    Error?.Invoke("Lost connection to server");
                    return;
                }

                try
                {
                    //Convert String to JObject 
                    JObject obj = JObject.Parse(s);

                    //If objects' key is wall
                    if (obj.ContainsKey("wall"))
                    {
                        //add wall to the World class.
                        Wall? DeseriWall = JsonConvert.DeserializeObject<Wall>(obj.ToString());

                        if (DeseriWall is not null && World is not null)
                        {
                            // Store Wall information
                            if (World.Walls.ContainsKey(DeseriWall.WallID))
                            {
                                World.Walls[DeseriWall.WallID] = DeseriWall;
                            }
                            else
                            {
                                World.Walls.Add(DeseriWall.WallID, DeseriWall);
                            }
                        }

                    }
                    //if objects' key is snake
                    else if (obj.ContainsKey("snake"))
                    {
                        //add snake to world
                        Snake? DeseriSnake = JsonConvert.DeserializeObject<Snake>(obj.ToString());

                        if (DeseriSnake is not null && World is not null)
                        {
                            // Store snake to the World class.
                            if (DeseriSnake.Disconnected)
                            {
                                World.SnakePlayers.Remove(DeseriSnake.UniqueID);
                            }
                            else
                            {
                                if (World.SnakePlayers.ContainsKey(DeseriSnake.UniqueID))
                                {
                                    World.SnakePlayers[DeseriSnake.UniqueID] = DeseriSnake;
                                }
                                else
                                {
                                    World.SnakePlayers.Add(DeseriSnake.UniqueID, DeseriSnake);
                                }
                            }
                        }
                    }
                    else if (obj.ContainsKey("power"))
                    {
                        //add power to the World class.
                        PowerUp? DeseriPower = JsonConvert.DeserializeObject<PowerUp>(obj.ToString());

                        if (DeseriPower is not null && World is not null)
                        {
                            if (DeseriPower.Died)
                            {
                                World.PowerUps.Remove(DeseriPower.Power);
                            }
                            else
                            {
                                if (World.PowerUps.ContainsKey(DeseriPower.Power))
                                {
                                    World.PowerUps[DeseriPower.Power] = DeseriPower;
                                }
                                else
                                {
                                    World.PowerUps.Add(DeseriPower.Power, DeseriPower);
                                }
                            }
                        }
                    }
                }
                //when string s is not JSON type, it means first send. 
                catch (Exception)
                {
                    if (FirstSend)
                    {
                        this.UniqueID = Convert.ToInt32(s);

                        FirstSend = false;
                    }
                    else
                    {
                        this.WorldSize = Convert.ToInt32(s);
                        World = new World(WorldSize);
                        WorldCreate?.Invoke();
                    }
                }
            }
            else
                return;
        }

        /// <summary>
        /// Sending input data from user to the server.
        /// </summary>
        /// <param name="s"></param>
        public void InputKey(string s)
        {
            //https://stackoverflow.com/questions/13489139/how-to-write-json-string-value-in-code
            //The client shall not send any command requests to the server before
            //receiving its player ID, world size, and walls.
            if(World is not null && (World.SnakePlayers.Count>0 || World.PowerUps.Count>0) || s is not null)
            {
                if (theServer is not null)
                {
                    if(s is not null)
                        Networking.Send(theServer.TheSocket, JsonConvert.SerializeObject(new {moving = s}) + "\n");
                    else
                        Networking.Send(theServer.TheSocket, JsonConvert.SerializeObject(new {moving = "none"}) + "\n");
                }
            }
        }
    }
}