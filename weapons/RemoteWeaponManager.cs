using Godot;
using System;
using System.Collections.Generic;

public class RemoteWeaponManager : Node
{
    // Declare member variables here. Examples:
    // private int a = 2;
    // private string b = "text";

    private RandomNumberGenerator _rand;

    private Vector2 _nextTargetPosition;

    private float MaxSpeed = 100f;

    protected Vector2 Velocity;

    private float _withinRange = 20.0f;

    private Node _container;

    private Dictionary<String, String> _agentNodeDictionary = new Dictionary<String, String>();
    private Dictionary<String, Dictionary<String, String>> _weaponNodeDictionary = new Dictionary<String, Dictionary<String, String>>();

    GameWorld _gameWorld;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _container = (Node)GetNode("Containers");

        _rand = new RandomNumberGenerator();
    }

    public void Initialize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;
    }

    public void Fire(Agent agent)
    {
        if (_agentNodeDictionary.ContainsKey(agent.GetUnitID()))
        {
            foreach (RemoteWeapon remoteWeapon in _container.GetNode(_agentNodeDictionary[agent.GetUnitID()]).GetChildren())
            {
                remoteWeapon.Fire(null, Vector2.Zero);
            }
        }
    }

    public int GetRemoteWeaponUnitsCount(Agent agent)
    {

        if (_agentNodeDictionary.ContainsKey(agent.GetUnitID()))
        {
            return _container.GetNode(_agentNodeDictionary[agent.GetUnitID()]).GetChildren().Count;
        }
        else
        {
            return 0;
        }
    }

    public void Reload(Agent agent)
    {

        if (_agentNodeDictionary.ContainsKey(agent.GetUnitID()))
        {
            foreach (RemoteWeapon remoteWeapon in _container.GetNode(_agentNodeDictionary[agent.GetUnitID()]).GetChildren())
            {
                remoteWeapon.StartReload();
            }
        }
    }

    [Remote]
    private void _deleteRemoteWeapon(String info)
    {
        String[] splitInfo = info.Split(";");

        String agentUnitID = splitInfo[0];
        String weaponNodeName = splitInfo[1];

        if (_agentNodeDictionary.ContainsKey(agentUnitID) && _weaponNodeDictionary[agentUnitID].ContainsKey(weaponNodeName))
        {
            ((RemoteWeapon)_container.GetNode(_agentNodeDictionary[agentUnitID] + "/" + _weaponNodeDictionary[agentUnitID][weaponNodeName])).Explode();

            _weaponNodeDictionary[agentUnitID].Remove(weaponNodeName);
        }
    }

    public void AddRemoteWeaponForAgent(Agent agent, PackedScene weaponScene)
    {
        // Add node if not exit
        if (!_agentNodeDictionary.ContainsKey(agent.GetUnitID()))
        {
            Node agentNode = new Node();
            _container.AddChild(agentNode);

            _agentNodeDictionary.Add(agent.GetUnitID(), agentNode.Name);

            Dictionary<String, String> weaponNodeDictionary = new Dictionary<string, string>();

            _weaponNodeDictionary.Add(agent.GetUnitID(), weaponNodeDictionary);
        }

        RemoteWeapon remoteWeapon = (RemoteWeapon)(weaponScene).Instance();

        _container.GetNode(_agentNodeDictionary[agent.GetUnitID()]).AddChild(remoteWeapon);

        remoteWeapon.Initialize(_gameWorld, agent, this);

        _weaponNodeDictionary[agent.GetUnitID()].Add(remoteWeapon.Name, remoteWeapon.Name);

        if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
        {
            String message = agent.GetUnitID() + ";"
                        + remoteWeapon.Name + ";" + weaponScene.ResourcePath + ";"
                         + remoteWeapon.GlobalPosition.x + ";" + remoteWeapon.GlobalPosition.y + ";"
                         + remoteWeapon.GlobalRotation + ";" + remoteWeapon.GetHealth() + ";";

            Rpc(nameof(_clientAddRemoteWeaponForAgent), message);
        }

    }

    [Remote]
    private void _clientAddRemoteWeaponForAgent(String info)
    {
        if (!GetTree().IsNetworkServer())
        {
            String[] splitInfo = info.Split(";");
            int infoIndex = 0;

            String agentUnitID = splitInfo[infoIndex];
            infoIndex++;

            Agent agent = null;

            if (agentUnitID.Contains(AgentSpawnManager.AgentPlayerPrefix) && _gameWorld.GetAgentSpawnManager().GetSpawnPlayers().ContainsKey(agentUnitID))
            {
                agent = _gameWorld.GetAgentSpawnManager().GetSpawnPlayers()[agentUnitID];
            }
            else if (agentUnitID.Contains(AgentSpawnManager.AgentBotPrefix) && _gameWorld.GetAgentSpawnManager().GetSpawnBots().ContainsKey(agentUnitID))
            {
                agent = _gameWorld.GetAgentSpawnManager().GetSpawnBots()[agentUnitID];
            }
            // Agent not found, so no need to sync
            else
            {
                return;
            }

            // Add node if not exit
            if (!_agentNodeDictionary.ContainsKey(agentUnitID))
            {
                Node agentNode = new Node();
                _container.AddChild(agentNode);

                _agentNodeDictionary.Add(agentUnitID, agentNode.Name);

                Dictionary<String, String> weaponNodeDictionary = new Dictionary<string, string>();

                _weaponNodeDictionary.Add(agent.GetUnitID(), weaponNodeDictionary);
            }

            String remoteWeaponNodeName = splitInfo[infoIndex];
            infoIndex++;

            String remoteWeaponSceneName = splitInfo[infoIndex];
            infoIndex++;

            RemoteWeapon remoteWeapon = (RemoteWeapon)((PackedScene)GD.Load(remoteWeaponSceneName)).Instance();

            _container.GetNode(_agentNodeDictionary[agentUnitID]).AddChild(remoteWeapon);

            _weaponNodeDictionary[agentUnitID].Add(remoteWeaponNodeName, remoteWeapon.Name);

            remoteWeapon.Initialize(_gameWorld, agent, this);

            remoteWeapon.GlobalPosition = new Vector2(float.Parse(splitInfo[infoIndex]), float.Parse(splitInfo[infoIndex + 1]));
            infoIndex = infoIndex + 2;

            remoteWeapon.GlobalRotation = float.Parse(splitInfo[infoIndex]);
            infoIndex++;

            remoteWeapon.SetHealth(int.Parse(splitInfo[infoIndex]));
            infoIndex++;

        }
    }

    public void SyncState()
    {
        String message = "" + _agentNodeDictionary.Keys.Count + ";";

        List<String> removedWeapons = new List<String>();
        foreach (String agentUnitID in _agentNodeDictionary.Keys)
        {
            message = message + agentUnitID + ";";

            int agentRemoteWeaponCounter = 0;

            String agentRemoteWeapon = "";
            foreach (RemoteWeapon remoteWeapon in _container.GetNode(_agentNodeDictionary[agentUnitID]).GetChildren())
            {
                // Only sync remote weapon that still alive
                if (remoteWeapon.GetHealth() > 0)
                {
                    agentRemoteWeapon = agentRemoteWeapon + remoteWeapon.Name + ";"
                             + remoteWeapon.GlobalPosition.x + ";" + remoteWeapon.GlobalPosition.y + ";"
                             + remoteWeapon.GlobalRotation + ";" + remoteWeapon.GetHealth() + ";";
                    agentRemoteWeaponCounter++;
                }
                else
                {
                    removedWeapons.Add(agentUnitID + ";" + remoteWeapon.Name);
                }
            }
            message = message + agentRemoteWeaponCounter + ";" + agentRemoteWeapon;
        }

        // Delete remote weapon
        foreach (String removedWeapon in removedWeapons)
        {
            // Delete for server itself
            _deleteRemoteWeapon(removedWeapon);

            if (GetTree().NetworkPeer != null)
            {
                Rpc(nameof(_deleteRemoteWeapon), removedWeapon);
            }
        }

        if (GetTree().NetworkPeer != null)
        {
            // Sync remain weapon
            RpcUnreliable(nameof(_clientSyncState), message);
        }
    }

    [Remote]
    private void _clientSyncState(String info)
    {
        if (!(GetTree().NetworkPeer == null) && !GetTree().IsNetworkServer())
        {

            String[] splitInfo = info.Split(";");
            int infoIndex = 0;

            int totalAgentCount = int.Parse(splitInfo[infoIndex]);
            infoIndex++;

            for (int agentIndex = 0; agentIndex < totalAgentCount; agentIndex++)
            {
                String agentUnitID = splitInfo[infoIndex];
                infoIndex++;

                int totalRemoteWeaponUnitCount = int.Parse(splitInfo[infoIndex]);
                infoIndex++;

                for (int weaponIndex = 0; weaponIndex < totalRemoteWeaponUnitCount; weaponIndex++)
                {
                    String weaponNodeName = splitInfo[infoIndex];
                    infoIndex++;

                    //GD.Print("SYNC WEAPON NAME: " + weaponNodeName);
                    // Only update if node exit, should be catch up eventually
                    if (_weaponNodeDictionary[agentUnitID].ContainsKey(weaponNodeName))
                    {

                        //  GD.Print("SYNC WEAPON NAME INTERNAL: " + weaponNodeName);
                        RemoteWeapon remoteWeapon = (RemoteWeapon)_container.GetNode(_agentNodeDictionary[agentUnitID] + "/" + _weaponNodeDictionary[agentUnitID][weaponNodeName]);
                        remoteWeapon.GlobalPosition = new Vector2(float.Parse(splitInfo[infoIndex]), float.Parse(splitInfo[infoIndex + 1]));
                        infoIndex = infoIndex + 2;

                        remoteWeapon.GlobalRotation = float.Parse(splitInfo[infoIndex]);
                        infoIndex++;

                        remoteWeapon.SetHealth(int.Parse(splitInfo[infoIndex]));
                        infoIndex++;
                    }
                }
            }
        }
    }

    public void DeleteRemoteWeaponForAgent(Agent agent)
    {
        // Add node if not exit
        if (_agentNodeDictionary.ContainsKey(agent.GetUnitID()))
        {
            //Deactivate
            foreach (RemoteWeapon remoteWeapon in _container.GetNode(_agentNodeDictionary[agent.GetUnitID()]).GetChildren())
            {
                remoteWeapon.Explode();
            }

            _weaponNodeDictionary[agent.GetUnitID()].Clear();
            _weaponNodeDictionary.Remove(agent.GetUnitID());

            _container.GetNode(_agentNodeDictionary[agent.GetUnitID()]).QueueFree();
            _agentNodeDictionary.Remove(agent.GetUnitID());
        }
    }

}
