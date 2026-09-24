using Sandbox;
using System.Linq;
namespace PaintballBaddies;
/// <summary>Opt-in fixed camera for repeatable environment lighting comparisons.</summary>
public sealed class ArenaLightingReview : Component
{
    [Property] public Vector3 ViewPosition { get; set; } = new(-900,1400,65);
    [Property] public Vector3 LookAt { get; set; } = new(800,1400,65);
    private PlayerController player;
    private bool cameraControl;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().FirstOrDefault();
        if(player.IsValid()){cameraControl=player.UseCameraControls;player.UseCameraControls=false;}
    }
    protected override void OnUpdate()
    {
        WorldPosition=ViewPosition;
        WorldRotation=Rotation.LookAt(LookAt-ViewPosition);
    }
    protected override void OnDestroy()
    {
        if(player.IsValid())player.UseCameraControls=cameraControl;
    }
}
