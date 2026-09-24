using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Bounded clothing inertia using the engine's damped spring.</summary>
public sealed class CitizenSuitFollowThrough : Component
{
    private SkinnedModelRenderer body,outfit;
    private Vector3 previousPosition,previousVelocity;
    private bool initialized;
    private Model previousModel;
    private float appliedWeight;
    private bool hasOverride;
    private Vector3.SpringDamped spring=new(){Frequency=6,Damping=.8f};
    public float CurrentWeight { get; private set; }
    public float PeakWeight { get; private set; }
    public bool MorphsReady => outfit.IsValid() && outfit.Model.IsValid() &&
        outfit.Model.Morphs.GetIndex("SuitFollowThroughLUp")>=0;
    public void Configure(SkinnedModelRenderer skeleton,SkinnedModelRenderer garment)
    { body=skeleton;outfit=garment;initialized=false; }
    protected override void OnPreRender()
    {
        if(!MorphsReady || !body.IsValid() || !body.Enabled || !outfit.Enabled
            || !body.GameObject.Active || !outfit.GameObject.Active
            || !outfit.SceneObject.IsValid() || !body.TryGetBoneTransform("spine_2",out var chest))
        { initialized=false;return; }
        float dt=Time.Delta;
        if(!float.IsFinite(dt) || dt<=0)return;
        var position=chest.Position;
        if(!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
        { initialized=false;Apply(0);return; }
        if(!initialized || previousModel!=outfit.Model || dt>.1f || (position-previousPosition).Length>32)
        {
            previousModel=outfit.Model;
            initialized=true;previousPosition=position;previousVelocity=Vector3.Zero;
            spring.Current=spring.Target=spring.Velocity=Vector3.Zero;
            Apply(0);return;
        }
        var velocity=(position-previousPosition)/dt;
        var acceleration=(velocity-previousVelocity)/dt;
        previousPosition=position;previousVelocity=velocity;
        spring.Target=Vector3.Up*(-acceleration.z/900).Clamp(-1,1);
        spring.Update(dt);
        Apply(spring.Current.z.Clamp(-1,1));
    }
    private void Apply(float weight)
    {
        if(!float.IsFinite(weight))weight=0;
        CurrentWeight=weight;PeakWeight=MathF.Max(PeakWeight,MathF.Abs(weight));
        // A resting suit needs no override. Avoid repeatedly submitting empty
        // morph work, including the first frame of a newly created renderer.
        if(MathF.Abs(weight)<.0001f)
        {
            if(hasOverride)
                foreach(var name in new[]{"SuitFollowThroughLUp","SuitFollowThroughRUp","SuitFollowThroughLDown","SuitFollowThroughRDown"})
                    outfit.Morphs.Clear(name,0);
            hasOverride=false;appliedWeight=0;return;
        }
        if(hasOverride && MathF.Abs(weight-appliedWeight)<.0001f)return;
        appliedWeight=weight;hasOverride=true;
        var up=MathF.Max(0,weight);var down=MathF.Max(0,-weight);
        outfit.Morphs.Set("SuitFollowThroughLUp",up,0);
        outfit.Morphs.Set("SuitFollowThroughRUp",up,0);
        outfit.Morphs.Set("SuitFollowThroughLDown",down,0);
        outfit.Morphs.Set("SuitFollowThroughRDown",down,0);
    }
    // The owning presentation destroys the garment with this component. Never
    // queue fresh GPU morph work while that render hierarchy is being torn down.
    protected override void OnDestroy() { body=null;outfit=null; }
}
