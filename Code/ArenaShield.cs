using Sandbox;
using System;
using Sandbox.Citizen;
namespace PaintballBaddies;

/// <summary>Two reusable clear shields shared by all competitors.</summary>
public sealed class ArenaShield : Component
{
    public const int Capacity=5;
    public const int MeleeDamage=(Capacity+1)/2;
    public enum MeleeResult { None, Absorbed, Broken }
    [Sync(SyncFlags.FromHost)] public int Remaining { get; private set; }=Capacity;
    [Sync(SyncFlags.FromHost)] public PaintballCombatant Owner { get; private set; }
    private GameObject panel;
    private ModelRenderer cracks;
    private int lastRemaining=Capacity;
    private float hitKick;
    public int Shatters {get;private set;}
    [Sync(SyncFlags.FromHost)] public float RespawnDelay {get;private set;}
    [Sync(SyncFlags.FromHost)] public Transform PickupTransform {get;private set;}
    [Sync(SyncFlags.FromHost)] public string PaintHistory {get;private set;}="";
    private string receivedPaint;
    private readonly List<ShieldMark> history=new();
    private sealed class ShieldMark {public Vector3 Point {get;set;} public Color Tint {get;set;}}
    private readonly Random random=new();
    private Vector3 lastSpawn;
    private bool ready,wasHeld;
    private IDisposable carryHook;
    private SkinnedModelRenderer carryBody;
    private readonly Queue<GameObject> paint=new();
    public int PaintCount=>paint.Count;
    public float GripError { get; private set; }
    public bool Stowed => Owner.IsValid() && (Owner.Components.Get<CloseCombat>() is {Incapacitated:true} || Owner.Components.Get<CloseCombat>() is {Attacking:true} || Owner.Components.Get<CoverController>() is {Attached:true,Peeking:false}
        || Owner.Components.Get<CoverController>()?.Sliding==true || Owner.Components.Get<VaultController>()?.IsVaulting==true
        || Owner.Components.Get<ArenaOpponent>()?.UsingCover==true);
    private static Vector3 Handle => new(-5,8,0);
    public void ApplyGrip(SkinnedModelRenderer body,CitizenAnimationHelper animation)
    {
        carryBody=body;
        if(!Stowed)animation.IkLeftHand=null; // Native left-hand carry pose owns this arm.
    }
    public void AddPaint(SceneTraceResult hit,Color tint)
    {
        if(!MultiplayerSession.Authority)return;
        var record=new ShieldMark {Point=WorldTransform.PointToLocal(hit.HitPosition),Tint=tint};
        if(MultiplayerSession.Online){history.Add(record);while(history.Count>Capacity)history.RemoveAt(0);PaintHistory=Json.Serialize(history);}
        ApplyPaint(record);
    }
    private void ApplyPaint(ShieldMark record)
    {
        while(paint.Count>=Capacity)paint.Dequeue()?.Destroy();
        var patch=new GameObject(GameObject){Name="Shield paint splatter",NetworkMode=NetworkMode.Never};patch.Tags.Add("paintball_debris");
        var local=record.Point;
        float y=local.y.Clamp(-11,11);float angle=MathF.Asin(y/30);
        var normal=new Vector3(MathF.Cos(angle),MathF.Sin(angle),0);
        patch.LocalPosition=new Vector3(30*(MathF.Cos(angle)-1),y,local.z.Clamp(-17,17))+normal*.46f;
        patch.LocalRotation=Rotation.FromYaw(angle*180/MathF.PI);
        var renderer=patch.Components.Create<ModelRenderer>();renderer.Model=Model.Load("models/gear/clear_shield/paint_patch.vmdl");
        renderer.MaterialOverride=Material.Load("models/gear/clear_shield/paint.vmat");renderer.Tint=record.Tint;
        float splashScale=PaintImpactSystem.SplashScale;
        // Preserve the shallow curved patch's fit as its surface area grows.
        patch.LocalScale=new Vector3(splashScale*splashScale,splashScale,splashScale);
        paint.Enqueue(patch);
    }
    protected override void OnStart()
    {
        // A held shield follows each peer's displayed hand. Streaming a second
        // world transform fights that attachment and makes it trail the wearer.
        if(MultiplayerSession.Authority)GameObject.Network.Flags|=NetworkFlags.NoTransformSync;
        panel=new GameObject(GameObject){Name="Clear curved polycarbonate shield",NetworkMode=NetworkMode.Never};
        panel.Components.Create<ModelRenderer>().Model=Model.Load("models/gear/clear_shield/panel.vmdl");
        var crackObject=new GameObject(GameObject){Name="Shield fracture seams",NetworkMode=NetworkMode.Never};
        cracks=crackObject.Components.Create<ModelRenderer>();cracks.Model=Model.Load("models/gear/clear_shield/fracture/cracks.vmdl");
        cracks.Tint=new Color(.8f,.91f,.96f);cracks.Enabled=false;
        var rim=new GameObject(GameObject){Name="Rounded shield rim",NetworkMode=NetworkMode.Never};
        rim.Components.Create<ModelRenderer>().Model=Model.Load("models/gear/clear_shield/rim.vmdl");
        TrainingRange.Box(GameObject,"Shield forearm grip",new(-5,8,0),new(1,9,1.5f),new Color(.12f,.15f,.17f));
        foreach(float side in new[]{-1f,1f})TrainingRange.Box(GameObject,"Shield handle bracket",new(-2.5f,8+side*4,0),new(5,.8f,.8f),new Color(.12f,.15f,.17f));
        foreach(var child in GameObject.Children)child.NetworkMode=NetworkMode.Never;
        ready=true;if(MultiplayerSession.Authority)Respawn();
        carryHook=Scene.AddHook(GameObjectSystem.Stage.FinishUpdate,100,()=>
        {
            if(ready && Owner.IsValid())FollowOwner();
        },nameof(ArenaShield),"Align shield with final displayed hand");
    }
    private void Respawn()
    {
        Owner=null;if(!MultiplayerSession.Online)GameObject.Parent=null;Remaining=Capacity;lastRemaining=Capacity;hitKick=0;
        var authoredSpawns=ArenaMap.ShieldSpawns(Scene);
        // Authored points support raised floors and other arenas. The original map
        // retains its random floor search when no pickup markers have been placed.
        for(int attempt=0;attempt<100;attempt++)
        {
            var anchor=authoredSpawns.Length>0 ? authoredSpawns[random.Next(authoredSpawns.Length)].WorldPosition
                : new Vector3(random.Next(-950,950),random.Next(-650,1400),0);
            var point=anchor+Vector3.Up*100;
            var floor=Scene.Trace.Ray(point,anchor-Vector3.Up*20).IgnoreGameObjectHierarchy(GameObject).Run();
            if(!floor.Hit || floor.Normal.z<.95f || floor.HitPosition.z>anchor.z+8)continue;
            point=floor.HitPosition+Vector3.Up*24;
            if((point-lastSpawn).Length<180)continue;
            if(Scene.GetAllComponents<ArenaShield>().Any(x=>x!=this&&(x.WorldPosition-point).Length<180))continue;
            if(Scene.Trace.Sphere(23,point,point+Vector3.Up*24).IgnoreGameObjectHierarchy(GameObject).Run().Hit)continue;
            WorldPosition=point;WorldRotation=Rotation.FromYaw(random.Next(360));lastSpawn=point;PickupTransform=WorldTransform;
            foreach(var renderer in Components.GetAll<ModelRenderer>(FindMode.EverythingInSelfAndDescendants))renderer.Enabled=true;
            cracks.Enabled=false;
            RespawnDelay=0;return;
        }
        RespawnDelay=1;
    }
    public bool Collect(PaintballCombatant actor)
    {
        if(!MultiplayerSession.Authority || !ready || RespawnDelay>0 || Owner.IsValid() || !actor.IsValid() || actor.Eliminated
            || actor.Components.Get<CloseCombat>()?.Incapacitated==true)return false;
        if(Scene.GetAllComponents<ArenaShield>().Any(x=>x!=this&&x.Owner==actor))return false;
        wasHeld=true;Owner=actor;if(!MultiplayerSession.Online)GameObject.Parent=actor.GameObject;Remaining=Capacity;lastRemaining=Capacity;hitKick=0;
        carryBody=null;
        PlaySound("sounds/paintball/shield_collect.sound",actor.WorldPosition);
        FollowOwner();return true;
    }
    private void FollowOwner()
    {
        // Collection/respawn can change owners before that actor's presentation
        // update calls ApplyGrip. Resolve the current wearer immediately instead
        // of displaying a fallback pose (or the previous wearer's cached hand).
        carryBody=Owner.Components.Get<CitizenPlayerPresentation>()?.FootstepRenderer
            ?? Owner.Components.Get<CitizenOpponentPresentation>()?.FootstepRenderer;
        var player=Owner.Components.Get<PlayerController>();
        var facing=carryBody.IsValid() ? Rotation.FromYaw(carryBody.WorldRotation.Angles().yaw)
            : player.IsValid() ? Rotation.FromYaw(player.EyeAngles.yaw) : Owner.WorldRotation;
        var crouched=player.IsValid() ? player.IsDucking : Owner.Components.Get<ArenaOpponent>()?.CoverCrouched==true;
        var hand=carryBody.IsValid() ? carryBody.GetBoneObject("hand_L") : null;
        bool visible=hand.IsValid() && !Stowed;
        if(visible)
        {
            if(!MultiplayerSession.Online && GameObject.Parent!=hand)GameObject.Parent=hand;
            WorldScale=Vector3.One;WorldRotation=facing*Rotation.FromPitch(MathF.Sin(hitKick/.24f*MathF.PI)*8);
            WorldPosition=hand.WorldPosition-WorldRotation*Handle;
            GripError=(hand.WorldPosition-(WorldPosition+WorldRotation*Handle)).Length;
        }
        else {WorldRotation=facing;WorldPosition=Owner.WorldPosition+facing*new Vector3(20,0,crouched ? 27 : 40);}
        foreach(var renderer in Components.GetAll<ModelRenderer>(FindMode.EverythingInSelfAndDescendants))renderer.Enabled=visible;
        cracks.Enabled=visible && Remaining<=Capacity/2 && Remaining>0;
    }
    protected override void OnUpdate()
    {
        if(!ready)return;
        if(Remaining<lastRemaining && Remaining>0)hitKick=.24f;
        lastRemaining=Remaining;hitKick=MathF.Max(0,hitKick-Time.Delta);
        if(!MultiplayerSession.Authority)
        {
            if(receivedPaint!=PaintHistory)
            {
                while(paint.Count>0)paint.Dequeue()?.Destroy();
                if(!string.IsNullOrEmpty(PaintHistory))foreach(var record in Json.Deserialize<List<ShieldMark>>(PaintHistory))ApplyPaint(record);
                receivedPaint=PaintHistory;
            }
            if(Owner.IsValid())FollowOwner();
            else
            {
                if(Remaining>0)WorldTransform=PickupTransform;
                foreach(var renderer in Components.GetAll<ModelRenderer>(FindMode.EverythingInSelfAndDescendants))renderer.Enabled=Remaining>0;
                cracks.Enabled=false;
            }
            return;
        }
        if(wasHeld && !Owner.IsValid()){Break();return;}
        if(RespawnDelay>0){RespawnDelay-=Time.Delta;if(RespawnDelay<=0)Respawn();return;}
        if(Owner.IsValid())
        {
            if(Owner.Eliminated || Owner.Components.Get<CloseCombat>()?.Incapacitated==true){Break();return;}
            FollowOwner();return;
        }
        foreach(var actor in Scene.GetAllComponents<PaintballCombatant>())
        {
            if((actor.WorldPosition-WorldPosition.WithZ(actor.WorldPosition.z)).Length>40)continue;
            if(Scene.Trace.Ray(actor.EyePosition,WorldPosition).IgnoreGameObjectHierarchy(actor.GameObject).IgnoreGameObjectHierarchy(GameObject).Run().Hit)continue;
            if(Collect(actor))break;
        }
    }
    public void Absorb()
    {
        if(!MultiplayerSession.Authority || !Owner.IsValid() || RespawnDelay>0 || Stowed)return;
        Damage(1,-WorldRotation.Forward);
    }
    private void Damage(int amount,Vector3 direction)
    {
        Remaining=Math.Max(0,Remaining-amount);
        if(Remaining==0)Break(true,direction);
    }
    private bool BlocksFrom(Vector3 source)
        => Owner.IsValid() && Remaining>0 && RespawnDelay<=0 && !Stowed
            && Vector3.Dot((source-Owner.WorldPosition).WithZ(0).Normal,WorldRotation.Forward.WithZ(0).Normal)>.05f;
    public static MeleeResult TryAbsorbMelee(PaintballCombatant actor,Vector3 source)
    {
        if(!MultiplayerSession.Authority || !actor.IsValid())return MeleeResult.None;
        var shield=actor.Scene.GetAllComponents<ArenaShield>().FirstOrDefault(x=>x.Owner==actor && x.BlocksFrom(source));
        if(!shield.IsValid())return MeleeResult.None;
        shield.Damage(MeleeDamage,(actor.WorldPosition-source).WithZ(0).Normal);
        return shield.Remaining==0 ? MeleeResult.Broken : MeleeResult.Absorbed;
    }
    public static void ReleaseForKnockdown(PaintballCombatant actor)
    {
        if(!MultiplayerSession.Authority || !actor.IsValid())return;
        foreach(var shield in actor.Scene.GetAllComponents<ArenaShield>().Where(x=>x.Owner==actor).ToArray())
            shield.Break(); // Forfeit the pickup; actual durability breaks alone shatter it.
    }
    private void Break(bool shatter=false,Vector3 direction=default)
    {
        if(shatter)
        {
            Shatters++;
            int seed=Game.Random.Int(0,1000000);
            if(MultiplayerSession.Online)NetworkEffects.ShieldBreak(WorldTransform,direction,seed);
            else ShieldBreakEffect.Spawn(Scene,WorldTransform,direction,seed);
            PlaySound("sounds/paintball/shield_break.sound",WorldPosition);
        }
        // Detach before the owner is removed at a round boundary.
        if(!MultiplayerSession.Online)GameObject.Parent=null;Owner=null;Remaining=0;RespawnDelay=4;
        while(paint.Count>0)paint.Dequeue()?.Destroy();
        wasHeld=false;carryBody=null;history.Clear();PaintHistory="";
        foreach(var renderer in Components.GetAll<ModelRenderer>(FindMode.EverythingInSelfAndDescendants))renderer.Enabled=false;
        foreach(var decal in Components.GetAll<Decal>(FindMode.EverythingInSelfAndDescendants).ToArray())decal.GameObject.Destroy();
    }
    public static ArenaShield Find(GameObject hit)
    {
        for(var go=hit;go is not null;go=go.Parent)
            if(go.Components.Get<ArenaShield>() is {} shield)return shield;
        return null;
    }
    /// <summary>Route shield hits before either competitor's score or clothing changes.</summary>
    public static bool TryAbsorbImpact(SceneTraceResult hit, Vector3 source, Color tint)
    {
        var shield=Find(hit.GameObject);
        bool physical=shield is not null;
        if(shield is null && PaintballCombatant.Find(hit.GameObject) is {} actor)
        {
            shield=actor.Scene.GetAllComponents<ArenaShield>().FirstOrDefault(s=>s.Owner==actor && s.Remaining>0 && !s.Stowed);
            if(shield is not null)
            {
                // Front half of the wearer. Side/rear shots still tag the player;
                // a visibly stowed shield supplies no protection.
                if(!shield.BlocksFrom(source))shield=null;
            }
        }
        if(shield is null)return false;
        if(physical)PaintImpactSystem.Find(shield.Scene)?.Spawn(hit,tint);
        else
        {
            // The frontal guard also catches a head/leg hit just outside the
            // visible panel. Place its mark on the curved panel, not the wearer.
            shield.AddPaint(hit,tint);
            PlaySound("sounds/paintball/impact.sound",shield.WorldPosition);
        }
        shield.Absorb();
        return true;
    }
    private static void PlaySound(string path,Vector3 position)
    {if(MultiplayerSession.Online)NetworkEffects.Sound(path,position,Guid.Empty);else Sound.Play(path,position);}
    protected override void OnDestroy()=>carryHook?.Dispose();
    public static void ResetArena(Scene scene)
    {
        if(!MultiplayerSession.Authority)return;
        foreach(var shield in scene.GetAllComponents<ArenaShield>().ToArray())shield.GameObject.Destroy();
        for(int i=0;i<2;i++)new GameObject(true,$"Clear shield pickup {i+1}").Components.Create<ArenaShield>();
    }
}
