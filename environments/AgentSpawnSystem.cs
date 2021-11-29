using Godot;
using System;
using System.Collections.Generic;


public class AgentSpawnInfo
{
    public Team Team;
    public String Name;
    public int Delay;
}

public class AgentSpawnSystem : Node2D
{
    private Queue<AgentSpawnInfo> spawnQueue;
    
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        spawnQueue = new Queue<AgentSpawnInfo>();
    }

    public void Initialize()
    {

    }

    public void placeAgentOnWaitingQueue(String Agent)
    {

    }

    [Remote]
    private void _initializeNewPlayer(String info)
    {
        NetworkPlayer pininfo = new NetworkPlayer(info);

        if (GetTree().IsNetworkServer())
        {
            _spawnPlayer(pininfo.net_id + ";" + pininfo.team + ";" + _agentPlayerPrefix + _agentPlayerCounter + ";" + pininfo.name);
            _agentPlayerCounter++;
        }

        // Propagate info of obstacles to other
        if (GetTree().IsNetworkServer() && pininfo.net_id != 1)
        {

            foreach (TeamMapAI teamMapAI in TeamMapAIs)
            {
                teamMapAI.SyncTeamMapAICurrentUnitAmount(pininfo.net_id);
            }

            // Add current bot info to new player
            foreach (String playerIDs in spawnPlayers.Keys)
            {
                RpcId(pininfo.net_id, nameof(_spawnPlayer), playerIDs.Replace(_agentPlayerPrefix, "") + ";" + (int)spawnPlayers[playerIDs].GetTeam() + ";" + spawnPlayers[playerIDs].Name + ";" + spawnPlayers[playerIDs].GetDisplayName());
                // Sync inventory
                _inventoryManager.SyncInventory(pininfo.net_id, spawnPlayers[playerIDs]);
            }

            // Add current bot info to new player
            foreach (Agent spawnAgent in SpawnBots.Values)
            {
                RpcId(pininfo.net_id, nameof(_addBotOnNetwork), (int)spawnAgent.GetTeam() + ";" + spawnAgent.Name);
                // Sync inventory
                _inventoryManager.SyncInventory(pininfo.net_id, spawnAgent);
            }

            int index = 0;
            foreach (CapturableBase capturableBase in CapturableBaseManager.GetBases())
            {
                RpcId(pininfo.net_id, nameof(_syncCapturableBase), index + ";" + (int)capturableBase.GetCaptureBaseTeam());
                index++;
            }


            // Sync the destoryed obstacles
            _obstacleManager.syncObstacles(pininfo.net_id);

            // Sync the waiting period if it is already end
            if (!_waitingPeriod)
            {
                RpcId(pininfo.net_id, nameof(_getWaitingPeriodStatus), "00:00");
            }
        }
    }

    [Remote]
    private void _spawnPlayer(String unitInfo)
    {
        int netId = int.Parse(unitInfo.Split(";")[0]);
        int team = int.Parse(unitInfo.Split(";")[1]);
        String unitName = unitInfo.Split(";")[2];
        String displayName = unitInfo.Split(";")[3];

        if (GetTree().IsNetworkServer())
        {
            foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
            {
                // Spawn the new player within the currently iterated player as long it's not the server
                // Because the server's list already contains the new player, that one will also get itself!
                if (item.Key != 1)
                {
                    RpcId(item.Key, nameof(_spawnPlayer), unitInfo);
                }
            }
        }

        _cratePlayer(netId, (Team.TeamCode)team, unitName, displayName);
    }


    private void _cratePlayer(int netId, Team.TeamCode team, String unitName, String displayName)
    {
        String playerId = _agentPlayerPrefix + netId;

        // Already generated
        if (spawnPlayers.ContainsKey(playerId))
        {
            return;
        }

        // If observer is setup, clean it up
        if (HasNode(_agentObserverPrefix + observerCounter))
        {
            GetNode(_agentObserverPrefix + observerCounter).QueueFree();
            observerCounter++;
        }

        // Load the scene and create an instance
        Player agent = (Player)(TeamMapAIs[(int)team].CreateUnit(unitName, displayName, false));

        // If this actor does not belong to the server, change the network master accordingly
        if (netId != 1)
        {
            agent.SetNetworkMaster(netId);
        }

        // If this actor is the current client controlled, add camera and attach HUD
        if (netId == network.gamestateNetworkPlayer.net_id)
        {
            // Attach camera
            agent.SetHUD(_hud, _inventoryManager);
            agent.SetCameraRemotePath(_camera2D);

            // Set player marker
            _miniMap.SetPlayer(agent);
        }
        else
        {
            // Add as normal agent marker
            _miniMap.AddAgent(agent);
        }

        spawnPlayers.Add(playerId, agent);
        EmitSignal(nameof(PlayerCreateSignal));
    }


    [Remote]
    private void _removeUnitOnNetwork(String id)
    {
        if (GetTree().IsNetworkServer())
        {
            foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
            {
                // Skip server from replication code
                if (item.Key == 1)
                {
                    continue;
                }
                // Replicate despawn into currently iterated player

                RpcId(item.Key, nameof(_removeUnitOnNetwork), id);
            }
        }

        if (SpawnBots.ContainsKey(id))
        {
            _removeUnit(id, false);
        }
        else
        {
            _removeUnit(id, true);
        }
    }



    private void _syncBots()
    {
        if (GetTree().IsNetworkServer())
        {
            // Calculate the target amount of spawned bots
            int bot_count = network.serverinfo.max_players - network.networkPlayers.Count;

            if (SpawnBots.Count > bot_count)
            {
                while (SpawnBots.Count > bot_count)
                {
                    foreach (Agent spawnBot in SpawnBots.Values)
                    {
                        _removeUnitOnNetwork(spawnBot.Name);
                        break;
                    }
                }
            }
            else if (SpawnBots.Count < bot_count)
            {
                // If bot_count
                while (SpawnBots.Count < bot_count)
                {
                    TeamMapAI targetAI = null;
                    // Set the initial to max bot count
                    int smallestUnitCount = bot_count;

                    foreach (TeamMapAI currentAI in TeamMapAIs)
                    {
                        if (currentAI.GetAutoSpawnMember() && currentAI.isNewUnitAllow())
                        {
                            if (currentAI.GetUnitsContainer().GetChildren().Count <= smallestUnitCount)
                            {
                                smallestUnitCount = currentAI.GetUnitsContainer().GetChildren().Count;
                                targetAI = currentAI;
                            }
                        }
                    }

                    if (targetAI != null)
                    {
                        String botId = (int)targetAI.GetTeam() + ";" + AgentPrefix + AgentBotCounter;
                        AgentBotCounter++;

                        Rpc(nameof(_addBotOnNetwork), botId);

                        _addBotOnNetwork(botId);
                    }
                    else
                    {
                        // No longer allowed to add more bot now, so exit loop
                        break;
                    }
                }
            }

            _checkGameWinningCondition();
        }

    }

    [Remote]
    private void _addBotOnNetwork(String botId)
    {
        Team.TeamCode team = (Team.TeamCode)int.Parse(botId.Split(";")[0]);
        String unitName = botId.Split(";")[1];

        if (!SpawnBots.ContainsKey(unitName))
        {
            bool enableAI = false;

            if (GetTree().IsNetworkServer())
            {
                enableAI = true;
            }

            Agent agent = TeamMapAIs[(int)team].CreateUnit(unitName, unitName, enableAI);
            SpawnBots.Add(unitName, agent);

            // Add agent marker to minimap
            _miniMap.AddAgent(agent);
        }
    }
}
