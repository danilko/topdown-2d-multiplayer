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

        GameConditionManager.GameResultMessage gameResultMessage = GameConditionManager.DecodeGameResult(messages);

        String title = "";
        if(gameResultMessage.WinningTeamCode == Team.TeamCode.TEAMUNKOWN)
        {
            title = "IT IS A TIE";
        }
        else
        {
            title = "WINNER: " + gameResultMessage.WinningTeamCode;
        }

        ((Label) GetNode("ResultPanel/lblResultTitle")).Text = title;
        ((Label) GetNode("ResultPanel/lblResultSummary")).Text = "" + gameResultMessage.EndGameCondition;
        ((Label) GetNode("ResultPanel/lblElapsedTime")).Text = "Elapsed Time: " + gameResultMessage.ElapsedTime;
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
