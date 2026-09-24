using Sandbox;
namespace PaintballBaddies;
public sealed class WallMaterialReview : Component
{
 [Property] public Vector3 ReviewPosition { get; set; } = new(-900,-600,70);
 [Property] public Vector3 ReviewDirection { get; set; } = new(200,-215,-30);
 private PlayerController player;
 protected override void OnUpdate()
 {
  player ??=Scene.GetAllComponents<PlayerController>().FirstOrDefault();
  if(player.IsValid())player.UseCameraControls=false;
  WorldPosition=ReviewPosition;WorldRotation=Rotation.LookAt(ReviewDirection);
 }
 protected override void OnDestroy(){if(player.IsValid())player.UseCameraControls=true;}
}
