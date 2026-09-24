using Sandbox;
namespace PaintballBaddies;
public sealed class StandingCoverReview : Component
{
    PlayerController player; CoverController cover; PaintballMarker marker;
    float elapsed; int phase; Vector3 start;
    void Check(string label,bool pass)=>Log.Info($"STANDING_COVER {(pass ? "PASS" : "FAIL")} {label} position={player.WorldPosition} peek={cover.Peeking} corner={cover.CornerAvailable}");
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();cover=player.Components.Get<CoverController>();marker=player.Components.Get<PaintballMarker>();
        cover.Leave();cover.TestInput=true;player.UseLookControls=false;
        // Exercise the actual central service unit, not a substitute box.
        player.WorldPosition=new(-10,0,2);player.EyeAngles=new(0,0,0);player.Body.Velocity=Vector3.Zero;
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;if(elapsed<1.2f)return;elapsed=0;
        switch(phase++)
        {
            case 0: Check("attach",cover.TryEnter());break;
            case 1: Check("standing protected",cover.Attached&&!cover.LowCover&&!player.IsDucking);cover.TestAim=true;break;
            case 2: Check("middle stays protected",!cover.Peeking&&!cover.CornerAvailable);cover.Leave();cover.TestAim=false;player.WorldPosition=new(15,-24,2);player.Body.Velocity=Vector3.Zero;break;
            case 3: Check("edge entry",cover.TryEnter());break;
            case 4: Check("edge detected without movement",cover.CornerAvailable);start=player.WorldPosition;cover.TestAim=true;marker.ReviewAimOverride=true;break;
            case 5: Check("standing side exposure",cover.Peeking&&!player.IsDucking&&(player.WorldPosition-start).Length>38);Check("can fire",marker.FireAt(player.EyePosition+Vector3.Forward*400));cover.TestAim=false;marker.ReviewAimOverride=false;break;
            case 6: Check("release returns to cover",cover.Attached&&!cover.Peeking&&(player.WorldPosition-start).Length<2);Check("space releases",cover.Activate(false)&&!cover.Attached&&player.UseInputControls);break;
            case 7:
                var bunker=Scene.GetAllComponents<CoverSurface>().First(x=>x.Curved&&x.GameObject.Name.StartsWith("Bunker -"));
                player.WorldPosition=bunker.WorldPosition-Vector3.Forward*90+Vector3.Up*2;player.Body.Velocity=Vector3.Zero;player.EyeAngles=new(0,0,0);break;
            case 8: Check("rounded tall attach",cover.TryEnter()&&!cover.LowCover);break;
            case 9: Check("rounded peek available",cover.CornerAvailable);cover.TestAim=true;break;
            case 10: Check("rounded standing exposure",cover.Peeking&&!player.IsDucking);cover.TestAim=false;break;
            case 11: Check("rounded return",cover.Attached&&!cover.Peeking);Log.Info("STANDING_COVER COMPLETE");break;
        }
    }
    protected override void OnDestroy()
    {
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;cover.TestAim=false;}
        if(player.IsValid())player.UseLookControls=true;
        if(marker.IsValid())marker.ReviewAimOverride=null;
    }
}
