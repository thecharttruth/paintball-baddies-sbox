using Sandbox;
namespace PaintballBaddies;
public sealed class RaisedMarkerReview : Component
{
    PlayerController player; CoverController cover; GameObject obstacle; float time; int phase;
    protected override void OnUpdate()
    {
        time+=Time.Delta;if(time<1)return;
        if(phase==0)
        {
            player=Scene.GetAllComponents<PlayerController>().First();cover=player.Components.Get<CoverController>();
            player.UseInputControls=player.UseLookControls=false;
            obstacle=TrainingRange.Box(null,"Raised marker review cover",new(-250,-700,27.5f),new(35,140,55),Color.Gray,true);
            obstacle.Components.Create<CoverSurface>();
            player.WorldPosition=new(-310,-700,1);player.EyeAngles=new(0,0,0);player.Body.Velocity=Vector3.Zero;
            cover.TestInput=true;phase=1;time=0;
        }
        else if(phase==1){cover.TryEnter();cover.TestAim=true;phase=2;time=0;}
        else if(phase==2&&time>3)
        {
            var weapon=player.Components.Get<PaintballMarker>();var pose=player.Components.Get<CitizenPlayerPresentation>();
            var trace=Scene.Trace.Sphere(.35f,weapon.Muzzle,weapon.Muzzle+Vector3.Forward*80).IgnoreGameObjectHierarchy(player.GameObject).WithoutTags("paintball_debris").WithSurfaceMeshes().Run();
            Log.Info("RAISED_MARKER "+Json.Serialize(new{cover.Attached,cover.Peeking,pose.CoverGunLift,Muzzle=weapon.Muzzle.ToString(),Clear=!trace.Hit,Obstacle=trace.GameObject?.Name}));
            PaintImpactSystem.Find(Scene).Clear();weapon.FireAt(new Vector3(1168,-700,60));phase=3;time=0;
        }
        else if(phase==3&&time>1)
        {
            var mark=PaintImpactSystem.Find(Scene).OldestDecal;
            Log.Info("RAISED_SHOT receiver="+mark?.GameObject.Parent?.Name);
            cover.TestAim=false;phase=4;time=0;
        }
        else if(phase==4&&time>1)
        {
            Log.Info("RAISED_RETURN "+Json.Serialize(new{cover.Attached,cover.Peeking,player.IsDucking,Lift=player.Components.Get<CitizenPlayerPresentation>().CoverGunLift}));phase=5;
        }
    }
    protected override void OnDestroy(){obstacle?.Destroy();if(cover.IsValid()){cover.Leave();cover.TestInput=false;}if(player.IsValid())player.UseInputControls=player.UseLookControls=true;}
}
