using Godot;
using System;

public class NetworkPlayer
{
    public NetworkPlayer()
    {
    }

    public NetworkPlayer(String networkPlayer)
    {
        net_id = Int32.Parse(networkPlayer.Split(";")[0]);
        name = networkPlayer.Split(";")[1];
        team = networkPlayer.Split(";")[2];
    }

    public String name { get; set; }
    public String team { get; set; }
    public int net_id = 1;

    public override String ToString()
    {
        return net_id + ";" + name + ";" + team;
    }
}
