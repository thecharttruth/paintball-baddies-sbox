using Sandbox;
using System;
namespace PaintballBaddies;
/// <summary>Opt-in comparison against the native imported reload animation.</summary>
public sealed class ReloadUpperBodyChecks : Component
{
    [Property] public int CharacterIndex {get;set;}
    private PlayerController player;
    private float elapsed,maxPosition,maxAngle;
    private int samples;
    private bool selected;
    protected override void OnUpdate()
    {
        player ??= Scene.GetAllComponents<PlayerController>().First();
        if(!selected){player.Components.Get<RosterSelection>().Select(CharacterIndex);selected=true;}
        elapsed+=Time.Delta;player.UseInputControls=false;player.UseLookControls=false;
        player.Components.Get<ViperAvatar>().PreviewSequence="reload_standing";
        player.Renderer.PlaybackRate=0;player.Renderer.Sequence.TimeNormalized=(elapsed/3).Clamp(0,1);
    }
    protected override void OnPreRender()
    {
        if(!player.IsValid() || elapsed<.15f)return;
        var body=player.Renderer;var track=ReloadUpperBodyData.Load(RosterSelection.Names[CharacterIndex].ToLowerInvariant());
        for(int i=0;i<track.names.Length;i++)
        {
            var bone=body.Model.Bones.GetBone(track.names[i]);
            if(!body.TryGetBoneTransformAnimation(bone,out var native))continue;
            var actual=body.WorldTransform.ToLocal(native);var expected=ReloadUpperBodyData.Sample(track,body.Model,i,body.Sequence.TimeNormalized);
            maxPosition=MathF.Max(maxPosition,(actual.Position-expected.Position).Length);
            maxAngle=MathF.Max(maxAngle,actual.Rotation.Distance(expected.Rotation));samples++;
        }
        if(elapsed<3)return;
        Log.Info($"RELOAD_UPPER {(samples>500 && maxPosition<.15f && maxAngle<1 ? "PASS" : "FAIL")} samples={samples} position_inches={maxPosition} angle_degrees={maxAngle}");
        Destroy();
    }
    protected override void OnDestroy()
    {
        if(!player.IsValid())return;
        player.UseInputControls=player.UseLookControls=true;player.Components.Get<ViperAvatar>().PreviewSequence="";
    }
}
