using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in actual-cabinet test of both right-handed firing corners.</summary>
public sealed class HandedCoverReview : Component
{
    public List<string> Results {get;}=new();
    public bool Done {get;private set;}
    public int Stage {get;private set;}
    public float PauseAtStage {get;set;}=-1;
    PlayerController player;CoverController cover;PaintballMarker marker;
    Vector3 oldPosition,origin;Angles oldAngles;bool oldLook;float elapsed;
    int side=1,angle;
    void Check(bool ok,string label){Results.Add((ok ? "PASS " : "FAIL ")+label);Log.Info(Results.Last());}
    protected override void OnStart()
    {
        Scene.GetAllComponents<PaintballControls>().First().StartPractice();
        player=Scene.GetAllComponents<PlayerController>().First();cover=player.Components.Get<CoverController>();marker=player.Components.Get<PaintballMarker>();
        oldPosition=player.WorldPosition;oldAngles=player.EyeAngles;oldLook=player.UseLookControls;
        player.UseLookControls=false;cover.Leave();cover.TestInput=true;Place();
    }
    void Place(){cover.TestAim=false;marker.ReviewAimOverride=false;player.WorldPosition=new(15,side*24,2);player.EyeAngles=Angles.Zero;player.Body.Velocity=Vector3.Zero;elapsed=0;}
    protected override void OnUpdate()
    {
        if(Done)return;elapsed+=Time.Delta;
        if(elapsed<.9f)return;elapsed=0;
        if(Stage==0){Check(cover.TryEnter(),$"side {side} cabinet attachment");Stage=1;return;}
        if(Stage==1){Check(cover.CornerAvailable && !cover.LowCover,$"side {side} standing edge detected");origin=player.WorldPosition;cover.TestAim=true;marker.ReviewAimOverride=true;Stage=2;return;}
        if(Stage==2)
        {
            var eye=PaintballFlight.Trace(Scene,player.EyePosition,marker.Muzzle,player.GameObject);
            var lane=PaintballFlight.Trace(Scene,marker.Muzzle,marker.Muzzle+player.EyeAngles.ToRotation().Forward*120,player.GameObject);
            var camera=Scene.Camera;var sight=Scene.Trace.Ray(camera.WorldPosition,camera.WorldPosition+camera.WorldRotation.Forward*180).IgnoreGameObjectHierarchy(player.GameObject).WithoutTags("paintball_debris").Run();
            Check(cover.Peeking && !player.IsDucking,$"side {side} angle {angle} standing peek");
            Check(!eye.Hit && !lane.Hit,$"side {side} angle {angle} actual gun clears cabinet ({eye.GameObject?.Name}, {lane.GameObject?.Name})");
            if(eye.Hit || lane.Hit)Results.Add($"DIAGNOSTIC position={player.WorldPosition} muzzle={marker.Muzzle} shift={(player.WorldPosition-origin).Length} aim={marker.Aiming} alignment={Vector3.Dot(marker.PresentationAnchor.WorldRotation.Forward,player.EyeAngles.ToRotation().Forward)}");
            Check(!sight.Hit,$"side {side} angle {angle} camera sight clears cabinet ({sight.GameObject?.Name})");
            Check(marker.FireAt(marker.Muzzle+player.EyeAngles.ToRotation().Forward*400),$"side {side} angle {angle} firing accepted");
            if(angle<2){angle++;player.EyeAngles=new(0,angle==1 ? 12 : -12,0);return;}
            Stage=3;if(PauseAtStage==side){Scene.TimeScale=0;PauseAtStage=-1;}return;
        }
        if(Stage==3){cover.TestAim=false;marker.ReviewAimOverride=false;Stage=4;return;}
        Check(cover.Attached&&!cover.Peeking&&(player.WorldPosition-origin).Length<2,$"side {side} returns to original shelter");
        cover.Leave();Check(player.UseInputControls,$"side {side} leave restores input");
        if(side==1){side=-1;angle=0;Stage=0;Place();}else Done=true;
    }
    protected override void OnDestroy()
    {
        Scene.TimeScale=1;
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;cover.TestAim=false;}
        if(marker.IsValid())marker.ReviewAimOverride=null;
        if(player.IsValid()){player.WorldPosition=oldPosition;player.EyeAngles=oldAngles;player.UseLookControls=oldLook;player.Body.Velocity=Vector3.Zero;}
    }
}
