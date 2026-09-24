using Sandbox;
namespace PaintballBaddies;
/// <summary>Measures unattended default-health exposure; does not assess human skill or fun.</summary>
public sealed class ExposureTimingChecks : Component
{
    [Property] public bool ReachCover {get;set;}
    private bool attached;
    private ArenaMatch match;
    private PlayerController player;
    private PaintballMarker marker;
    private float elapsed,roundTime,firstHit=-1;
    private int trial;
    private bool started;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        match ??=player.Components.Get<ArenaMatch>();marker ??=player.Components.Get<PaintballMarker>();
        if(match is null || marker is null || elapsed<1)return;
        if(!started)
        {
            match.StartRound();player.UseInputControls=false;player.UseLookControls=false;marker.AcceptInput=false;
            firstHit=-1;roundTime=0;started=true;attached=false;player.EyeAngles=new Angles(0,0,0);
        }
        roundTime+=Time.Delta;
        if(ReachCover && match.InRound)
        {
            var cover=player.Components.Get<CoverController>();cover.TestInput=true;
            if(!attached){player.WishVelocity=Vector3.Forward*180;attached=cover.TryEnter();}
            if(attached)player.WishVelocity=Vector3.Zero;
        }
        if(firstHit<0 && match.PlayerState.PaintHits>0)firstHit=roundTime;
        if(match.InRound && roundTime<20)return;
        Log.Info($"EXPOSURE_SAMPLE trial={trial+1} coverMode={ReachCover} attached={attached} health={match.PlayerState.HitLimit} firstHit={firstHit} outcome={match.Status} elapsed={roundTime} hits={match.PlayerState.PaintHits}");
        trial++;started=false;
        if(trial>=(ReachCover?1:3)){Log.Info("EXPOSURE_COMPLETE");Destroy();}
    }
}
