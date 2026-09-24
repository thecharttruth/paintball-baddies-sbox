using Sandbox;
namespace PaintballBaddies;
public sealed class CoverMuzzleReview : Component
{
    private PlayerController player;
    private CoverController cover;
    private PaintballMarker marker;
    private CoverSurface[] surfaces;
    private int index,phase;
    private float elapsed;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();cover=player.Components.Get<CoverController>();marker=player.Components.Get<PaintballMarker>();
        surfaces=Scene.GetAllComponents<CoverSurface>().Where(x=>x.GameObject.Name.StartsWith("Concrete cover ")).OrderBy(x=>x.GameObject.Name).ToArray();
        cover.Leave();cover.TestInput=true;player.UseLookControls=false;
    }
    protected override void OnUpdate()
    {
        if(index>=surfaces.Length)return;
        elapsed+=Time.Delta;if(elapsed<1)return;elapsed=0;
        var surface=surfaces[index];var direction=surface.WorldRotation.Forward;
        if(phase==0)
        {
            cover.Leave();player.WorldPosition=surface.WorldPosition-direction*52+Vector3.Up*2;
            player.EyeAngles=Rotation.LookAt(direction).Angles();player.Body.Velocity=Vector3.Zero;phase=1;
        }
        else if(phase==1){cover.TryEnter();cover.TestAim=true;marker.ReviewAimOverride=true;phase=2;}
        else
        {
            var hit=Scene.Trace.Sphere(.35f,player.EyePosition,marker.Muzzle).IgnoreGameObjectHierarchy(player.GameObject).WithoutTags("paintball_debris").Run();
            var forwardHit=Scene.Trace.Sphere(.35f,marker.Muzzle,marker.Muzzle+direction*80).IgnoreGameObjectHierarchy(player.GameObject).WithoutTags("paintball_debris").Run();
            Log.Info("COVER_MUZZLE "+Json.Serialize(new{name=surface.GameObject.Name,cover.Attached,cover.Peeking,top=surface.Top,muzzle=marker.Muzzle.z,clear=!hit.Hit,obstacle=hit.GameObject?.Name,forwardClear=!forwardHit.Hit,forwardObstacle=forwardHit.GameObject?.Name}));
            index++;phase=0;
        }
    }
    protected override void OnDestroy()
    {
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;}
        if(marker.IsValid())marker.ReviewAimOverride=null;
        if(player.IsValid())player.UseLookControls=true;
    }
}
