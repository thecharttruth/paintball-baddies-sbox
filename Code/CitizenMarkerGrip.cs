using Sandbox;
using Sandbox.Citizen;

namespace PaintballBaddies;

/// <summary>Paintball marker carry and support-hand recovery on the native human rig.</summary>
public sealed class CitizenMarkerGrip : Component
{
    // Shared native hold_R fit: gloved index at the VX-9 trigger guard.
    public static Vector3 MarkerOffset => new(0,0,-3f);
    public float Grip { get; private set; } = 1;
    [Property] public float RightGripStrength { get; set; } = 1;
    public Rotation SupportRotation { get; private set; }
    public bool Returning { get; private set; }
    private bool rotationInitialized;
    private Transform returnStart;

    public void UpdateGrip(SkinnedModelRenderer body, float strength, bool release, float delta)
    {
        strength = strength.Clamp(0,1);
        Grip += ((release ? 0 : strength)-Grip)*(1-System.MathF.Exp(-20*delta));
        body.Set("pb_grip_weight", strength * RightGripStrength.Clamp(0,1));
        body.Set("pb_left_grip", Grip);
    }

    public bool Attach(SkinnedModelRenderer body, GameObject marker, Vector3 offset, Angles angles)
    {
        if (!marker.IsValid() || !body.IsValid()) return false;
        // Let the engine carry the marker with the final animated bone, including
        // IK updates after OnUpdate. Copying its world pose here lagged the wrist.
        body.CreateBoneObjects = true;
        var hold = body.GetBoneObject("hold_R");
        if (!hold.IsValid()) return false;
        if (marker.Parent != hold) marker.Parent = hold;
        marker.LocalPosition = offset;
        marker.LocalRotation = angles.ToRotation();
        return true;
    }

    public void UpdateSupport(SkinnedModelRenderer body, CitizenAnimationHelper animation,
        GameObject marker, GameObject target, Vector3 offset, bool release, float recovery)
    {
        if (release) { animation.IkLeftHand = null; return; }
        if (!target.IsValid() || !body.TryGetBoneTransform("hand_L", out var left)) return;
        if (!rotationInitialized)
        {
            SupportRotation = marker.WorldRotation.Inverse*left.Rotation;
            rotationInitialized = true;
        }
        target.WorldPosition = marker.WorldPosition+marker.WorldRotation*offset;
        target.WorldRotation = marker.WorldRotation*SupportRotation;
        if (recovery >= 0)
        {
            if (!Returning) { returnStart = GameObject.WorldTransform.ToLocal(left); Returning = true; }
            var progress = recovery.Clamp(0,1);
            var blend = progress*progress*(3-2*progress);
            target.WorldPosition = Vector3.Lerp(GameObject.WorldTransform.PointToWorld(returnStart.Position), target.WorldPosition, blend);
            target.WorldRotation = Rotation.Slerp(GameObject.WorldRotation*returnStart.Rotation, target.WorldRotation, blend);
        }
        animation.IkLeftHand = target;
    }

    public void ResetRecovery() => Returning = false;
}
