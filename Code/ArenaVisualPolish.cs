using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Restrained native post processing; objective effects stay below the aim line.</summary>
public sealed class ArenaVisualPolish : Component
{
    protected override void OnStart()
    {
        if(Scene.GetAllComponents<Bloom>().Any())return;
        var bloom=Components.Create<Bloom>();
        bloom.Strength=.10f;bloom.Threshold=2;bloom.Tint=Color.White;
    }
}

/// <summary>Shared, inexpensive native line geometry. No colliders, lights or shadows.</summary>
public static class ArenaGlow
{
    public static readonly Color Gold=new(1,.64f,.10f);
    public static readonly Color Teal=new(.12f,.95f,.78f);
    public static LineRenderer Ring(GameObject parent,string name,float radius,float width,Color color,int segments=48)
    {
        var go=new GameObject(parent){Name=name,NetworkMode=NetworkMode.Never};go.Tags.Add("paintball_debris");
        var line=go.Components.Create<LineRenderer>();
        line.UseVectorPoints=true;line.VectorPoints=new();
        for(int i=0;i<=segments;i++)
        {
            float angle=i*MathF.Tau/segments;
            line.VectorPoints.Add(new Vector3(MathF.Cos(angle)*radius,MathF.Sin(angle)*radius,0));
        }
        line.Width=new Curve(new Curve.Frame(0,width),new Curve.Frame(1,width));
        line.Color=Gradient.FromColors(color,color);
        line.Face=SceneLineObject.FaceMode.Camera;
        line.CastShadows=false;line.Lighting=false;line.Opaque=false;line.Additive=true;
        var local=go.Components.Create<LocalGlowLine>();local.SetPoints(line.VectorPoints);
        return line;
    }
}

/// <summary>LineRenderer vector points are world-space; follow the cosmetic parent explicitly.</summary>
public sealed class LocalGlowLine : Component
{
    private List<Vector3> points=new();
    private Transform previous;
    private bool ready;
    public void SetPoints(List<Vector3> value){points=value.ToList();ready=false;Refresh();}
    private void Refresh()
    {
        if(ready && WorldTransform.Equals(previous))return;
        previous=WorldTransform;ready=true;
        var line=Components.Get<LineRenderer>();
        if(line.IsValid())line.VectorPoints=points.Select(p=>WorldTransform.PointToWorld(p)).ToList();
    }
    protected override void OnUpdate()=>Refresh();
}

/// <summary>One smooth bank outline, four approach marks, and a local deposit progress ring.</summary>
public sealed class HeistBankPresentation : Component
{
    private LineRenderer outline,progress;
    private float age;
    private int progressStep=-1;
    protected override void OnStart()
    {
        outline=ArenaGlow.Ring(GameObject,"Bank perimeter",MultiplayerSession.BankRadius,2.3f,ArenaGlow.Teal*2);
        progress=ArenaGlow.Ring(GameObject,"Deposit progress",MultiplayerSession.BankRadius-7,2,ArenaGlow.Gold*2);
        for(int i=0;i<4;i++)
        {
            var mark=TrainingRange.Box(GameObject,"Bank approach light",Rotation.FromYaw(i*90).Forward*(MultiplayerSession.BankRadius+7),new Vector3(8,2,1),ArenaGlow.Teal*2);
            mark.LocalRotation=Rotation.FromYaw(i*90);mark.NetworkMode=NetworkMode.Never;
            var renderer=mark.Components.Get<ModelRenderer>();renderer.MaterialOverride=Material.Load("materials/dev/primary_white_emissive.vmat");
            renderer.RenderType=ModelRenderer.ShadowRenderType.Off;
        }
    }
    protected override void OnUpdate()
    {
        age+=Time.Delta;
        var player=MultiplayerSession.LocalPlayer(Scene)?.Components.Get<PaintballCombatant>();
        int step=(int)MathF.Ceiling((player?.BankProgress ?? 0)*48);
        progress.Enabled=step>0;
        if(step!=progressStep)
        {
            progressStep=step;
            progress.Components.Get<LocalGlowLine>().SetPoints(Enumerable.Range(0,Math.Max(2,step+1)).Select(i=>
                new Vector3(MathF.Cos(i*MathF.Tau/48),MathF.Sin(i*MathF.Tau/48),0)*(MultiplayerSession.BankRadius-7)).ToList());
        }
        // A gentle breathing outline, not a flashing beacon obscuring combat.
        var tint=ArenaGlow.Teal*(1.65f+.18f*MathF.Sin(age*2));
        outline.Color=Gradient.FromColors(tint,tint);
    }
}

/// <summary>Bounded world-space payoff for a completed deposit or a newly arrived bank.</summary>
public sealed class HeistBankBurst : Component
{
    public const int MaximumBursts=4;
    public int Amount {get;set;}
    public float Age {get;private set;}
    private LineRenderer ring;
    private ParticleEffect particles;
    public static void Spawn(Scene scene,Vector3 position,int amount)
    {
        if(!scene.IsValid())return;
        foreach(var old in scene.GetAllComponents<HeistBankBurst>().OrderByDescending(x=>x.Age).SkipLast(MaximumBursts-1).ToArray())old.GameObject.Destroy();
        var go=new GameObject(false,"Bank secure pulse"){WorldPosition=position+Vector3.Up*1.8f,NetworkMode=NetworkMode.Never};
        go.Tags.Add("paintball_debris");var burst=go.Components.Create<HeistBankBurst>();burst.Amount=amount;go.Enabled=true;
    }
    protected override void OnStart()
    {
        ring=ArenaGlow.Ring(GameObject,"Secure ripple",MultiplayerSession.BankRadius,2.5f,ArenaGlow.Teal*2.2f);
        if(Amount<=0)return;
        particles=Components.Create<ParticleEffect>();particles.MaxParticles=18;particles.Lifetime=.75f;
        particles.LocalSpace=0;particles.ApplyAlpha=false;particles.ApplyColor=false;particles.ApplyRotation=false;particles.ApplyShape=false;
        particles.Force=true;particles.ForceDirection=Vector3.Down*150;particles.ForceScale=1;particles.ForceSpace=ParticleEffect.SimulationSpace.World;
        particles.Collision=false;
        var renderer=Components.Create<ParticleSpriteRenderer>();
        renderer.Sprite=new Sprite {Animations=[new Sprite.Animation {Name="Default",Frames=[new Sprite.Frame {Texture=Texture.White}]}]};
        renderer.Scale=1;renderer.Additive=true;renderer.Lighting=false;renderer.Shadows=false;
        particles.OnStep=(p,dt)=>p.Alpha=((.75f-p.Age)/.4f).Clamp(0,1);
        int count=Math.Clamp(Amount+6,8,18);
        for(int i=0;i<count;i++)
        {
            var direction=Rotation.FromYaw(i*360f/count).Forward;
            var p=particles.Emit(WorldPosition+direction*MultiplayerSession.BankRadius,0);
            p.Size=new Vector3(1.1f);p.Color=ArenaGlow.Gold*1.6f;
            p.Velocity=direction*12+Vector3.Up*(40+i%4*8);
        }
    }
    protected override void OnUpdate()
    {
        Age+=Time.Delta;
        if(Age>=.85f){GameObject.Destroy();return;}
        ring.LocalScale=Vector3.One*(1+Age*.28f);
        var tint=(Amount>0 ? ArenaGlow.Gold : ArenaGlow.Teal)*1.8f;
        tint=tint.WithAlpha((1-Age/.85f)*(1-Age/.85f));ring.Color=Gradient.FromColors(tint,tint);
    }
}
