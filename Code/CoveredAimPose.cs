using Sandbox;

namespace PaintballBaddies;

/// <summary>Applies pitch to an authored two-handed pose and its marker as one assembly.</summary>
public static class CoveredAimPose
{
    public static bool Apply( SkinnedModelRenderer body, float degrees, Vector3 markerOffset, out Transform markerPose, bool writeBones = true )
    {
        markerPose = default;
        if ( !body.IsValid() || !body.Model.IsValid() ) return false;
        var spine = body.Model.Bones.GetBone( "Spine" );
        var handBone = body.Model.Bones.GetBone( "RightHand" );
        if ( spine is null || handBone is null
            || !body.TryGetBoneTransformAnimation( spine, out var pivot )
            || !body.TryGetBoneTransformAnimation( handBone, out var hand ) ) return false;
        var pitch = body.WorldRotation * Rotation.FromPitch( degrees.Clamp( -45, 45 ) ) * body.WorldRotation.Inverse;
        if ( writeBones ) foreach ( var bone in body.Model.Bones.AllBones )
        {
            var ancestor = bone;
            while ( ancestor.Name != "Spine" && ancestor.Parent is not null ) ancestor = ancestor.Parent;
            if ( ancestor.Name != "Spine" || !body.TryGetBoneTransformAnimation( bone, out var pose ) ) continue;
            pose.Position = pivot.Position + pitch * (pose.Position - pivot.Position);
            pose.Rotation = pitch * pose.Rotation;
            body.SetBoneTransform( bone, body.WorldTransform.ToLocal( pose ) );
        }
        var origin = hand.Position + body.WorldRotation * markerOffset;
        markerPose = new Transform( pivot.Position + pitch * (origin - pivot.Position), pitch * body.WorldRotation );
        return true;
    }
}
