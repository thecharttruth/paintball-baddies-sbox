using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in repeated physical hits against the actual practice plate.</summary>
public sealed class TargetBurstReview : Component
{
    private PaintballMarker weapon;
    private PaintballTarget target;
    private PlayerController player;
    private float elapsed,lastShot=-1,minInterval=float.MaxValue;
    private int fired,samples,initialHits;
    private bool stable=true,done;
    private Vector3 position;
    private Rotation rotation;
    private Color tint;
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(!weapon.IsValid() && elapsed>1)
        {
            player=Scene.GetAllComponents<PlayerController>().First();
            weapon=player.Components.Get<PaintballMarker>();weapon.AcceptInput=false;
            player.UseInputControls=false;player.WishVelocity=Vector3.Zero;
            target=Scene.GetAllComponents<PaintballTarget>().First(x=>System.MathF.Abs(x.WorldPosition.y)<1);
            initialHits=target.HitsReceived;
            position=target.WorldPosition;rotation=target.WorldRotation;tint=target.Components.Get<ModelRenderer>().Tint;
        }
        if(!target.IsValid() || done)return;
        samples++;
        stable &= (target.WorldPosition-position).Length<.001f && target.WorldRotation==rotation && target.Components.Get<ModelRenderer>().Tint==tint;
        if(elapsed<4 && weapon.FireAt(position))
        {
            if(lastShot>=0)minInterval=System.MathF.Min(minInterval,elapsed-lastShot);
            lastShot=elapsed;fired++;
        }
        if(elapsed>5)
        {
            var impacts=PaintImpactSystem.Find(Scene);
            int hits=target.HitsReceived-initialHits;
            Log.Info("TARGET_BURST "+Json.Serialize(new{fired,hits,minInterval,samples,stable,decals=impacts.ActiveCount,droplets=impacts.ActiveDroplets,
                passed=fired>=8 && hits==fired && minInterval>=PaintballFlight.ShotInterval-.002f && stable && impacts.ActiveCount>=fired && impacts.ActiveDroplets==0}));
            done=true;
        }
    }
    protected override void OnDestroy()
    {
        if(weapon.IsValid())weapon.AcceptInput=true;
        if(player.IsValid())player.UseInputControls=true;
    }
}
