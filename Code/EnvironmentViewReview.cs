using Sandbox;
namespace PaintballBaddies;
/// <summary>Temporary fixed camera for native environment inspection.</summary>
public sealed class EnvironmentViewReview : Component
{
 [Property] public Vector3 ViewPosition { get; set; } = new(850,-180,175);
 [Property] public Vector3 Target { get; set; } = new(1168,0,186);
 protected override void OnUpdate()
 {
  foreach(var p in Scene.GetAllComponents<PlayerController>()) p.UseCameraControls=false;
  WorldPosition=ViewPosition;WorldRotation=Rotation.LookAt(Target-ViewPosition);
 }
 protected override void OnDestroy()
 {
  foreach(var p in Scene.GetAllComponents<PlayerController>()) p.UseCameraControls=true;
 }
}
