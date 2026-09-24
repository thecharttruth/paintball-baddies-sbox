using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
namespace PaintballBaddies;

/// <summary>Shared bounded paint decals; the generated mask is packed into alpha once.</summary>
public sealed class PaintImpactSystem : Component
{
    public const string BodyImpactSound = "sounds/paintball/body/impact.sound";
    public const string SurfaceImpactSound = "sounds/paintball/impact.sound";
    public static string ImpactSoundFor(GameObject target) =>
        ArenaShield.Find(target) is null && PaintballCombatant.Find(target) is not null
            ? BodyImpactSound : SurfaceImpactSound;
    public const int Capacity = 96;
    public const float SplashScale = 1.15f;
    public const int DropletCapacity = 48;
    public int ActiveDroplets => droplets.Count(x => x.Age < x.Lifetime);
    private readonly List<Droplet> droplets = new();
    private int dropletCursor;
    private sealed class Droplet
    {
        public GameObject Object;
        public ModelRenderer Renderer;
        public Vector3 Velocity;
        public float Age, Lifetime, Radius;
    }
    public const float Duration = 75;
    public int ActiveCount => marks.Count;
    internal Decal OldestDecal => marks.TryPeek(out var mark) ? mark.Decal : null;
    private static Texture splash;
    private const string SplashPath = "textures/paint/paint_splash_irregular_v2.png";
    private static string loadedSplashPath;
    internal static Texture SplashTexture { get { PrepareTextures(); return splash; } }
    private static Texture wetSurface;
    private readonly Queue<Mark> marks = new();
    private int sequence;
    private sealed class Mark { public GameObject Object; public Decal Decal; public Color Tint; public float Age; }
    public static PaintImpactSystem Find(Scene scene) => scene.GetAllComponents<PaintImpactSystem>().FirstOrDefault();
    protected override void OnStart() => PrepareTextures();
    private static void PrepareTextures()
    {
        if (loadedSplashPath == SplashPath && splash is { IsValid: true } && wetSurface is { IsValid: true }) return;
        using var source=Bitmap.CreateFromBytes(FileSystem.Mounted.ReadAllBytes(SplashPath).ToArray());
        using var mask=source.Resize(512,512);
        var pixels=mask.GetPixels();
        // A mask is data: preserve its coverage, use white RGB for runtime team tint.
        for(int i=0;i<pixels.Length;i++) pixels[i]=Color.White.WithAlpha(pixels[i].r);
        mask.SetPixels(pixels);splash=mask.ToTexture();
        using var rmo=new Bitmap(1,1);
        rmo.Clear(new Color(.25f,0,1,1));wetSurface=rmo.ToTexture();
        loadedSplashPath = SplashPath;
    }
    public void Spawn(SceneTraceResult hit,Color tint)
    {
        if(!MultiplayerSession.Online){SpawnLocal(hit,tint);return;}
        if(!MultiplayerSession.Authority || !hit.Hit)return;
        if(ArenaShield.Find(hit.GameObject) is {} shield){shield.AddPaint(hit,tint);NetworkEffects.Sound("sounds/paintball/impact.sound",hit.HitPosition,System.Guid.Empty);return;}
        if(PaintballCombatant.Find(hit.GameObject) is {} actor)
        {
            (actor.Components.Get<CharacterPaint>() ?? actor.Components.Create<CharacterPaint>()).Add(hit,tint);
            NetworkEffects.Sound(BodyImpactSound,hit.HitPosition,System.Guid.Empty);return;
        }
        MultiplayerSession.Find(Scene)?.AddWorldPaint(hit.HitPosition,hit.Normal,tint);
    }
    internal void SpawnLocal(SceneTraceResult hit,Color tint,bool freshImpact=true,float age=0)
    {
        if(!hit.Hit)return;
        if(freshImpact)Sound.Play(ImpactSoundFor(hit.GameObject),hit.HitPosition);
        PrepareTextures();
        if(freshImpact)SpawnDroplets(hit.HitPosition,hit.Normal,tint);
        if(ArenaShield.Find(hit.GameObject) is {} shield){shield.AddPaint(hit,tint);return;}
        if(ArenaShield.Find(hit.GameObject) is null && PaintballCombatant.Find(hit.GameObject) is {} actor)
        {
            var clothing=actor.Components.Get<CharacterPaint>() ?? actor.Components.Create<CharacterPaint>();
            clothing.Add(hit,tint);
            return;
        }
        var rendered=Scene.Trace.Ray(hit.HitPosition+hit.Normal*2,hit.HitPosition-hit.Normal*16)
            .WithSurfaceMeshes().UsePhysicsWorld(!Game.IsEditor).WithoutTags("paintball_debris").Run();
        if(rendered.Hit && rendered.GameObject==hit.GameObject)hit=rendered;
        // Recycle the oldest native decal at capacity instead of rebuilding its
        // GameObject, component and definition for every sustained-fire impact.
        Mark mark=marks.Count>=Capacity ? marks.Dequeue() : null;
        if(mark is not null && (!mark.Object.IsValid() || !mark.Decal.IsValid()))
        {
            mark.Object?.Destroy();mark=null;
        }
        if(mark is null)
        {
            var fresh=new GameObject(true,"Paint splash decal"){NetworkMode=NetworkMode.Never};
            var component=fresh.Components.Create<Decal>();
            component.Decals=new List<DecalDefinition> { new() { ColorTexture=splash,RoughMetalOcclusionTexture=wetSurface,Width=1,Height=1,ColorMix=1 } };
            mark=new Mark { Object=fresh,Decal=component };
        }
        var go=mark.Object;
        go.Parent=hit.GameObject.IsValid() ? hit.GameObject : null;
        go.WorldScale=Vector3.One;
        go.WorldPosition=hit.HitPosition+hit.Normal*.35f;
        go.WorldRotation=Rotation.LookAt(-hit.Normal);
        var decal=mark.Decal;
        float size=(5.5f+(sequence%5)*.45f)*SplashScale;
        decal.Size=new Vector2(size,size);decal.Depth=8f;
        decal.Rotation=(sequence++*137.508f)%360;decal.ColorTint=tint;
        decal.AttenuationAngle=.4f;decal.LifeTime=0;decal.Transient=false;
        mark.Tint=tint;mark.Age=age;
        marks.Enqueue(mark);
    }
    private void SpawnDroplets(Vector3 position, Vector3 normal, Color tint)
    {
        var basis = Rotation.LookAt(normal);
        for (int i=0;i<6;i++)
        {
            Droplet drop;
            if (droplets.Count < DropletCapacity)
            {
                var go = new GameObject(false,"Paint impact droplet"){NetworkMode=NetworkMode.Never};
                go.WorldPosition=position;
                go.WorldScale=new Vector3(.0035f);
                go.Tags.Add("paintball_debris");
                drop = new Droplet { Object=go, Renderer=go.Components.Create<ModelRenderer>() };
                drop.Renderer.Model=Model.Load("models/dev/sphere.vmdl");
                droplets.Add(drop);
            }
            else drop=droplets[dropletCursor++ % DropletCapacity];
            float angle=(sequence*2.399963f+i*1.047198f);
            var radial=basis.Right*MathF.Cos(angle)+basis.Up*MathF.Sin(angle);
            drop.Age=0;drop.Lifetime=.22f+(i%3)*.055f;
            drop.Radius=.0035f+(i%3)*.0015f;
            drop.Velocity=normal*(24+i*3)+radial*(30+i*5);
            drop.Object.Enabled=false;
            drop.Object.WorldPosition=position+normal*.5f;
            drop.Object.WorldScale=Vector3.One*drop.Radius;
            drop.Renderer.Tint=tint;
            drop.Object.Enabled=true;
        }
    }
    protected override void OnUpdate()
    {
        foreach(var drop in droplets)
        {
            if(drop.Age>=drop.Lifetime)continue;
            drop.Age+=Time.Delta;
            if(drop.Age>=drop.Lifetime) { drop.Object.Enabled=false; continue; }
            drop.Object.WorldPosition+=drop.Velocity*Time.Delta+Vector3.Down*(90*Time.Delta*Time.Delta);
            drop.Velocity+=Vector3.Down*(180*Time.Delta);
            float tail=((drop.Lifetime-drop.Age)/.1f).Clamp(0,1);
            drop.Object.WorldScale=Vector3.One*(drop.Radius*tail);
        }
        foreach(var mark in marks)
        {
            mark.Age+=Time.Delta;
            if(mark.Decal.IsValid() && mark.Age>Duration-12)
                mark.Decal.ColorTint=mark.Tint.WithAlpha(((Duration-mark.Age)/12).Clamp(0,1));
        }
        while(marks.TryPeek(out var first) && (first.Age>=Duration || !first.Object.IsValid()))
            marks.Dequeue().Object?.Destroy();
    }
    public void Clear()
    {
        while(marks.Count>0)marks.Dequeue().Object?.Destroy();
        foreach(var drop in droplets)drop.Object?.Destroy();
        droplets.Clear();dropletCursor=0;
    }
    protected override void OnDestroy() => Clear();
}
