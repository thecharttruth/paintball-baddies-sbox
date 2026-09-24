using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Host-collected physical token stack; native Citizen prop for its local visual.</summary>
public sealed class HeistChip : Component
{
    [Sync(SyncFlags.FromHost)] public int Amount {get;set;}=1;
    [Sync(SyncFlags.FromHost)] public int Round {get;set;}
    [Sync(SyncFlags.FromHost)] public bool Collected {get;private set;}
    public float Lifetime {get;set;}=18;
    private Guid droppedBy;
    private float pickupDelay=.55f,ownerDelay=2;
    private GameObject visual;
    private LineRenderer halo;
    private float age;
    public void ResetPickupDelay(Guid owner){droppedBy=owner;pickupDelay=.55f;ownerDelay=2;}
    protected override void OnStart()
    {
        GameObject.Tags.Add("paintball_debris");
        visual=new GameObject(GameObject){Name="Gold paint chip",NetworkMode=NetworkMode.Never};
        var renderer=visual.Components.Create<ModelRenderer>();renderer.Model=Model.Load("models/citizen_props/coin01.vmdl");
        renderer.Tint=new Color(1,.72f,.12f);
        float size=renderer.Model.Bounds.Size.Length;visual.LocalScale=Vector3.One*(18/MathF.Max(size,1));
        halo=ArenaGlow.Ring(GameObject,"Collectible gold halo",10,.8f,ArenaGlow.Gold*1.8f,24);
        halo.LocalPosition=Vector3.Up*1.5f;
    }
    protected override void OnUpdate()
    {
        age+=Time.Delta;
        if(visual.IsValid()){visual.Enabled=!Collected;visual.LocalPosition=Vector3.Up*(15+MathF.Sin(age*3)*2);visual.LocalRotation=Rotation.FromYaw(age*70)*Rotation.FromPitch(75);}
        if(halo.IsValid())halo.Enabled=!Collected;
        if(!MultiplayerSession.Authority || Collected)return;
        var session=MultiplayerSession.Find(Scene);
        if(!session.IsValid() || !session.InRound || !session.IsHeist || Round!=session.RoundId){GameObject.Destroy();return;}
        Lifetime-=Time.Delta;pickupDelay-=Time.Delta;ownerDelay-=Time.Delta;
        if(Lifetime<=0){GameObject.Destroy();return;}
        foreach(var actor in Scene.GetAllComponents<PaintballCombatant>().OrderBy(a=>(a.WorldPosition-WorldPosition).Length))
            if(TryCollect(actor))break;
    }
    public bool TryCollect(PaintballCombatant actor)
    {
        var session=MultiplayerSession.Find(Scene);
        if(!MultiplayerSession.Authority || Collected || pickupDelay>0 || !actor.IsValid() || !actor.GameObject.Active
            || !actor.AcceptHits || actor.Eliminated || actor.Components.Get<CloseCombat>()?.Incapacitated==true
            || !session.IsValid() || !session.InRound || !session.IsHeist || session.RoundId!=Round
            || (ownerDelay>0 && actor.GameObject.Id==droppedBy)
            || MathF.Abs(actor.WorldPosition.z-WorldPosition.z)>40 || (actor.WorldPosition-WorldPosition).WithZ(0).Length>35)return false;
        if(Scene.Trace.Ray(actor.WorldPosition+Vector3.Up*28,WorldPosition+Vector3.Up*15)
            .IgnoreGameObjectHierarchy(actor.GameObject).IgnoreGameObjectHierarchy(GameObject).WithoutTags("paintball_debris").Run().Hit)return false;
        Collected=true;actor.CarriedTokens+=Amount;actor.HeistFeedback("collect",Amount);GameObject.Destroy();return true;
    }
}
