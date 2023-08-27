using Godot;
using System;

using System.Net.NetworkInformation;

public partial class GameNetEngine: Node 
{
    public GameNetEngine StaticInstance;

    public override void _Ready() 
    {
        StaticInstance = this;
    }
}
