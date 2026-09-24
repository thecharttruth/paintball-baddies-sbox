using Sandbox;
namespace PaintballBaddies;
public sealed class SurfacePaintReview : Component
{
    float elapsed;bool done;
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;if(done||elapsed<2)return;done=true;
        var system=PaintImpactSystem.Find(Scene);
        int tested=0,hitCount=0;
        foreach(var r in Scene.GetAllComponents<ModelRenderer>().ToArray())
        {
            if(!r.Enabled||!r.GameObject.Active||!r.Model.IsValid()||r is SkinnedModelRenderer||r.GameObject.Tags.Has("paintball_debris")||r.GameObject.Name.Contains("marker",System.StringComparison.OrdinalIgnoreCase))continue;
            var bounds=r.Model.Bounds;var center=bounds.Center;bool found=false;
            foreach(var dir in new[]{Vector3.Up,Vector3.Down,Vector3.Forward,Vector3.Backward,Vector3.Left,Vector3.Right})
            {
                var extent=System.MathF.Abs(dir.x)*bounds.Size.x+System.MathF.Abs(dir.y)*bounds.Size.y+System.MathF.Abs(dir.z)*bounds.Size.z;
                var from=r.WorldTransform.PointToWorld(center+dir*(extent*.5f+12));
                var to=r.WorldTransform.PointToWorld(center-dir*extent*.5f);
                var trace=Scene.Trace.Sphere(.35f,from,to).WithSurfaceMeshes().WithoutTags("paintball_debris").Run();
                if(!trace.Hit||trace.GameObject!=r.GameObject)continue;
                system.Spawn(trace,Color.Magenta);found=true;hitCount++;break;
            }
            if(!found && r.GameObject.Name.Contains("roof",System.StringComparison.OrdinalIgnoreCase))
            {
                foreach(var x in new[]{-.4f,-.2f,.2f,.4f})
                foreach(var y in new[]{-.4f,-.2f,.2f,.4f})
                {
                    var roofPoint=r.WorldTransform.PointToWorld(center+new Vector3(bounds.Size.x*x,bounds.Size.y*y,0));
                    var t=Scene.Trace.Ray(roofPoint-Vector3.Up*30,roofPoint+Vector3.Up*30).WithSurfaceMeshes().WithoutTags("paintball_debris").Run();
                    if(t.Hit&&t.GameObject==r.GameObject){system.Spawn(t,Color.Magenta);found=true;}
                }
                if(found)hitCount++;
            }
            tested++;Log.Info($"SURFACE_PAINT {(found ? "HIT" : "UNVERIFIED")} {r.GameObject.Name}");
        }
        Log.Info($"SURFACE_PAINT COMPLETE hit={hitCount} tested={tested}");
        var glass=Scene.GetAllComponents<ModelRenderer>().First(x=>x.GameObject.Name=="Foundry skylight glazing");
        var point=glass.WorldTransform.PointToWorld(glass.Model.Bounds.Center);
        var glassHit=Scene.Trace.Ray(point-Vector3.Up*35,point+Vector3.Up*35).WithSurfaceMeshes().WithoutTags("paintball_debris").Run();
        system.Clear();system.Spawn(glassHit,Color.Magenta);
        var player=Scene.GetAllComponents<PlayerController>().First();player.UseCameraControls=false;
        Scene.Camera.WorldPosition=glassHit.HitPosition+glassHit.Normal*45;
        Scene.Camera.WorldRotation=Rotation.LookAt(-glassHit.Normal);Scene.Camera.FieldOfView=55;
        Log.Info($"GLASS_PAINT hit={glassHit.GameObject?.Name} at={glassHit.HitPosition}");
    }
}
