using Sandbox;
using System.Linq;
namespace PaintballBaddies;

/// <summary>Opt-in native decal checks and close camera preview.</summary>
public sealed class PaintImpactChecks : Component
{
    [Property] public bool RepeatPreview { get; set; }
    private float nextPreview;
    [Property] public bool CheckLifetime { get; set; }
    [Property] public bool FloorView { get; set; }
    private bool ran, dropletsExpired;
    private bool fadeChecked,expiryChecked;
    private bool reuseSurvived;
    private Decal lifetimeMark;
    private float elapsed;
    private void Check(string name,bool pass,string detail) => Log.Info($"PAINT_DECAL {(pass ? "PASS" : "FAIL")} {name}: {detail}");
    protected override void OnUpdate()
    {
        var player=Scene.GetAllComponents<PlayerController>().First();
        player.UseCameraControls=player.UseInputControls=player.UseLookControls=false;
        var camera=Components.Get<CameraComponent>();
        camera.WorldPosition=FloorView ? new Vector3(-463,-28,35) : new Vector3(-485,-52,78);
        camera.WorldRotation=Rotation.LookAt((FloorView ? new Vector3(-440,-5,0) : new Vector3(-420,0,47))-camera.WorldPosition);
        camera.FieldOfView=55;
        elapsed+=Time.Delta;
        if(ran && RepeatPreview && elapsed>nextPreview)
        {
            nextPreview=elapsed+.4f;
            var previewHit=Scene.Trace.Ray(new Vector3(-450,0,55),new Vector3(-410,0,55)).Run();
            PaintImpactSystem.Find(Scene).Spawn(previewHit,new Color(1,.12f,.6f));
        }
        if(ran && !RepeatPreview && elapsed>1.6f && !dropletsExpired)
        {
            var active=PaintImpactSystem.Find(Scene);
            Check("droplets expire",active.ActiveDroplets==0,$"active={active.ActiveDroplets}");
            dropletsExpired=true;
        }
        if(ran && CheckLifetime)
        {
            var active=PaintImpactSystem.Find(Scene);
            if(elapsed>66 && !fadeChecked)
            {
                var mark=lifetimeMark;
                float alpha=mark?.ColorTint.ConstantValue.a ?? -1;
                Log.Info($"PAINT_LIFETIME {(alpha>0 && alpha<1 ? "PASS" : "FAIL")} fades before expiry; alpha={alpha}");
                fadeChecked=true;
                var floorHit=Scene.Trace.Ray(new Vector3(-440,-5,15),new Vector3(-440,-5,-3)).Run();
                while(active.ActiveCount<PaintImpactSystem.Capacity)active.Spawn(floorHit,Color.Cyan);
                active.Spawn(floorHit,Color.Cyan);
                Log.Info($"PAINT_LIFETIME {(mark.IsValid() && mark.ColorTint.ConstantValue.a>.99f && mark.GameObject.Parent==floorHit.GameObject ? "PASS" : "FAIL")} recycling restores full opacity on new surface");
            }
            if(elapsed>78 && !reuseSurvived)
            {
                Log.Info($"PAINT_LIFETIME {(lifetimeMark.IsValid() && lifetimeMark.ColorTint.ConstantValue.a>.99f && active.ActiveCount==91 ? "PASS" : "FAIL")} reused mark survives original expiry; queued={active.ActiveCount}");
                reuseSurvived=true;
            }
            if(elapsed>144 && !expiryChecked)
            {
                int objects=Scene.GetAllComponents<Decal>().Count(d=>d.GameObject.Name=="Paint splash decal");
                Log.Info($"PAINT_LIFETIME {(active.ActiveCount==0 && objects==0 ? "PASS" : "FAIL")} removes expired decals; queued={active.ActiveCount}, objects={objects}");
                Log.Info("PAINT_LIFETIME COMPLETE");expiryChecked=true;
            }
        }
        if(ran || elapsed<1)return;
        ran=true;
        var system=PaintImpactSystem.Find(Scene);
        var hit=Scene.Trace.Ray(new Vector3(-450,0,55),new Vector3(-410,0,55)).Run();
        Check("hits native target",hit.Hit && hit.GameObject.Components.Get<PaintballTarget>() is not null,$"hit={hit.Hit}");
        system.Clear();system.Spawn(hit,new Color(1,.12f,.6f));
        Check("impact emits six droplets",system.ActiveDroplets==6,$"active={system.ActiveDroplets}");
        Check("spawns one decal",system.ActiveCount==1,$"active={system.ActiveCount}");
        var decal=Scene.GetAllComponents<Decal>().First(d=>d.GameObject.Name=="Paint splash decal");
        var texture=decal.Decals[0].ColorTexture;
        Check("shared mask texture valid",texture.IsValid && !texture.IsError && texture.Width==512,$"size={texture.Size}");
        Check("mask background transparent",texture.GetPixel(0,0).a<5,$"alpha={texture.GetPixel(0,0).a}");
        Check("mask centre opaque",texture.GetPixel(256,256).a>240,$"alpha={texture.GetPixel(256,256).a}");
        for(int i=0;i<PaintImpactSystem.Capacity-1;i++)system.Spawn(hit,Color.Cyan);
        var recycledFloor=Scene.Trace.Ray(new Vector3(-440,-5,15),new Vector3(-440,-5,-3)).Run();
        system.Spawn(recycledFloor,Color.Cyan);
        Check("oldest native decal reused",decal.IsValid() && Scene.GetAllComponents<Decal>().Count(d=>d.GameObject.Name=="Paint splash decal")==PaintImpactSystem.Capacity,"original component remains alive at capacity");
        Check("recycled decal moves to new surface",recycledFloor.Hit && decal.IsValid() && decal.GameObject.Parent==recycledFloor.GameObject
            && (decal.WorldPosition-(recycledFloor.HitPosition+recycledFloor.Normal*.35f)).Length<.01f,"target decal reparented to floor");
        for(int i=0;i<14;i++)system.Spawn(hit,Color.Cyan);
        Check("scene cap enforced",system.ActiveCount==PaintImpactSystem.Capacity,$"active={system.ActiveCount}");
        Check("droplet pool bounded",system.ActiveDroplets==PaintImpactSystem.DropletCapacity,$"active={system.ActiveDroplets}");
        system.Clear();Check("reset removes droplets",system.ActiveDroplets==0,$"active={system.ActiveDroplets}");
        Check("reset removes marks",system.ActiveCount==0,$"active={system.ActiveCount}");
        for(int i=0;i<5;i++)
        {
            var point=new Vector3(-420,(i-2)*5,48+(i%2)*13);
            var sample=Scene.Trace.Ray(point+Vector3.Backward*30,point+Vector3.Forward*10).Run();
            system.Spawn(sample,i%2==0 ? new Color(1,.12f,.6f) : new Color(.05f,.9f,.85f));
            // Scene enumeration may still include objects queued for destruction
            // by Clear this frame. Observe the actual live queue instead.
            if(i==0)lifetimeMark=system.OldestDecal;
        }
        var floor=Scene.Trace.Ray(new Vector3(-440,-5,15),new Vector3(-440,-5,-3)).Run();
        system.Spawn(floor,new Color(1,.12f,.6f));
        Check("wall and floor preview marks",system.ActiveCount==6,$"active={system.ActiveCount}");
        Log.Info("PAINT_DECAL COMPLETE");
    }
}
