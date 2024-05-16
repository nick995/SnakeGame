# Play Video

https://youtube.com/shorts/dK_DYjRw7tY?feature=share


# SnakeGame

This is the first version of the program that implements a Controller and View of the snake game. This program allows you to play 'simple snake game' through MAUI application.

## Playing

For playing game, please set Server and SnakeClient as Startup Project.

## Features

- We added the cute eyes on the snake
- We added the explosion and it will spread out randomly but not too widely 
- Server Status visible 

### Model

Snakes, PowerUp, and Walls are based on the instruction, we added the default Constructions. The world class will contain Snake(Player), Wall, and PowerUp. We store those data into Dictionary since each object has a Specific Key and in this case, we can access the data fastly.

> GameModel: We set the default constructor of each for the world, but snake has some method

1.	Snake: Snake classes basically consist of methods that implement snake motion. The direction changes depending on the command entered by the player, and when getting(colliding) powerups, the helper method was invoked to stop the tail for 12 frames (default setting). We also created a helper method to make it possible to teleport at the other side when snake is reached to the border of the world.

1.	Thread: When the client successfully connects to the server, we created a thread and separated it from the main thread so that the client's work can be performed on the seperated thread. When a new thread is created, the server receive the necessary information from the client. When the client first sends its name to the server, the server takes the name and creates a new snake object, and sends the information of the object to the user. The server also sends information about the objects created in the world so that users can build a game world. At this time, while creating and sending World's information, lock the World so that the information does not overlap with other threads to prevent the information from being transmitted and received incorrectly. After that, OnNetworkAction of the client's socket is changed to a method of checking the user's command input information, and the Run() method is invoked to update the actual screen of the game.

2.	Run: When the Run() method is invoked, the game frame is set according to the timePerFrame information obtained by the XML setting. If there is an error in the socket state of the client, remove the client from the game world. Update the game information and send it to the client. If the server has successfully transmitted the information, prepare to receive the player's input command data.

3.	Update: The Update() method is a method that manages the information needed to update the game world. It checks the player's respawn time to help the player proceed smoothly when the player dies or comes alive, and makes the power-ups continue to occur. It also keeps checking the collision of each object and updating the information to suit each situation so that the game logic is not broken.

4.	Collision: We implemented the logic that fits the collision of each object.

	- Snake ~ Wall: To calculate the collision with the wall, we first obtained the first and second coordinates of the wall, which are p1 and p2. The reason was to calculate the part where the image of the wall was being drawn on the world. If the X values of the two coordinates are the same, it means that they are drawn vertically, so we found the difference between the Y coordinates, and then we found in which direction the wall was drawn, as vice versa. So we made the range of coordinates of X and Y, which is shaped like boxes, and if the snake's head hit the coordinate value of this box, it told us that a collision occurred. If the snake needed to respawn, it expanded the box's range so that the snake's torso would not create on the wall.
	
	- Snake ~ PowerUp: After calculating the length of the snake's head coordinates and the power-up coordinates, if this length is less than the distance between the head and the power-up, it tells you that the collision has occurred.

	- Snake ~ Snake: The snake-to-snake collision made it similar to logic when the snake and the wall collided. The difference is that we used the coordinates of the vector values in the snake's body list to find the part where the snake's body is being drawn. If the head of snake hits the coordinates of other snake's range box of the body, such like the range box on the wall, it tells you that it has collided. If a snake's head hits each other, both snakes will die.

	- Snake ~ itself: There is also a method that tells you when a snake collides with its body. At this time, we calculate the collision between snakes a little lighter than the logic we calculated. The reason is that the snake rotates 90 degrees when it changes direction, so it needs at least three turns to hit its body. The Vector2D list where the snake is turned is stored in the snake's body information, and the body, including the head drawn from the head (1) to the fourth vertex (4), will never collide with the snake's head. Therefore, we only need to calculate the body vertex after that, so we made it check only if the snake has more than five vertices of the body.

	- Powerup ~ Wall: If the coordinates of the power-up are on the range box of the wall, it will not created.

5. Respawn: We created a helper method that makes snakes and powerups respawning. The head and tail coordinates of the snake were randomly generated to match the world size, and then the collision method created earlier was used to prevent them from being generated in overlapping places. The direction of the snake that is made is also random. The power-up also produced random coordinates similar to snakes.

	


### View

> WorldPanel

We drew canvas using the DrawObjectWithTransform method as much as possible. This is because it was much easier to calculate by moving the location of the canvas of the object than by calculating the world’s coordinates. Instead, if we need to figure and draw coordinates in a row, like the body of a snake, we used two body coordinates which is the world coordinates, and subtract them, and save the result to match the object coordinates to DrawLine coordinates.

