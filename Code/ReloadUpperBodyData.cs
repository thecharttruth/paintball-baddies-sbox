using Sandbox;
using System;
using System.Collections.Generic;
namespace PaintballBaddies;

/// <summary>Offline sampled reload transforms; rotations preserve native bind axes.</summary>
public static class ReloadUpperBodyData
{
    public sealed class BonePose { public float[] position {get;set;} public float[] rotationDelta {get;set;} }
    public sealed class Frame { public BonePose[] bones {get;set;} }
    public sealed class Track { public int version {get;set;} public float duration {get;set;} public string[] names {get;set;} public Frame[] frames {get;set;} }
    private static readonly Dictionary<string,Track> cache=new();
    public static float BlendWeight(float phase)
    {
        var weight=MathF.Min((phase/.08f).Clamp(0,1),((1-phase)/.08f).Clamp(0,1));
        return weight*weight*(3-2*weight);
    }
    public static Vector3 MarkerOffset(string modelPath,Vector3 carryOffset,float phase)
    {
        var authored=CoveredRosterAssets.CharacterIndex(modelPath)==2 ? new Vector3(4.92126f,-.984252f,-3.740157f) : carryOffset;
        return Vector3.Lerp(carryOffset,authored,BlendWeight(phase));
    }
    internal static string ImaniReviewTrack {get;set;}
    public static Track Load(string character)
    {
        var path=character=="imani" ? (string.IsNullOrEmpty(ImaniReviewTrack) ? "animations/imani_reload_reach.json" : ImaniReviewTrack) : $"animations/{character}_reload_upper_body.json";
        if(!cache.TryGetValue(path,out var track))
        {
            track=Json.Deserialize<Track>(FileSystem.Mounted.ReadAllText(path));
            cache[path]=track;
        }
        return track;
    }
    public static Transform Sample(Track track,Model model,int boneIndex,float phase)
    {
        var sample=phase.Clamp(0,1)*(track.frames.Length-1);var i=(int)sample;var t=sample-i;
        var a=track.frames[i].bones[boneIndex];var b=track.frames[Math.Min(i+1,track.frames.Length-1)].bones[boneIndex];
        var p=Vector3.Lerp(new(a.position[0],a.position[1],a.position[2]),new(b.position[0],b.position[1],b.position[2]),t);
        var qa=new Rotation(a.rotationDelta[0],a.rotationDelta[1],a.rotationDelta[2],a.rotationDelta[3]);
        var qb=new Rotation(b.rotationDelta[0],b.rotationDelta[1],b.rotationDelta[2],b.rotationDelta[3]);
        return new Transform(p,Rotation.Slerp(qa,qb,t)*model.GetBoneTransform(track.names[boneIndex]).Rotation);
    }
    public static bool Apply(SkinnedModelRenderer body,float phase,Vector3 markerOffset,out Transform markerPose,out Vector3 bodyOffset,bool writeBones=true)
    {
        markerPose=default;bodyOffset=default;
        var index=CoveredRosterAssets.CharacterIndex(body.Model?.Name);
        if(index<0)return false;
        var track=Load(RosterSelection.Names[index].ToLowerInvariant());
        var spineIndex=Array.IndexOf(track.names,"Spine");
        var spine=body.Model.Bones.GetBone("Spine");
        if(spineIndex<0 || !body.TryGetBoneTransformAnimation(spine,out var nativeSpine))return false;
        bodyOffset=body.WorldTransform.PointToLocal(nativeSpine.Position)-Sample(track,body.Model,spineIndex,phase).Position;
        var weight=BlendWeight(phase);
        // Imani's reload prop track uses the original marker mount. Blend that
        // authored mount with the calibrated carry mount over the same pose fade.
        var blendedMarkerOffset=MarkerOffset(body.Model?.Name,markerOffset,phase);
        for(int i=0;i<track.names.Length;i++)
        {
            var bone=body.Model.Bones.GetBone(track.names[i]);
            if(!body.TryGetBoneTransformAnimation(bone,out var native))continue;
            var current=body.WorldTransform.ToLocal(native);var target=Sample(track,body.Model,i,phase);
            var pose=new Transform(Vector3.Lerp(current.Position,target.Position+bodyOffset,weight),Rotation.Slerp(current.Rotation,target.Rotation,weight));
            if(writeBones)body.SetBoneTransform(bone,pose);
            if(track.names[i]=="RightHand")markerPose=new Transform(body.WorldTransform.PointToWorld(pose.Position+blendedMarkerOffset),body.WorldRotation);
        }
        return true;
    }
}
