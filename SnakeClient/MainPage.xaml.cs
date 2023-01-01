using GameSystem;
using GameWorld;

namespace SnakeGame;

public partial class MainPage : ContentPage
{
    // The object variable for getting gameController data
    private GameController gameController;

    public MainPage()
    {
        gameController = new GameController();

        InitializeComponent();

        // Update Frame Handler
        gameController.DatasArrived += OnFrame;
        // Initialize World Handler
        gameController.WorldCreate += DrawingWorld;
        // Display Error Message Handler
        gameController.Error += NetworkErrorHandler;
        // Update Connect Information Handler
        gameController.Connected += ButtonDisable;
    }

    /// <summary>
    /// Handler for initializing game world
    /// </summary>
    private void DrawingWorld()
    {
        worldPanel.SetWorld(gameController.World);
        worldPanel.SetUniqueID(gameController.getUniqueID());
        OnFrame();
    }

    /// <summary>
    /// Method to be invoked by tapping to focus on the input editbox.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    void OnTapped(object sender, EventArgs args)
    {
        keyboardHack.Focus();
    }

    /// <summary>
    /// Method to be invoked by typing input value into the editbox
    /// It sends input data to the server so that user can move the snake.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    void OnTextChanged(object sender, TextChangedEventArgs args)
    {
        Entry entry = (Entry)sender;

        String text = entry.Text.ToLower();
        if (text == "w")
        {
            gameController.InputKey("up");
        }
        else if (text == "a")
        {
            gameController.InputKey("left");
        }
        else if (text == "s")
        {
            gameController.InputKey("down");
        }
        else if (text == "d")
        {
            gameController.InputKey("right");
        }
        else
        {
            gameController.InputKey("none");
        }
        // Remove input data in the editbox.
        entry.Text = "";
    }

    /// <summary>
    /// Handler for the controller's Error event
    /// </summary>
    /// <param name="s"> Error invoked </param>
    private void NetworkErrorHandler(string s)
    {
        Dispatcher.Dispatch(() => DisplayAlert("Error", s + " Please try again. ", "OK"));
        // Make connect button to be enabled for restart
        Dispatcher.Dispatch(() => connectButton.IsEnabled = true);
        // Show Disconnection status
        Dispatcher.Dispatch(() => ServerStatus.Fill = Colors.Red);
    }


    /// <summary>
    /// Event handler for the connect button
    /// We will put the connection attempt logic here in the view, instead of the controller,
    /// because it is closely tied with disabling/enabling buttons, and showing dialogs.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void ConnectClick(object sender, EventArgs args)
    {
        if (serverText.Text == "")
        {
            DisplayAlert("Error", "Please enter a server address", "OK");
            return;
        }
        if (nameText.Text == "")
        {
            DisplayAlert("Error", "Please enter a name", "OK");
            return;
        }
        if (nameText.Text.Length > 16)
        {
            DisplayAlert("Error", "Name must be less than 16 characters", "OK");
            return;
        }
        gameController.Connect(serverText.Text, nameText.Text);

        keyboardHack.Focus();
    }

    /// <summary>
    /// Handler for showing connection status
    /// </summary>
    public void ButtonDisable()
    {
        // If connection is successfully worked, show green circle.
        Dispatcher.Dispatch(() => ServerStatus.Fill = Colors.Green );
        // and make connect button unable to prevent infinite connection.
        Dispatcher.Dispatch(() => connectButton.IsEnabled= false);
    }

    /// <summary>
    /// Use this method as an event handler for when the controller has updated the world
    /// </summary>
    public void OnFrame()
    {   
        // Actual Draw
        Dispatcher.Dispatch(() => graphicsView.Invalidate());
    }

    /// <summary>
    /// Control Help DisplayAlert method
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ControlsButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("Controls",
                     "W:\t\t Move up\n" +
                     "A:\t\t Move left\n" +
                     "S:\t\t Move down\n" +
                     "D:\t\t Move right\n",
                     "OK");
    }

    /// <summary>
    /// Information about game
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AboutButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("About",
      "SnakeGame solution\nArtwork by Jolie Uk and Alex Smith\nGame design by Daniel Kopta and Travis Martin\n" +
      "Implementation by ...\n" +
        "CS 3500 Fall 2022, University of Utah", "OK");
    }

    /// <summary>
    /// Make focus on to the input editbox.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ContentPage_Focused(object sender, FocusEventArgs e)
    {
        if (!connectButton.IsEnabled)
            keyboardHack.Focus();
    }
}