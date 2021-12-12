using Godot;
using System;
using System.Collections.Generic;

using System.IO;
using System.IO.Compression;
using System.Text;

public class NetworkSnapshotManager : Node
{

    [Signal]
    public delegate void SnapshotReceivedSignal();

    [Signal]
    public delegate void NeworkRateUpdateSignal();

    // The "signature" (timestamp) added into each generated state snapshot
    // int max is 2,147,483,647 
    // 2,147,483,647 / 60 (snapshots) / 60 (seconds => 1 min) /60 (mins = > 1 hr) /24 (24 hrs => 1 day) = 414 days (before the snapshot expire)
    // So as long as this round of games end in 1 year, the snapshot signature will be unique
    private int _snapshotSignature = 1;

    // The signature of the last snapshot received
    private int _lastSnapshotSignature = 0;

    private float _currentNetworkBytes = 0;
    private float _currentNetworkSnapshots = 0;
    private float _currentAppliedNetworkSnapshots = 0;

    private GameWorld _gameWorld;
    private Network _network;

    public class ClientData
    {
        public String Id;
        public int Health;
        public Vector2 Position;
        public float Rotation;
        public int RightWeapon;
        public int LeftWeapon;
        public int RightWeaponIndex;
        public int LeftWeaponIndex;
    }

    public class Snapshot : Godot.Object
    {
        public int signature;
        public Dictionary<String, ClientData> playerData = new Dictionary<String, ClientData>();
        public Dictionary<String, ClientData> botData = new Dictionary<String, ClientData>();
    }

    public class PlayerInput
    {
        public enum InputAction {
            NOT_TRIGGER,
            TRIGGER,
            RELOAD
        }

        public int Up;

        public int Down;

        public int Left;

        public int Right;

        public Vector2 MousePosition;

        public int RightWeaponAction;
        public int LeftWeaponAction;
        public int RightWeaponIndex;
        public int LeftWeaponIndex;
    }

