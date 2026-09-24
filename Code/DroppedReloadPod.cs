using Sandbox;
namespace PaintballBaddies;
/// <summary>Brief physical presentation of a pod released on elimination.</summary>
public sealed class DroppedReloadPod : Component
{
    private float age,startHeight;
    public float FallDistance => startHeight-WorldPosition.z;
    public bool ActorFilterVerified
    {
        get
        {
            var center = WorldPosition + WorldRotation * new Vector3(0,0,4.5f);
            var offset = WorldRotation * new Vector3(5,0,0);
            var ordinary = Scene.Trace.Sphere(.35f,center-offset,center+offset).Run();
            var actor = Scene.Trace.Sphere(.35f,center-offset,center+offset).WithCollisionRules("paintball_actor").Run();
            return ordinary.GameObject == GameObject && actor.GameObject != GameObject;
        }
    }
    public bool ShotFilterVerified
    {
        get
        {
            var center = WorldPosition + WorldRotation * new Vector3(0,0,4.5f);
            var offset = WorldRotation * new Vector3(5,0,0);
            var ordinary = Scene.Trace.Sphere(.35f,center-offset,center+offset).Run();
            var filtered = Scene.Trace.Sphere(.35f,center-offset,center+offset).WithoutTags("paintball_debris").Run();
            return ordinary.GameObject == GameObject && filtered.GameObject != GameObject;
        }
    }
    protected override void OnStart(){startHeight=WorldPosition.z;}
    protected override void OnUpdate(){age+=Time.Delta;if(age>5)GameObject.Destroy();}
}
