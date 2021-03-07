using Godot;
using System;

public class SimulateGameWorld : GameWorld
{
    private Godot.Collections.Dictionary<String, int> _spawnBotTargetBase = new Godot.Collections.Dictionary<String, int>();

    public override void _Ready()
    {
        GameStates = (GameStates)GetNode("/root/GAMESTATES");

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

            _spawnBotTargetBase[agent.GetUnitName()] = (_spawnBotTargetBase[agent.GetUnitName()] + 1) % CapaturableBaseManager.GetBases().Count;
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
            Agent enemyNode = (Agent)TeamMapAIs[(int)agent.GetCurrentTeam()].GetUnit(agent.Name);

            CapturableBase capturableBase = (CapturableBase)(CapaturableBaseManager.GetBases()[_spawnBotTargetBase[agent.GetUnitName()]]);

            Vector2 randomPosition = capturableBase.GetRandomPositionWithinCaptureRadius();

            enemyNode.MoveToward(randomPosition - enemyNode.GlobalPosition, delta);

            enemyNode.LookAt(SpawnBots[AgentPrefix + ((index + 1) % SpawnBots.Count)].GlobalPosition);

            // Always fire
            enemyNode.Fire(Weapon.WeaponOrder.Right, 1);
            enemyNode.Fire(Weapon.WeaponOrder.Left, 1);
            index++;
        }
    }
}
