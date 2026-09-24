using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
namespace PaintballBaddies;

/// <summary>The curved panel becomes bounded native model particles, never gameplay colliders.</summary>
public sealed class ShieldBreakEffect : Component
{
    public const float Lifetime=3;
    public const int MaximumBursts=3;
    public Vector3 ImpactDirection {get;set;}
    public int Seed {get;set;}
    public float Age {get;private set;}
    public int Fragments => Components.GetAll<ParticleEffect>(FindMode.EverythingInSelfAndDescendants)
        .Where(x=>x.Components.Get<ParticleModelRenderer>().IsValid()).Sum(x=>x.ParticleCount);
    public sealed class Piece {public string Model {get;set;} public float[] Center {get;set;}}
    public sealed class Layout {public List<Piece> Pieces {get;set;}}
    private static Layout layout;

    public static void Spawn(Scene scene,Transform transform,Vector3 direction,int seed)
    {
        foreach(var older in scene.GetAllComponents<ShieldBreakEffect>().OrderByDescending(x=>x.Age).SkipLast(MaximumBursts-1).ToArray())
            older.GameObject.Destroy();
        var go=new GameObject(false,"Shield shatter"){WorldTransform=transform,NetworkMode=NetworkMode.Never};
        go.Tags.Add("paintball_debris");
        var effect=go.Components.Create<ShieldBreakEffect>();effect.ImpactDirection=direction;effect.Seed=seed;go.Enabled=true;
    }
    protected override void OnStart()
    {
        layout ??=Json.Deserialize<Layout>(FileSystem.Mounted.ReadAllText("models/gear/clear_shield/fracture/layout.json"));
        var random=new Random(Seed);
        // Brief edge highlights make the transparent pieces readable without a
        // full-screen flash. They share the seeded, bounded shatter lifecycle.
        var glints=Components.Create<ParticleEffect>();
        glints.MaxParticles=16;glints.Lifetime=.38f;glints.LocalSpace=0;
        glints.ApplyAlpha=false;glints.ApplyColor=false;glints.ApplyShape=false;glints.ApplyRotation=false;
        glints.Collision=false;
        var highlights=Components.Create<ParticleSpriteRenderer>();
        highlights.Sprite=new Sprite {Animations=[new Sprite.Animation {Name="Default",Frames=[new Sprite.Frame {Texture=Texture.White}]}]};
        highlights.Additive=true;highlights.Lighting=false;highlights.Shadows=false;highlights.Scale=1;
        glints.OnStep=(p,dt)=>p.Alpha=((.38f-p.Age)/.38f).Clamp(0,1);
        foreach(var piece in layout.Pieces)
        {
            float Rand(float a,float b)=>a+(b-a)*(float)random.NextDouble();
            var center=new Vector3(piece.Center[0],piece.Center[1],piece.Center[2]);
            var go=new GameObject(GameObject){Name="Curved shield fragment",NetworkMode=NetworkMode.Never};go.Tags.Add("paintball_debris");
            var particles=go.Components.Create<ParticleEffect>();
            particles.MaxParticles=1;particles.Lifetime=Lifetime;particles.LocalSpace=0;
            particles.ApplyRotation=false;particles.ApplyShape=false;particles.ApplyAlpha=false;particles.ApplyColor=false;
            particles.Force=true;particles.ForceDirection=Vector3.Down*500;particles.ForceScale=1;
            particles.ForceSpace=ParticleEffect.SimulationSpace.World;
            particles.Damping=.35f;particles.Collision=true;particles.CollisionRadius=.7f;
            particles.CollisionIgnore=new TagSet();particles.CollisionIgnore.Add("paintball_actor");particles.CollisionIgnore.Add("paintball_debris");
            particles.Bounce=.28f;particles.Friction=.6f;particles.PushStrength=0;
            var renderer=go.Components.Create<ParticleModelRenderer>();
            renderer.Choices=new(){new(){Model=Model.Load(piece.Model)}};renderer.Scale=1;renderer.CastShadows=false;renderer.RotateWithGameObject=false;
            var spin=new Angles(Rand(-190,190),Rand(-210,210),Rand(-170,170));
            var start=WorldRotation.Angles();
            particles.OnStep=(p,dt)=>{p.Angles=start+spin*p.Age;p.Alpha=((Lifetime-p.Age)/.55f).Clamp(0,1);};
            var particle=particles.Emit(WorldTransform.PointToWorld(center),0);
            particle.Angles=start;particle.StartAngles=start;particle.Size=Vector3.One;
            particle.Velocity=ImpactDirection.Normal*Rand(95,160)+WorldRotation*new Vector3(Rand(-35,35),center.y*3,center.z*1.5f)+Vector3.Up*Rand(45,100);
            var glint=glints.Emit(WorldTransform.PointToWorld(center),0);
            glint.Size=new Vector3(.75f);glint.Color=new Color(.6f,.88f,1)*1.5f;glint.Velocity=particle.Velocity;
        }
    }
    protected override void OnUpdate(){Age+=Time.Delta;if(Age>Lifetime+.15f)GameObject.Destroy();}
}
