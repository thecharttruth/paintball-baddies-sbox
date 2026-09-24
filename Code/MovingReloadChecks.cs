using Sandbox;
using System;
namespace PaintballBaddies;
/// <summary>Opt-in walking/crouched reload review using actual controller movement.</summary>
public sealed class MovingReloadChecks : Component
{
    [Property] public int CharacterIndex {get;set;}
    [Property] public bool Crouched {get;set;}
    [Property] public bool Running {get;set;}
    [Property] public bool Stationary {get;set;}
    [Property] public string CandidateModelPath {get;set;} = "";
    private PlayerController player;
    private PaintballMarker weapon;
    private float elapsed,maxArmDifference,maxFootDifference;
    private int stage,samples,activeFrames,fullBodyFrames;
    private Vector3 start;
    private Vector3 previousHand;
    private bool hasHand;
    private float maxHandStep,maxStepPhase,maxStepElapsed;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();weapon=player.Components.Get<PaintballMarker>();
        weapon.PoseApplied+=ObserveFinalPose;
        player.UseInputControls=player.UseLookControls=player.UseCameraControls=false;weapon.AcceptInput=false;
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(stage==0)
        {
            player.Components.Get<RosterSelection>().Select(CharacterIndex);player.WorldPosition=new Vector3(-950,-500,2);player.EyeAngles=new Angles(0,90,0);start=player.WorldPosition;stage=1;
            if(!string.IsNullOrEmpty(CandidateModelPath))player.Renderer.Model=Model.Load(CandidateModelPath);
        }
        player.UpdateDucking(Crouched);player.WishVelocity=Vector3.Left*(Stationary ? 0 : Running ? 180 : 55);
        WorldPosition=player.WorldPosition+new Vector3(150,-100,90);WorldRotation=Rotation.LookAt(player.WorldPosition+Vector3.Up*38-WorldPosition);
        if(stage==1 && elapsed>.7f)
        {
            Check("shot prepares reload",weapon.FireAt(player.WorldPosition+Vector3.Left*500+Vector3.Up*70),"shot accepted");
            weapon.Reload();stage=2;
        }
        if(stage==2 && elapsed>1 && elapsed<2.9f)
        {
            if(player.Components.Get<ReloadPresentationTrack>() is {Docked:false})activeFrames++;
            var body=player.Renderer;
            if(body.Sequence.Name=="reload_standing")fullBodyFrames++;
            foreach(var name in new[]{"LeftHand","LeftFoot","RightFoot"})
            {
                var bone=body.Model.Bones.GetBone(name);
                if(!body.TryGetBoneTransformAnimation(bone,out var raw) || !body.TryGetBoneTransform(name,out var final))continue;
                var difference=(raw.Position-final.Position).Length;
                if(name=="LeftHand")maxArmDifference=MathF.Max(maxArmDifference,difference);else maxFootDifference=MathF.Max(maxFootDifference,difference);
            }
            samples++;
        }
        if(elapsed<3.6f)return;
        Check("selected character",player.Renderer.Model?.Name==(string.IsNullOrEmpty(CandidateModelPath) ? RosterSelection.GetModelPath(CharacterIndex) : CandidateModelPath),player.Renderer.Model?.Name);
        Check(Stationary ? "stationary position retained" : "movement continues",Stationary ? (player.WorldPosition-start).WithZ(0).Length<2 : Vector3.Dot(player.WorldPosition-start,Vector3.Left)>140,$"travel={player.WorldPosition-start}");
        Check("locomotion retained",player.Renderer.Sequence.Name==(Stationary ? "idle" : Crouched ? "crouch_walk" : Running ? "run" : "walk"),player.Renderer.Sequence.Name);
        Check("reload props animate",activeFrames>60,$"active_frames={activeFrames}");
        Check("upper body layered",samples>60 && maxArmDifference>2,$"hand_offset_inches={maxArmDifference}, full_body_frames={fullBodyFrames}");
        Check("legs retain animation",maxFootDifference<.1f,$"foot_override_inches={maxFootDifference}");
        Check("ammo conserved",weapon.Ammo==40 && weapon.Reserve==159 && weapon.ReloadRemaining==0,$"ammo={weapon.Ammo},reserve={weapon.Reserve}");
        Check("gear docked after reload",player.Components.Get<ReloadPresentationTrack>() is {Docked:true},"dock state");
        Log.Info($"MOVING_RELOAD OBSERVATION maximum relative hand step inches={maxHandStep} running={Running} reload_phase={maxStepPhase} elapsed={maxStepElapsed}");
        Log.Info("MOVING_RELOAD COMPLETE");Destroy();
    }
    private void Check(string n,bool pass,string detail)=>Log.Info($"MOVING_RELOAD {(pass ? "PASS" : "FAIL")} {n}: {detail}");
    private void ObserveFinalPose(SkinnedModelRenderer body)
    {
        if(elapsed<=.5f || !body.IsValid() || !body.TryGetBoneTransform("LeftHand",out var hand))return;
        var relative=hand.Position-player.WorldPosition;
        if(hasHand && (relative-previousHand).Length>maxHandStep)
        {
            maxHandStep=(relative-previousHand).Length;maxStepPhase=weapon.ReloadProgress;maxStepElapsed=elapsed;
        }
        previousHand=relative;hasHand=true;
    }
    protected override void OnDestroy()
    {
        if(weapon.IsValid())weapon.PoseApplied-=ObserveFinalPose;
        if(!player.IsValid())return;
        player.WishVelocity=Vector3.Zero;player.UpdateDucking(false);player.UseInputControls=player.UseLookControls=player.UseCameraControls=true;weapon.AcceptInput=true;
    }
}
