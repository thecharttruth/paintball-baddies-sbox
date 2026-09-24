using Sandbox;
namespace PaintballBaddies;

/// <summary>Temporary native checks of real arena geometry, from both local X faces.</summary>
public sealed class LowCoverClearanceReview : Component
{
    public List<string> Results {get;}=new();
    public bool Done {get;private set;}
    public int Index {get;private set;}
    public int Count=>surfaces?.Length??0;
    CoverSurface[] surfaces;PlayerController player;CoverController cover;PaintballMarker marker;
    Vector3 oldPosition,direction;Angles oldAngles;bool oldLook;float elapsed;int phase,side;
    System.IDisposable hook;
    protected override void OnStart()
    {
        Scene.GetAllComponents<PaintballControls>().First().StartPractice();
        player=MultiplayerSession.LocalPlayer(Scene);cover=player.Components.Get<CoverController>();marker=player.Components.Get<PaintballMarker>();
        oldPosition=player.WorldPosition;oldAngles=player.EyeAngles;oldLook=player.UseLookControls;
        player.UseLookControls=false;cover.TestInput=true;
        hook=Scene.AddHook(GameObjectSystem.Stage.StartUpdate,-45,()=>{Input.AnalogMove=Vector3.Zero;Input.AnalogLook=Angles.Zero;Input.SetAction("attack1",false);Input.SetAction("attack2",false);},nameof(LowCoverClearanceReview),"Temporary cover geometry check");
        surfaces=Scene.GetAllComponents<CoverSurface>().Where(x=>!x.StandingOnly&&x.Top-x.WorldPosition.z>=30&&x.Top-x.WorldPosition.z<67
            &&(x.Components.Get<BoxCollider>().IsValid()||x.Components.Get<ModelCollider>()?.Model is {IsValid:true})).OrderBy(x=>x.GameObject.Name).ToArray();
        if(Count==0){Done=true;return;}Place();
    }
    void Place()
    {
        cover.Leave();cover.TestAim=false;marker.ReviewAimOverride=false;
        var surface=surfaces[Index];var box=surface.Components.Get<BoxCollider>();
        var bounds=surface.Components.Get<ModelCollider>()?.Model.Bounds??default;
        var localCenter=box.IsValid()?box.Center:bounds.Center;
        var localSize=box.IsValid()?box.Scale:bounds.Size;
        direction=surface.WorldRotation.Forward*(side==0?1:-1);
        var center=surface.WorldTransform.PointToWorld(localCenter);
        player.WorldPosition=(center-direction*(localSize.x*surface.WorldScale.x*.5f+42)).WithZ(surface.WorldPosition.z+2);
        player.EyeAngles=Rotation.LookAt(direction).Angles();player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;
        elapsed=0;phase=0;
    }
    void Check(bool ok,string text)=>Results.Add((ok?"PASS ":"FAIL ")+surfaces[Index].GameObject.Name+" face "+side+" "+text);
    protected override void OnUpdate()
    {
        if(Done)return;elapsed+=Time.Delta;
        var surface=surfaces[Index];
        if(phase==0&&elapsed>.4f)
        {
            bool attached=cover.TryEnter()&&cover.ReservedSurface==surface&&cover.LowCover;
            Check(attached,"attaches as low cover");
            if(!attached)
            {
                var probe=Scene.Trace.Ray(player.WorldPosition+Vector3.Up*28,player.WorldPosition+Vector3.Up*28+direction*70).IgnoreGameObjectHierarchy(player.GameObject).Run();
                Results.Add($"INFO {surface.GameObject.Name} root={player.WorldPosition} airborne={player.IsAirborne} probe={probe.GameObject?.Name} normal={probe.Normal} distance={probe.Distance} selected={cover.ReservedSurface?.GameObject.Name}");
                Advance();return;
            }phase=1;elapsed=0;
        }
        else if(phase==1&&elapsed>.55f)
        {
            var sheltered=Scene.Trace.Ray(player.EyePosition,player.EyePosition+direction*180).IgnoreGameObjectHierarchy(player.GameObject).Run();
            Check(player.IsDucking&&sheltered.GameObject==surface.GameObject,"crouched eye protected");
            cover.TestAim=true;marker.ReviewAimOverride=true;phase=2;elapsed=0;
        }
        else if(phase==2&&elapsed>1)
        {
            // Other distant scenery is irrelevant; reject this cover blocking either barrel segment.
            var eye=PaintballFlight.Trace(Scene,player.EyePosition,marker.Muzzle,player.GameObject);
            var lane=PaintballFlight.Trace(Scene,marker.Muzzle,marker.Muzzle+direction*160,player.GameObject);
            Check(cover.Peeking&&!player.IsDucking&&eye.GameObject!=surface.GameObject&&lane.GameObject!=surface.GameObject,
                $"standing shot clears cover (top {surface.Top:F1}, muzzle {marker.Muzzle.z:F1}, lane {lane.GameObject?.Name})");
            Check(marker.FireAt(marker.Muzzle+direction*500),"standing shot accepted");Advance();
        }
    }
    void Advance(){side++;if(side==2){Index++;side=0;}if(Index>=Count){Done=true;cover.Leave();return;}Place();}
    protected override void OnDestroy()
    {
        hook?.Dispose();Input.ReleaseActions();
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;cover.TestAim=false;}
        if(marker.IsValid())marker.ReviewAimOverride=null;
        if(player.IsValid()){player.WorldPosition=oldPosition;player.EyeAngles=oldAngles;player.UseLookControls=oldLook;player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;}
    }
}