You can see that we used foreach with ‘ToList()’ for drawing objects. This is because even though we lock the state to get every data of the world, if Enumerator was modified, it threw exception, called ‘System.InvalidOperationException’, because it was modified. For this reason, we added .ToList() and we can avoid the Exception. The specific information of the exception is below.

'''

System.InvalidOperationException: Collection was modified; enumeration operation may not execute.

'''

1.	GraphicView: First, the center of the graphicView, which is the screen that the user actually watches, is the coordinate of the snake’s head. Through this, we can make the screen move to match where the snake is going. By drawing the background according to the world coordinates, the world did not be drag along even if the graphicView moved.

2.	Snake: We drew the body based on the coordinates of the snake’s head. Also, when drawing the body, we calculated the coordinates of the two attached index body so that we can draw snake properly with using DrawLine method. Therefore, when the head of the body moves, the tail can be drawn according to the coordinates of the head of the body. In the event of Died, the circle appeared randomly, creating the shape of a firework. It also kept the afterimage long enough to make notice that other users knew that the snake was died there.

3.	Wall: X and Y coordinates of starting point and end point were calculated, and we divided the result by the length of one side of the wall so that we can get the number of walls. And then we drew walls according to the point coordinates based on the number of walls.

4.	PowerUp: Like snake, we transformed the world coordinates into object coordinates, and then made them possible to be drawn there easily.

> MainPage.xaml.cs

We used four handlers informed by the Controller, and it allowed us to update the View.

1.	OnFrame: This is a method that draws a canvas in every single frame. Once the updated data is sent to View, View will draw objects on canvas through invalidate() based on that updated information.

2.	DrawingWorld: This method is updated when WorldCreate Handler is invoked. It initializes the World of the game where user actually would play. 

3.	NetworkErrorHandler: This method is updated when a network-related error occurs. If the Controller encounters a network-related error, for example, a socket, it notifies the user that a network-related error has occurred during the game. Also, when this method is updated, it sets the status light informing you of the status of your network connection to red. At this point, activate the Connect button to allow the user trying to reconnect to the server again.

4.	ButtonDisable: This method prevents errors from occurring by clicking the Connect button infinitely when connected to the server. If you are connected to the server, the Connect button changes to disable to prevent unexpected errors in advance. Also, since the connection is activated, the connection status light will turn green.

### Controller

1.	Connection: When the user presses the Connect button in the View parts, the Controller will receive the player name, and IP address, and make a connection to the server. If we connect to the server, Connected will be invoked and let the view knows the connection is successful. After that, we will disable the ConnectButton to prevent the weird case that users press the button while they are playing. Also, change the ServerStatus from Red to Green to notice that the Server is working fine. 

2.	Connection Problem: Whenever the state’s ErrorOccurred is true. It means that the network has some problem such as an invalid IP Address, disconnect, or the server is closed. In this case, we invoke the Error to let View knows there is some problem with the server. Whenever the Error is invoked, it will enable the connect button again to let the user re-connect and the ServerStatus color will change from Green to Red to notice. 

3.	Handling the Data: We created the ParsingData() method for parsing the data since we get data from the server as JSON type and we need to store data into World class (Model). When data is split and called in foreach loop, we decided to try to parse each data. If so, we do not need any other complexity for parsing data. When we tried to Parse the data and if it is not JSON type, we can tell there are 3 cases which are UniqueId, WorldSize, or wrong data format from the server. However, we can guarantee that the first data is the Unique ID for the user and the second data is world size. When we get world size, it means we are allowed to create the World. We invoked the WorldCreate and let view draws the world. After storing the all of data from the server, we invoke the DatasArrived to let view know. Whenever DatasArrived is invoked, View will draw Snakes, PowerUp, and Walls based on the World data. 

4.	InputKey: Whenever the user presses the key, the TextChanged event will fire in the View parts and send to the Controller what the user pressed. We should not send any command requests to the server before receiving the player ID, world size, and Walls. Therefore, we used the if statement to check if World have received the Snake or PowerUp (because they will only come after Wall). Also, while the network ErrorOccurred is true, if the user try to press the key, we will alert that the Connection is lost.

5.	Race Condition: We lock the state when we call the ProcessData() method. While we are parsing the data and drawing based on the Wolrd’s information, if we do not lock the state, there is a possibility that Snakes’ or PowerUps’ Dictionary (in the world) can be modified while WorldPanel class draws the snake or powerUps. Therefore, we lock the “state” for ProcessData() method and lock the “theWorld” in the WorldPanel class.
