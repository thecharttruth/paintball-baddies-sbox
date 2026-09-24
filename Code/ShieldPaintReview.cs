using Sandbox;
namespace PaintballBaddies;
public sealed class ShieldPaintReview : Component
{
    PlayerController player;ArenaShield shield;GameObject shooter;float elapsed;int phase;
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;if(elapsed<1)return;elapsed=0;
        if(phase==0)
        {
            player=Scene.GetAllComponents<PlayerController>().First();player.UseInputControls=player.UseLookControls=false;player.WorldPosition=new(-650,-700,1);player.EyeAngles=new(0,0,0);
            player.Components.Get<PaintballMarker>().ReviewAimOverride=true;shield=Scene.GetAllComponents<ArenaShield>().First();shield.Collect(player.Components.Get<PaintballCombatant>());shooter=new GameObject(true,"Shield paint review shooter");phase=1;
        }
        else if(phase==1)
        {
            var target=shield.WorldPosition;var from=target+Vector3.Forward*150;var go=new GameObject(true,"Shield paint review ball");go.WorldPosition=from;
            var ball=go.Components.Create<OpponentPaintball>();ball.Shooter=shooter;ball.Team=9;ball.Velocity=PaintballFlight.LaunchVelocity(from,target);phase=2;
        }
        else if(phase==3 || phase==4){Log.Info("SHIELD_BOUNDS "+Model.Load("models/gear/clear_shield/panel.vmdl").Bounds.ToString());phase=7;var a=shield.WorldPosition+Vector3.Forward*100;var b=shield.WorldPosition-Vector3.Forward*100;var tr=Scene.Trace.Ray(a,b).WithSurfaceMeshes().WithoutTags("paintball_debris").Run();var ts=Scene.Trace.Sphere(.35f,a,b).WithSurfaceMeshes().WithoutTags("paintball_debris").Run();Log.Info($"SHIELD_TRACE ray={tr.Hit}/{tr.GameObject?.Name} sphere={ts.Hit}/{ts.GameObject?.Name} owner={shield.Owner?.GameObject.Name} shield={shield.WorldPosition} player={player.WorldPosition}");}
        else if(phase==5){foreach(var clip in new[]{"HoldItem_LH_Pose_Standing_01","HoldItem_LH_Pose_Crouching_01","HoldItem_LH_Pose_CrouchingLow_01"}){var go=new GameObject(true,clip);go.WorldPosition=new(0,0,10000);var b=go.Components.Create<SkinnedModelRenderer>();b.Model=Model.Load("models/characters/viper_citizen_neck_review/viper_graph.vmdl");b.UseAnimGraph=false;b.Sequence.Name=clip;b.PlaybackRate=0;}phase=6;}
        else if(phase==6){foreach(var b in Scene.GetAllComponents<SkinnedModelRenderer>().Where(x=>x.WorldPosition.z>9000)){b.TryGetBoneTransform("hand_L",out var h);Log.Info($"SHIELD_POSE {b.GameObject.Name} {h.Position-b.WorldPosition}");b.GameObject.Destroy();}phase=7;}
        else if(phase==2)
        {
            player.UseCameraControls=false;Scene.Camera.WorldPosition=shield.WorldPosition+new Vector3(120,-30,15);Scene.Camera.WorldRotation=Rotation.LookAt(shield.WorldPosition-Scene.Camera.WorldPosition);
            Log.Info("SHIELD_PAINT "+Json.Serialize(new{shield.Remaining,shield.PaintCount,shield.GripError, Bounds=Model.Load("models/gear/clear_shield/panel.vmdl").Bounds.Size.ToString(), Muzzle=player.Components.Get<PaintballMarker>().Muzzle.ToString()}));phase=3;
        }
    }
    protected override void OnDestroy(){shooter?.Destroy();if(player.IsValid())player.Components.Get<PaintballMarker>().ReviewAimOverride=null;if(player.IsValid())player.UseInputControls=player.UseLookControls=player.UseCameraControls=true;}
}
