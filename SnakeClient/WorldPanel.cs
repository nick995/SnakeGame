/**
 * This class is for View of MVC.
 * It draws every object of the world based on the World data arrived from the Controller.
 * It also draws each object based on the delegate method.
 * 
 */

using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using IImage = Microsoft.Maui.Graphics.IImage;
#if MACCATALYST
using Microsoft.Maui.Graphics.Platform;
#else
using Microsoft.Maui.Graphics.Win2D;
#endif
using Color = Microsoft.Maui.Graphics.Color;
using System.Reflection;
using Microsoft.Maui;
using System.Net;
using Font = Microsoft.Maui.Graphics.Font;
using SizeF = Microsoft.Maui.Graphics.SizeF;
using GameWorld;
using Microsoft.UI.Xaml.Controls;
using System.Xml;
using Microsoft.Maui.Graphics;

namespace SnakeGame;
public class WorldPanel : IDrawable
{
    // Delegate for DrawObjectWithTransform which is for drawing object
    // Methods matching this delegate can draw whatever they want onto the canvas  
    public delegate void ObjectDrawer(object o, ICanvas canvas);

    private GraphicsView graphicsView = new();

    // The actual screen for the user
    private int viewSize = 900;
    // Initialize world and user information
    private World theWorld;
    private int playerUniqueID = -1;

    private IImage wall;
    private IImage background;

    private bool initializedForDrawing = false;
    // Snake died checker
    private bool isSnakeDied = false;

#if MACCATALYST
    private IImage loadImage(string name)
    {
        Assembly assembly = GetType().GetTypeInfo().Assembly;
        string path = "SnakeGame.Resources.Images";
        return PlatformImage.FromStream(assembly.GetManifestResourceStream($"{path}.{name}"));
    }
#else
    private IImage loadImage(string name)
    {
        Assembly assembly = GetType().GetTypeInfo().Assembly;
        string path = "SnakeGame.Resources.Images";
        var service = new W2DImageLoadingService();
        return service.FromStream(assembly.GetManifestResourceStream($"{path}.{name}"));
    }
#endif

    public WorldPanel()
    {
        graphicsView.Drawable = this;
        // Set the view screen to fit proper size
        graphicsView.HeightRequest = 900;
        graphicsView.WidthRequest = 900;
    }