    // Holds player input data (including the local one) which will be used to update the game state
    //This will be filled only on the server
    public Dictionary<int, Dictionary<int, PlayerInput>> playerInputs = new Dictionary<int, Dictionary<int, PlayerInput>>();


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _network = (Network)GetNode("/root/NETWORK");
    }

    public int GetSnapshotSignature()
    {
        return _snapshotSignature;
    }

    public Network GetNetwork()
    {
        return _network;
    }

    public void Initialize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;

        // After receiving and fully decoding a new snapshot, apply it to the game world
        this.Connect(nameof(SnapshotReceivedSignal), this, nameof(applySnapshot));
    }

    public void CacheInput(int net_id, PlayerInput playerInput)
    {
        if (!GetTree().IsNetworkServer())
        {
            return;
        }

        if (! playerInputs.ContainsKey(net_id))
        {
            playerInputs.Add(net_id, new Dictionary<int, PlayerInput>());
        }

        playerInputs[net_id].Add(playerInputs[net_id].Count, playerInput);
    }

    // Cacluate network rate base on send bytes, received snapshots, applied snapshots
    private void _onNetworkRateTimerUpdate()
    {
        // Convert from bytes to Kb (kio bits)
        String message = (_currentNetworkBytes / (8 * 1000)) + " Kb/s, "
        + _currentNetworkSnapshots + " obtained snapshots/s, "
        + _currentAppliedNetworkSnapshots + " applied snapshots/s";

        EmitSignal(nameof(NeworkRateUpdateSignal), message);

        _currentNetworkBytes = 0;
        _currentNetworkSnapshots = 0;
        _currentAppliedNetworkSnapshots = 0;
    }


    // Based on the "High level" snapshot data, encodes into a byte array
    // ready to be sent across the network. This function does not return
    // the data, just broadcasts it to the connected players. To that end,
    // it is meant to be run only on the server
    public void EncodeSnapshot(Snapshot snapshot)
    {
        int clientAgentInfoSentCount = 0;
        int botAgentInfoSentCount = 0;

        if (!GetTree().IsNetworkServer())
        {
            return;
        }

        ClientData[] clientValues = new ClientData[snapshot.playerData.Count];
        snapshot.playerData.Values.CopyTo(clientValues, 0);

        ClientData[] botValues = new ClientData[snapshot.botData.Count];
        snapshot.botData.Values.CopyTo(botValues, 0);

        // Loop through until all data are sent to clients
        while (clientAgentInfoSentCount < snapshot.playerData.Count || botAgentInfoSentCount < snapshot.botData.Count)
        {
            // Max agent sent info before being truncated
            int remainAvailableSlots = 20;

            String encodedData = "";

            // First add the snapshot signature (timestamp)
            encodedData = encodedData + snapshot.signature + ";";

            // Player data count
            // Only need to process if there are remain agent data not sent yet
            if (clientAgentInfoSentCount < snapshot.playerData.Count)
            {
                int targetCount = 0;

                // Chunck data base on available slots
                // Basically check snapshots into smaller size package, which each package maximize at remainAvailableSlots

                // If already sent count + available slots is still less than total client agents
                // Then maximum can sent is remainavailableslots
                if (snapshot.playerData.Count > (remainAvailableSlots + clientAgentInfoSentCount))
                {
                    encodedData = encodedData + remainAvailableSlots + ";";
                    targetCount = remainAvailableSlots + clientAgentInfoSentCount;
                }
                // Otherwise is remain agent counts need to be sent
                else
                {
                    encodedData = encodedData + (snapshot.playerData.Count - clientAgentInfoSentCount) + ";";
                    targetCount = snapshot.playerData.Count;
                }

                for (int index = clientAgentInfoSentCount; index < targetCount; index++)
                {
                    // snapshot_data should contain a "players" field which must be an array
                    // of player data. Each entry in this array should be a dictionary, containing
                    // the following fields: network_id, position, rotation, col
                    encodedData = encodedData + encodeClientData(clientValues[index]);
                    remainAvailableSlots--;
                    clientAgentInfoSentCount++;
                }
            }
            else
            {
                // Inidcate no client data to process
                encodedData = encodedData + 0 + ";";
            }

            // Bot data count
            // Only need to process if there are remain agent data not sent yet and there are available slots to send data
            if (botAgentInfoSentCount < snapshot.botData.Count && remainAvailableSlots > 0)
            {
                int targetCount = 0;

                // If already sent count + available slots is still less than total client agents
                // Then maximum can sent is remainavailableslots
                if (snapshot.botData.Count > (remainAvailableSlots + botAgentInfoSentCount))
                {
                    encodedData = encodedData + remainAvailableSlots + ";";
                    targetCount = remainAvailableSlots + botAgentInfoSentCount;
                }
                // Otherwise is remain agent counts need to be sent
                else
                {
                    encodedData = encodedData + (snapshot.botData.Count - botAgentInfoSentCount) + ";";
                    targetCount = snapshot.botData.Count;
                }

                // The bot_data field should be an array, each entry containing the following
                // fields: bot_id, position, rotation
                for (int index = botAgentInfoSentCount; index < targetCount; index++)
                {
                    encodedData = encodedData + encodeClientData(botValues[index]);
                    remainAvailableSlots--;
                    botAgentInfoSentCount++;
                }
            }
            else
            {
                // Inidcate no client data to process
                encodedData = encodedData + 0 + ";";
            }

            _currentNetworkBytes += encodedData.Length * sizeof(Char);
            _currentNetworkSnapshots++;

            // First add the snapshot signature (timestamp)
            RpcUnreliable(nameof(clientGetSnapshot), compressString(encodedData));
        }

        // Make sure the next update will have the correct snapshot signature
        _snapshotSignature += 1;
    }


    // Compress tring into gzip compressed bytes
    // Referece https://gigi.nullneuron.net/gigilabs/compressing-strings-using-gzip-in-c/
    public byte[] compressString(String input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);

        using (var outputStream = new MemoryStream())
        {
            using (var gZipStream = new GZipStream(outputStream, CompressionMode.Compress))
                gZipStream.Write(inputBytes, 0, inputBytes.Length);

            return outputStream.ToArray();
        }
    }

    // Decompress gzip bytes into string
    // Referece https://gigi.nullneuron.net/gigilabs/compressing-strings-using-gzip-in-c/
    public String decompressString(byte[] input)
    {

        using (var inputStream = new MemoryStream(input))
        using (var gZipStream = new GZipStream(inputStream, CompressionMode.Decompress))
        using (var outputStream = new MemoryStream())
        {
            gZipStream.CopyTo(outputStream);

            var outputBytes = outputStream.ToArray();

            return Encoding.UTF8.GetString(outputBytes);
        }
    }


    [Remote]
    private void clientGetSnapshot(byte[] encodedDataCompress)
    {
        String encodedData = decompressString(encodedDataCompress);

        _currentNetworkBytes += encodedData.Length * sizeof(Char);
        _currentNetworkSnapshots++;

        int parseIndex = 0;

        // Extract the signature
        int signature = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        // If the received snapshot is older (or even equal) to the last received one, ignore the rest
        if (signature <= _lastSnapshotSignature)
        {
            return;
        }

        Snapshot snapshot = new Snapshot();
        snapshot.signature = signature;

        // Initialize the player data and bot data arrays

        // Extract player data count
        int clientCount = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        // Then the player data itself
        for (int index = 0; index < clientCount; index++)
        {
            ClientData clientData = new ClientData();

            parseIndex = _parseClientData(encodedData, clientData, parseIndex);

            snapshot.playerData.Add(clientData.Id, clientData);
        }

        // Extract bot data count
        clientCount = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        // Then the bot data
        for (int index = 0; index < clientCount; index++)
        {
            ClientData clientData = new ClientData();
            parseIndex = _parseClientData(encodedData, clientData, parseIndex);

            snapshot.botData.Add(clientData.Id, clientData);
        }

        //  Update the "last_snapshot"
        _lastSnapshotSignature = signature;

        // Update snapshots counter
        _currentAppliedNetworkSnapshots++;

        // Emit the signal indicating that there is a new snapshot do be applied
        EmitSignal(nameof(SnapshotReceivedSignal), snapshot);
    }

    private int _parseClientData(String encodedData, ClientData clientData, int parseIndex)
    {
        clientData.Id = encodedData.Split(";")[parseIndex];
        parseIndex++;
        clientData.Health = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.Position.x = float.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.Position.y = float.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.Rotation = float.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.RightWeapon = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.LeftWeapon = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.RightWeaponIndex = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.LeftWeaponIndex = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        return parseIndex;
    }

    private String encodeClientData(ClientData clientData)
    {
        String encodedData = "" + clientData.Id + ";";
        encodedData = encodedData + clientData.Health + ";";
        encodedData = encodedData + clientData.Position.x + ";";
        encodedData = encodedData + clientData.Position.y + ";";
        encodedData = encodedData + clientData.Rotation + ";";
        encodedData = encodedData + clientData.RightWeapon + ";";
        encodedData = encodedData + clientData.LeftWeapon + ";";
        encodedData = encodedData + clientData.RightWeaponIndex + ";";
        encodedData = encodedData + clientData.LeftWeaponIndex + ";";

        return encodedData;
    }

    private void applySnapshot(Snapshot snapshot)
    {
        // In here we assume the obtained snapshot is newer than the last one
        // Iterate through player data 
        foreach (ClientData item in snapshot.playerData.Values)
        {
            _gameWorld.GetGameStateManager().UpdateAgentStateFromSnapshot(AgentSpawnManager.AgentPlayerPrefix + item.Id, item);
        }

        foreach (ClientData item in snapshot.botData.Values)
        {
            // Only need to do on client, as logic already perform on server through calculation
            if (!GetTree().IsNetworkServer())
            {
                _gameWorld.GetGameStateManager().UpdateAgentStateFromSnapshot(item.Id, item);
            }
        }
    }


}
