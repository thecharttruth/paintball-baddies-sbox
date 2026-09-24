using Sandbox;
namespace PaintballBaddies;

/// <summary>Replicate decisions for presentation; only the host runs the AI.</summary>
public sealed class NetworkBot : Component
{
    [Property,Sync(SyncFlags.FromHost)] public int Seat { get; set; }
    [Sync(SyncFlags.FromHost)] public Vector3 Velocity { get; set; }
    [Sync(SyncFlags.FromHost)] public Vector3 Aim { get; set; }
    [Sync(SyncFlags.FromHost)] public bool Covered { get; set; }
    [Sync(SyncFlags.FromHost)] public bool Peek { get; set; }
    [Sync(SyncFlags.FromHost)] public bool Low { get; set; }
    [Sync(SyncFlags.FromHost)] public string ContactState { get; set; }="";
    private float update;
    protected override void OnUpdate()
    {
        var bot=Components.Get<ArenaOpponent>();if(!bot.IsValid())return;
        if(MultiplayerSession.Authority)
        {
            update-=Time.Delta;if(update>0)return;update=.05f;
            Velocity=bot.NavigationVelocity;Aim=bot.CombatAimPoint;
            Covered=bot.UsingCover;Peek=bot.CoverPeeking;Low=bot.CoverIsLow;
            ContactState=Components.Get<CloseCombat>()?.CaptureNetworkState() ?? "";
        }
        else {bot.ApplyNetwork(this);Components.Get<CloseCombat>()?.ApplyNetworkState(ContactState);}
    }
}
