using Sandbox;
namespace PaintballBaddies;

/// <summary>Temporary result-screen momentum and landing check.</summary>
public sealed class RoundStopChecks : Component
{
    [Property] public bool FromCover { get; set; }
    private bool entered;
    private PlayerController player;
    private ArenaMatch match;
    private float elapsed,endedAt;
    private bool started,observed,done;
    private Vector3 resultPosition;
    protected override void OnUpdate()
    {
        if(done)return;
        elapsed+=Time.Delta;
        if(!started)
        {
            if(elapsed<1)return;
            player=Scene.GetAllComponents<PlayerController>().First();
            match=player.Components.Get<ArenaMatch>();
            match.RoundDuration=FromCover ? 1.5f : .25f;match.StartRound();
            player.UseInputControls=false;
            if(FromCover){player.WorldPosition=new Vector3(665,-375,2);player.EyeAngles=new Angles(0,0,0);}
            else player.WorldPosition+=Vector3.Up*90;
            started=true;return;
        }
        if(match.InRound)
        {
            if(FromCover)
            {
                if(!entered && elapsed>1.5f)
                {
                    entered=true;
                    var cover=player.Components.Get<CoverController>();
                    var accepted=cover.TryEnter();
                    Log.Info($"ROUND_STOP {(accepted && cover.Attached ? "PASS":"FAIL")} entered placed tank cover");
                }
                return;
            }
            player.WishVelocity=new Vector3(160,80,0);
            player.Body.Velocity=new Vector3(160,80,-100);
            return;
        }
        if(!observed)
        {
            observed=true;endedAt=elapsed;resultPosition=player.WorldPosition;
            var horizontal=player.Body.Velocity.WithZ(0).Length;
            Log.Info($"ROUND_STOP {(player.WishVelocity.Length<.01f && horizontal<.1f ? "PASS":"FAIL")} clears movement wish={player.WishVelocity} velocity={player.Body.Velocity}");
        }
        if(elapsed-endedAt<1)return;
        done=true;
        if(FromCover)
        {
            var cover=player.Components.Get<CoverController>();
            Log.Info($"ROUND_STOP {(!cover.Attached && !cover.Peeking && !cover.CanEnter && !player.IsDucking && !player.UseInputControls ? "PASS":"FAIL")} result clears cover attachment peek prompt and crouch");
        }
        var drift=(player.WorldPosition-resultPosition).WithZ(0).Length;
        Log.Info($"ROUND_STOP {(drift<2 && !player.IsAirborne ? "PASS":"FAIL")} stable landing drift={drift} airborne={player.IsAirborne} position={player.WorldPosition}");
    }
}
