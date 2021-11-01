using Godot;
using System;
using System.Collections.Generic;

public class SimulateGameWorld : GameWorld
{
    private Dictionary<String, int> _spawnBotTargetBase = new Dictionary<String, int>();

    public override void _Ready()
    {

        GameStates = (GameStates)GetNode("/root/GAMESTATES");
        // Clean up previous setup state
        GameStates.SetTeamMapAISettings(null);
        
        InitializeInventoryManager();

        InitializeCamera();
        InitializeTileMap();
        InitializeCapaturableBaseManager();
        InitializeTeamMapAI();

        _syncBots();
    }


    private void _syncBots()
    {
        // Calculate the target amount of spawned bots
        String botId = AgentPrefix + AgentBotCounter;
        SpawnBots.Add(botId, GetTeamMapAIs()[(int)Team.TeamCode.TEAMALPHA].CreateUnit(AgentPrefix + AgentBotCounter, AgentPrefix + AgentBotCounter, false));
        _spawnBotTargetBase.Add(botId, (int)AgentBotCounter);
        AgentBotCounter++;

        botId = AgentPrefix + AgentBotCounter;
        SpawnBots.Add(botId, GetTeamMapAIs()[(int)Team.TeamCode.TEAMBETA].CreateUnit(AgentPrefix + AgentBotCounter, AgentPrefix + AgentBotCounter, false));
        _spawnBotTargetBase.Add(botId, (int)AgentBotCounter);
        AgentBotCounter++;
    }

    private void _changeAgentBehavior()
    {
        foreach (Agent agent in SpawnBots.Values)
        {
            agent.Heal(100);
            // Assign next bases

            _spawnBotTargetBase[agent.GetUnitName()] = (_spawnBotTargetBase[agent.GetUnitName()] + 1) % CapturableBaseManager.GetBases().Count;
        }
    }

    public override void _PhysicsProcess(float delta)
    {
        // Update the timeout counter
        CurrentTime += delta;
        if (CurrentTime < GameStates.updateDelta)
        {
            return;
        }

        // "Reset" the time counting
        CurrentTime -= GameStates.updateDelta;

        int index = 0;
        // And update the game state
        foreach (Agent agent in SpawnBots.Values)
        {
            // Locate the bot node
            Agent enemyNode = (Agent)TeamMapAIs[(int)agent.GetTeam()].GetUnit(agent.Name);

            CapturableBase capturableBase = CapturableBaseManager.GetBases()[_spawnBotTargetBase[agent.GetUnitName()]];

            Vector2 randomPosition = capturableBase.GetRandomPositionWithinCaptureRadius();

            enemyNode.MoveToward(randomPosition - enemyNode.GlobalPosition, delta);
            enemyNode.MoveAndSlide((SpawnBots[AgentPrefix + ((index + 1) % SpawnBots.Count)].GlobalPosition - enemyNode.GlobalPosition) * 0.1f);
            enemyNode.LookAt(SpawnBots[AgentPrefix + ((index + 1) % SpawnBots.Count)].GlobalPosition);

            // Always fire
            enemyNode.Fire(Weapon.WeaponOrder.Right, 1);
            enemyNode.Fire(Weapon.WeaponOrder.Left, 1);
            index++;
        }
    }
}