    /// <summary>
    /// This method performs a translation to draw an object.
    /// </summary>
    /// <param name="canvas"> The canvas object for drawing onto </param>
    /// <param name="worldX">The X component of the object's position in world space</param>
    /// <param name="worldY">The Y component of the object's position in world space</param>
    /// <param name="drawer">The drawer delegate. After the transformation is applied, the delegate is invoked to draw whatever it wants</param>
    private void DrawObjectWithTransform(ICanvas canvas, object o, double worldX, double worldY, ObjectDrawer drawer)
    {
        // "push" the current transform
        canvas.SaveState();

        // Move canvas to the object location to make (0,0) coordinate
        canvas.Translate((float)worldX, (float)worldY);
        drawer(o, canvas);

        // "pop" the transform
        canvas.RestoreState();
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate
    /// This draws Snake object.
    /// </summary>
    /// <param name="o"> The player to draw </param>
    /// <param name="canvas"></param>
    private void SnakeDrawer(object o, ICanvas canvas)
    {
        Snake s = o as Snake;

        // If snake alives, draw snakes
        if (!isSnakeDied)
        {
            // Length of the snake
            int lengthOfSnake = s.Body.Count;

            // Set the color based on the unique id of the player.
            switch (s.UniqueID % 8)
            {
                case 0:
                    canvas.StrokeColor = Colors.DarkRed;
                    break;
                case 1:
                    canvas.StrokeColor = Colors.DarkGreen;
                    break;
                case 2:
                    canvas.StrokeColor = Colors.Yellow;
                    break;
                case 3:
                    canvas.StrokeColor = Colors.Black;
                    break;
                case 4:
                    canvas.StrokeColor = Colors.DarkOrange;
                    break;
                case 5:
                    canvas.StrokeColor = Colors.Blue;
                    break;
                case 6:
                    canvas.StrokeColor = Colors.Brown;
                    break;
                case 7:
                    canvas.StrokeColor = Colors.AliceBlue;
                    break;
            }

            // Snake size and shape of the snake
            canvas.StrokeSize = 10;
            canvas.StrokeLineCap = LineCap.Round;

            // Coordinate information to draw snake which is based on DrawLine
            float firstX = 0;
            float firstY = 0;
            float secondX = 0;
            float secondY = 0;
            // The coordinate where snake is located
            Vector2D temp = s.Body.Last();

            canvas.FontColor = Colors.White;

            // Draw whole snake body
            for (int i = lengthOfSnake - 1; i > 0; i--)
            {
                // If X-coordinate is same, we don't need to calculate X-coordinate for DrawLine.
                // Only need to calculate Y-coordinate of the snake body.
                if (temp.X == s.Body[i - 1].X)
                    secondY += (float)(s.Body[i - 1].Y - temp.Y);
                // and Y-coordinate also same as above
                else if (temp.Y == s.Body[i - 1].Y)
                    secondX += (float)(s.Body[i - 1].X - temp.X);

                // Draw two body of the snake based on the coordinate of the object location, not based on the world coordinate.
                canvas.DrawLine(firstX, firstY, secondX, secondY);

                // To draw next body
                firstX = secondX;
                firstY = secondY;
                temp = s.Body[i - 1];
            }
            // Draw Player Name and Score.
            canvas.DrawString(s.Name + ": " + s.Score, 0, 20, HorizontalAlignment.Center);
        }
        else // If snake dies, draw explosion
        {
            // MAKE FIREWORK RANDOMLY, LOOKING LIKE EXPLOSION
            Random rand = new Random();
            int explosionValueX = rand.Next(20);
            int explosionValueY = rand.Next(20);
            float randRotate = rand.Next(360);


            for (int i = 0; i < 3; i++)
            {
                canvas.FillColor = Colors.WhiteSmoke;
                canvas.FillCircle((float)(0 + explosionValueX), (float)(0 + explosionValueY), 4);
                canvas.Rotate((float)randRotate);
                canvas.FillColor = Colors.Red;
                canvas.FillCircle((float)(0 + explosionValueX), (float)(0 + explosionValueY), 4);
                canvas.Rotate((float)randRotate);
            }
        }
        // The eyes of the snake
        canvas.Rotate((float)s.Dir.ToAngle());
        canvas.FillColor = Colors.White;
        canvas.FillCircle(5, 1, 3);
        canvas.FillCircle(-5, 1, 3);
        canvas.FillColor = Colors.Black;
        canvas.FillCircle(5, 0, 2);
        canvas.FillCircle(-5, 0, 2);
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate
    /// This draws Wall object.
    /// </summary>
    /// <param name="o"> The wall to draw </param>
    /// <param name="canvas"></param>
    private void WallsDrawer(object o, ICanvas canvas)
    {
        Wall w = o as Wall;

        // The variable of the start point and end point of the wall
        double x1 = w.Point1.GetX();
        double y1 = w.Point1.GetY();
        double x2 = w.Point2.GetX();
        double y2 = w.Point2.GetY();

        // Set the size of the wall
        float width = 50;
        float height = 50;

        // Calculate how many walls in the unique key of the Dictionary.
        // Add start point and end point of the walls, and devided by width.
        double lengthOfWallInRow = x1 - x2;
        double lengthOfWallInCol = y1 - y2;
        int numOfRow = (int)(lengthOfWallInRow / width);
        int numOfCol = (int)(lengthOfWallInCol / width);

        // Absolutization of the number of the wall
        if (numOfRow < 0)
            numOfRow *= -1;
        if (numOfCol < 0)
            numOfCol *= -1;

        // Draw walls from right to left
        if (lengthOfWallInRow > 0 && lengthOfWallInCol == 0)
        {
            for (int i = 0; i <= numOfRow; i++)
            {
                canvas.DrawImage(wall, (-width / 2) - (i * width), -height / 2, width, height);
            }
        }
        // Draw walls from right to left
        else if (lengthOfWallInRow < 0 && lengthOfWallInCol == 0)
        {
            for (int i = 0; i <= numOfRow; i++)
            {
                canvas.DrawImage(wall, (-width / 2) + (i * width), -height / 2, width, height);
            }
        }
        // Draw walls from up to down
        else if (lengthOfWallInCol > 0 && lengthOfWallInRow == 0)
        {
            for (int i = 0; i <= numOfCol; i++)
            {
                canvas.DrawImage(wall, (-width / 2), (-height / 2) - (i * height), width, height);
            }
        }
        // Draw walls from down to up
        else if (lengthOfWallInCol < 0 && lengthOfWallInRow == 0)
        {
            for (int i = 0; i <= numOfCol; i++)
            {
                canvas.DrawImage(wall, (-width / 2), (-height / 2) + (i * height), width, height);
            }
        }
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate
    /// This draws PowerUp object.
    /// </summary>
    /// <param name="o"></param>
    /// <param name="canvas"></param>
    private void PowerUpDrawer(object o, ICanvas canvas)
    {
        PowerUp p = o as PowerUp;
        // The size of the PowerUp object
        float radius1 = 10f;
        float radius2 = 5f;

        canvas.FillColor = Colors.DarkGreen;
        canvas.FillCircle(0, 0, radius1);

        canvas.FillColor = Colors.Yellow;
        canvas.FillCircle(0, 0, radius2);
    }

    /// <summary>
    /// Setter of the game world.
    /// Method is invoked by the View, and it is set by Controller.
    /// </summary>
    /// <param name="w"> World obejct to draw </param>
    public void SetWorld(World w)
    {
        theWorld = w;
    }

    /// <summary>
    /// Setter of the UniqueID of the player.
    /// Method is invoked by the View, and it is set by Controller.
    /// </summary>
    /// <param name="w"> Player's unique id </param>
    public void SetUniqueID(int id)
    {
        playerUniqueID = id;
    }

    private void InitializeDrawing()
    {
        wall = loadImage("WallSprite.png");
        background = loadImage("Background.png");
        initializedForDrawing = true;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // We have to wait until Draw is called at least once 
        // before loading the images
        if (!initializedForDrawing)
            InitializeDrawing();

        // undo any leftover transformations from last frame
        canvas.ResetState();

        // Nothing draw until the world is set
        if (theWorld is not null)
        {
            // Nothing draw until player connects to the server and make own character in the world
            if (theWorld.SnakePlayers.ContainsKey(playerUniqueID))
            {
                // Center the view on the playable snake of the world
                float playerX = (float)theWorld.SnakePlayers[playerUniqueID].Body.Last().GetX();
                float playerY = (float)theWorld.SnakePlayers[playerUniqueID].Body.Last().GetY();

                canvas.Translate(-playerX + (viewSize / 2), -playerY + (viewSize / 2));

                // Draw background of the world
                canvas.DrawImage(background, (-theWorld.Size) / 2, (-theWorld.Size) / 2, theWorld.Size, theWorld.Size);

                lock (theWorld)
                {
                    //Drawing walls
                    foreach (Wall w in theWorld.Walls.Values.ToList())
                        DrawObjectWithTransform(canvas, w, w.Point1.GetX(), w.Point1.GetY(), WallsDrawer);

                    // If snake died, set the bool variable true to draw explosion, else set false to draw snake.
                    foreach (Snake s in theWorld.SnakePlayers.Values.ToList())
                    {
                        if (s.Died) { isSnakeDied = true; }

                        if (!s.Alive) { isSnakeDied = true; }
                        else { isSnakeDied = false; }

                        DrawObjectWithTransform(canvas, s, s.Body.Last().GetX(), s.Body.Last().GetY(), SnakeDrawer);
                    }
                    // Drawing Powerup
                    foreach (PowerUp p in theWorld.PowerUps.Values.ToList())
                        if(!p.Died)
                            DrawObjectWithTransform(canvas, p, p.Location.GetX(), p.Location.GetY(), PowerUpDrawer);
                }
            }
        }
    }
}