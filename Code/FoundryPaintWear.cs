using Sandbox;
using System;
using System.Collections.Generic;
namespace PaintballBaddies;

/// <summary>Persistent, deterministic dry paint from earlier venue matches.</summary>
public sealed class FoundryPaintWear : Component
{
    private GameObject wear;
    private static Texture drySurface;
    private int count;
    // Native package cleanup clears collections of disposable resources.
    // Arrays reject Clear(), so keep these texture caches in mutable lists.
    private static List<Texture> splashes, grime;
    private static Texture chips, chipNormal;
    private float coverDelay;
    private bool coverWearAdded;
    protected override void OnStart()
    {
        wear = new GameObject { NetworkMode=NetworkMode.Never, Name = "Paint residue from previous matches" };
        if ( drySurface is not { IsValid: true } )
        {
            using var bitmap = new Bitmap(1,1);
            bitmap.Clear(new Color(.9f,0,1,1));
            drySurface = bitmap.ToTexture();
        }
        PrepareWearTextures();
        var random = new Random(71429);
        float Between(float a,float b) => a + (float)random.NextDouble()*(b-a);
        // Repeated shots cluster at human height along the backstop walls.
        foreach(var side in new[]{-1f,1f})
        for(int cluster=0;cluster<8;cluster++)
        for(int mark=0;mark<9;mark++)
        {
            float x=-1020+cluster*290+Between(-48,48);
            float z=Between(22,96);
            TraceMark(new Vector3(x,side*770,z),new Vector3(x,side*840,z),Between(18,36),Between(0,360));
        }
        // End-wall firing backstops, below signage and skylights.
        foreach(var side in new[]{-1f,1f})
        for(int i=0;i<30;i++)
        {
            float y=Between(-680,680),z=Between(25,100);
            TraceMark(new Vector3(side*1120,y,z),new Vector3(side*1210,y,z),Between(18,38),Between(0,360));
        }
        for(int i=0;i<40;i++)
        {
            float x=Between(-1000,1000),y=Between(-650,650);
            TraceMark(new Vector3(x,y,8),new Vector3(x,y,-15),Between(15,30),Between(0,360));
        }
        // The enclosed annex carries wear from previous venue matches too.
        for(int i=0;i<50;i++)
        {
            float x=Between(-1060,1060),z=Between(20,100);
            TraceMark(new Vector3(x,1500,z),new Vector3(x,1550,z),Between(18,38),Between(0,360));
        }
        // Carry the venue's history along the annex flanks, not only its backstop.
        foreach(var side in new[]{-1f,1f})
        for(int cluster=0;cluster<6;cluster++)
        for(int mark=0;mark<5;mark++)
        {
            float y=880+cluster*112+Between(-32,32),z=Between(20,88);
            TraceMark(new Vector3(side*1120,y,z),new Vector3(side*1210,y,z),Between(12,30),Between(0,360));
        }
        for(int i=0;i<24;i++)
        {
            float x=Between(-1050,1050),y=Between(890,1480);
            TraceMark(new Vector3(x,y,8),new Vector3(x,y,-15),Between(10,24),Between(0,360));
        }
        AddAgeWear();
        Log.Info($"FOUNDRY_PAINT_WEAR marks={count} persistent=true");
    }
    protected override void OnUpdate()
    {
        if(coverWearAdded || (coverDelay+=Time.Delta)<1)return;
        coverWearAdded=true;
        var random=new Random(9317);
        var before=count;
        foreach(var cover in Scene.GetAllComponents<CoverSurface>().Where(x=>x.Components.Get<BoxCollider>().IsValid() || x.Components.Get<ModelCollider>()?.Model is { IsValid: true }).OrderBy(x=>x.WorldPosition.y>826 ? 0 : 1).ThenBy(x=>x.GameObject.Name).Take(40))
        {
            var box=cover.Components.Get<BoxCollider>();
            var model=cover.Components.Get<ModelCollider>()?.Model;
            var localCenter=box.IsValid() ? box.Center : model.Bounds.Center;
            var localSize=box.IsValid() ? box.Scale : model.Bounds.Size;
            var center=cover.WorldPosition+cover.WorldRotation*(localCenter*cover.WorldScale);
            var extent=localSize*cover.WorldScale;
            var radius=MathF.Max(extent.x,extent.y)*.75f+40;
            for(int i=0;i<4;i++)
            {
                var angle=i*90+(float)random.NextDouble()*30;
                var direction=Rotation.FromYaw(angle).Forward;
                var point=center.WithZ(cover.WorldPosition.z+MathF.Min(cover.Top-cover.WorldPosition.z-5,15+(float)random.NextDouble()*25));
                TraceMark(point+direction*radius,point,14+(float)random.NextDouble()*12,(float)random.NextDouble()*360,cover.GameObject);
                // Smaller overlapping impacts break up isolated, evenly sized marks.
                var offset=direction.Cross(Vector3.Up)*(8+(float)random.NextDouble()*12)+Vector3.Up*((float)random.NextDouble()*12-6);
                TraceMark(point+offset+direction*radius,point+offset,8+(float)random.NextDouble()*10,(float)random.NextDouble()*360,cover.GameObject);
                // Residue on the floor beside cover, never projected through the prop.
                var floorPoint=center+direction*(MathF.Max(extent.x,extent.y)*.55f+10);
                TraceMark(floorPoint.WithZ(7),floorPoint.WithZ(-8),10+(float)random.NextDouble()*12,(float)random.NextDouble()*360);
            }
        }
        Log.Info($"FOUNDRY_COVER_WEAR marks={count-before} total={count} persistent=true");
    }
    private void TraceMark(Vector3 from,Vector3 to,float size,float angle,GameObject expected=null)
    {
        var hit=Scene.Trace.Ray(from,to).Run();
        if(!hit.Hit || (expected is not null && hit.GameObject!=expected))return;
        var go=new GameObject(wear){Name="Dried venue paint"};
        go.WorldPosition=hit.HitPosition+hit.Normal*.2f;
        go.WorldRotation=Rotation.LookAt(-hit.Normal);
        var decal=go.Components.Create<Decal>();
        decal.Decals=new List<DecalDefinition>{new(){ColorTexture=splashes[count%splashes.Count],RoughMetalOcclusionTexture=drySurface,Width=1,Height=1,ColorMix=1}};
        var palette=new[]{new Color(.9f,.12f,.4f,.88f),new Color(.08f,.65f,.7f,.86f),new Color(.92f,.7f,.08f,.9f),new Color(.52f,.2f,.72f,.82f),new Color(.35f,.49f,.35f,.65f)};
        decal.ColorTint=palette[count++%palette.Length];
        // Cover uses conservative box colliders; recessed panels sit behind
        // that hull and need a deeper projection than flat wall geometry.
        decal.Size=new Vector2(size*2.3f,size*(count%3==0 ? 3.0f : 2.3f));decal.Depth=expected is null ? .7f : 8f;decal.Rotation=angle;
        decal.AttenuationAngle=.4f;decal.LifeTime=0;decal.Transient=false;
    }
    private static Texture LoadWear(string name,bool mask)
    {
        using var source=Bitmap.CreateFromBytes(FileSystem.Mounted.ReadAllBytes($"textures/venue_wear/{name}.png").ToArray());
        using var bitmap=source.Resize(512,512);
        if(mask)
        {
            var pixels=bitmap.GetPixels();
            for(int i=0;i<pixels.Length;i++)pixels[i]=Color.White.WithAlpha(pixels[i].a);
            bitmap.SetPixels(pixels);
        }
        return bitmap.ToTexture();
    }
    private static void PrepareWearTextures()
    {
        if(splashes is { Count: > 0 } && splashes[0].IsValid)return;
        splashes=new(){LoadWear("splash_1",true),LoadWear("splash_2",true),LoadWear("splash_3",true),LoadWear("splash_4",true)};
        grime=new(){LoadWear("grime_1",true),LoadWear("grime_2",true)};
        chips=LoadWear("chipped_plaster_color",false);chipNormal=LoadWear("chipped_plaster_normal",false);
    }
    private void AgeMark(Vector3 from,Vector3 to,Vector2 size,Color tint,int variant,float angle=0)
    {
        var hit=Scene.Trace.Ray(from,to).WithSurfaceMeshes().UsePhysicsWorld(!Game.IsEditor).Run();if(!hit.Hit)return;
        if(variant==2 && hit.GameObject.Name.Contains("column"))return;
        var go=new GameObject(wear){Name=variant==2 ? "Aged chipped masonry" : "Venue grime and oxidation"};
        go.WorldPosition=hit.HitPosition+hit.Normal*.15f;go.WorldRotation=Rotation.LookAt(-hit.Normal);
        var decal=go.Components.Create<Decal>();
        var definition=new DecalDefinition{ColorTexture=variant==2 ? chips : grime[variant%2],RoughMetalOcclusionTexture=drySurface,Width=1,Height=1,ColorMix=1};
        if(variant==2)definition.NormalTexture=chipNormal;
        decal.Decals=new List<DecalDefinition>{definition};decal.ColorTint=tint;
        decal.Size=size;decal.Depth=1;decal.Rotation=angle;decal.AttenuationAngle=.4f;decal.LifeTime=0;decal.Transient=false;
    }
    private void AddAgeWear()
    {
        var rng=new Random(82016);float R(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
        // Wide, low-contrast traffic stains, with denser residue near lane edges.
        for(int i=0;i<110;i++)
        {
            var point=new Vector3(R(-1110,1110),R(-760,1480),0);
            AgeMark(point.WithZ(6),point.WithZ(-12),new Vector2(R(180,360),R(90,200)),new Color(.12f,.105f,.085f,.68f),i%2,R(0,360));
        }
        foreach(float side in new[]{-1f,1f})
        for(int i=0;i<24;i++)
        {
            float x=R(-1100,1100),z=R(12,95);
            AgeMark(new Vector3(x,side*770,z),new Vector3(x,side*840,z),new Vector2(R(15,36),R(12,28)),new Color(.65f,.62f,.57f,.8f),2,R(0,360));
            AgeMark(new Vector3(x,side*770,18),new Vector3(x,side*840,18),new Vector2(R(90,160),45),new Color(.18f,.17f,.12f,.48f),i%2);
        }
        // Accumulated paint and boot smears in the practice firing lanes.
        for(int i=0;i<55;i++)
        {
            var point=new Vector3(R(-900,900),R(-660,650),0);
            TraceMark(point.WithZ(6),point.WithZ(-12),R(22,44),R(0,360));
        }
        // Oxidation is concentrated on actual metal columns and their feet.
        foreach(var renderer in Scene.GetAllComponents<ModelRenderer>().Where(x=>x.GameObject.Name.Contains("column")))
        {
            var center=renderer.WorldPosition;
            for(int side=0;side<4;side++)
            {
                var dir=Rotation.FromYaw(side*90).Forward;
                var point=center.WithZ(center.z+R(20,110));
                AgeMark(point+dir*55,point,new Vector2(30,70),new Color(.42f,.16f,.045f,.75f),side%2);
            }
        }
    }
    protected override void OnDestroy()=>wear?.Destroy();
}
