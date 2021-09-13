using Godot;
using System;

public class EndGameScreen : Control
{
    Network network;
    GameStates gameStates;
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        network = (Network)GetNode("/root/NETWORK");
        gameStates = (GameStates)GetNode("/root/GAMESTATES");
        
        String messages = gameStates.getMessgesForNextScene();
        // Set the message to empty as it is recieved
        gameStates.setMessagesForNextScene("");

        ((Label) GetNode("ResultPanel/lblResultTitle")).Text = messages.Split(";")[0];
        ((Label) GetNode("ResultPanel/lblResultSummary")).Text = messages.Split(";")[1];
        ((Label) GetNode("ResultPanel/lblElapsedTime")).Text = "Elapsed Time: " + messages.Split(";")[2];
    }

    private void btContinue()
    {
        if(GetTree().IsNetworkServer())
        {
            network.closeServer();
        }

        gameStates.EnterTitleScreen();
    }
}
