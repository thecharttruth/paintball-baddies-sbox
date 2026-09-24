using Sandbox;
namespace PaintballBaddies;
public sealed class HelmetProfileReview:Component
{
 PlayerController player;
 protected override void OnUpdate(){player??=Scene.GetAllComponents<PlayerController>().FirstOrDefault();if(!player.IsValid())return;player.UseInputControls=player.UseLookControls=player.UseCameraControls=false;player.EyeAngles=new(0,0,0);Scene.Camera.WorldPosition=player.WorldPosition+new Vector3(4,-65,65);Scene.Camera.WorldRotation=Rotation.LookAt(player.WorldPosition+new Vector3(0,0,62)-Scene.Camera.WorldPosition);}
 protected override void OnDestroy(){if(player.IsValid())player.UseInputControls=player.UseLookControls=player.UseCameraControls=true;}
}
