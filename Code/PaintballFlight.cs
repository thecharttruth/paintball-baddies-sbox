using Sandbox;
namespace PaintballBaddies;
public static class PaintballFlight
{
    public const float Speed=3528; // 294 feet per second; 5% more travel speed.
    public const float Gravity=386.09f;
    // Unlimited paint still needs deliberate timing: at most about 3.6 shots/second.
    public const float ShotInterval=.28f;
    public const float Lifetime=3;
    public const float SightCompensationRange=3200; // ~81m, previously an abrupt cutoff at ~61m.
    // Slightly enlarged for readability; keep physical travel speed and gravity.
    public const float Radius=.41f;
    public const float VisibilityScale=1.35f;
    public const float VisualScale=Radius*2*VisibilityScale/50;
    public static SceneTraceResult Trace(Scene scene,Vector3 from,Vector3 to,GameObject shooter)
    {
        var world=scene.Trace.Sphere(Radius,from,to).UseHitPosition(true).UseHitboxes(false)
            .IgnoreGameObjectHierarchy(shooter).WithoutTags("paintball_debris").WithSurfaceMeshes();
        // Movement hulls do not follow arms/legs or crouched poses. Keep them
        // for navigation, and use native animated hitboxes for paintballs.
        foreach(var actor in scene.GetAllComponents<CharacterHitboxes>())
            if(actor.Count>0 && actor.Actor.IsValid())world=world.IgnoreGameObjectHierarchy(actor.Actor);
        // Networked shields stay separate roots so each peer can follow the
        // displayed hand. Ignoring the pawn hierarchy alone misses its shield.
        foreach(var shield in scene.GetAllComponents<ArenaShield>())
            if(shield.Owner.IsValid() && shield.Owner.GameObject==shooter)
                world=world.IgnoreGameObjectHierarchy(shield.GameObject);
        var physical=world.Run();
        var character=scene.Trace.Sphere(Radius,from,to).UseHitPosition(true).UsePhysicsWorld(false)
            .UseHitboxes(true).IgnoreGameObjectHierarchy(shooter).Run();
        return character.Hit && (!physical.Hit || character.Fraction<physical.Fraction) ? character : physical;
    }
    public static GameObject CreateBead(GameObject ball,Color tint)
    {
        // Both renderers are local presentation, including on a replicated ball.
        // Keep the trail root at unit scale; only the sphere is paintball-sized.
        var presentation=new GameObject(ball,true,"Paintball presentation"){NetworkMode=NetworkMode.Never};
        var bead=new GameObject(presentation,true,"Paintball bead"){NetworkMode=NetworkMode.Never};
        bead.Tags.Add("paintball_debris");
        bead.LocalScale=new Vector3(VisualScale);
        var renderer=bead.Components.Create<ModelRenderer>();
        renderer.Model=Model.Load("models/dev/sphere.vmdl");
        renderer.MaterialOverride=Material.Load("materials/dev/primary_white_emissive.vmat");
        renderer.RenderType=ModelRenderer.ShadowRenderType.Off;
        renderer.Tint=tint;
        AddTrail(presentation,tint);
        return bead;
    }
    public static void UpdateBead(GameObject bead)
    {
        if(!bead.IsValid())return;
        var camera=bead.Scene.Camera;
        if(!camera.IsValid())return;
        // Enlarge the bead at every distance, including its near/far limits.
        // Presentation remains independent of the swept collision radius.
        float diameter=((bead.WorldPosition-camera.WorldPosition).Length*.0025f).Clamp(Radius*2,5)*VisibilityScale;
        bead.WorldScale=new Vector3(diameter/50);
    }
    // Low ballistic arc to the sight point; collision still decides what is hit.
    public static Vector3 LaunchVelocity(Vector3 from,Vector3 to)
    {
        var offset=to-from;
        float distance=offset.WithZ(0).Length;
        if(distance<1)return offset.Normal*Speed;
        // Zero the marker through the longer arena lanes. Farther sight points
        // retain the same aim slope and compensation at the maximum zeroing
        // distance, rather than suddenly losing all compensation after 2400.
        if(distance>SightCompensationRange)
        {
            offset*=SightCompensationRange/distance;
            distance=SightCompensationRange;
        }
        var horizontal=offset.WithZ(0);
        float speedSquared=Speed*Speed;
        float discriminant=speedSquared*speedSquared-Gravity*(Gravity*distance*distance+2*offset.z*speedSquared);
        if(discriminant<0)return offset.Normal*Speed;
        // Rationalized low-angle solution avoids cancellation for near-level shots.
        float tangent=(Gravity*distance*distance+2*offset.z*speedSquared)/(distance*(speedSquared+System.MathF.Sqrt(discriminant)));
        float horizontalSpeed=Speed/System.MathF.Sqrt(1+tangent*tangent);
        return horizontal.Normal*horizontalSpeed+Vector3.Up*(horizontalSpeed*tangent);
    }
    public static void AddTrail(GameObject ball,Color tint)
    {
        var trail=ball.Components.Create<TrailRenderer>();
        trail.MaxPoints=8;trail.PointDistance=2;trail.LifeTime=.055f;
        trail.CastShadows=false;trail.Opaque=false;
        trail.Face=SceneLineObject.FaceMode.Camera;trail.BlendMode=BlendMode.Normal;
        trail.Color=Gradient.FromColors(tint.WithAlpha(.9f),tint.WithAlpha(0));
        var width=new Curve();width.AddPoint(0,1.2f);width.AddPoint(1,0);trail.Width=width;
    }
}
