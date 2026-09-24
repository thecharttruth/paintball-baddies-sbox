using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Opt-in full-body camera and actual-controller run review.</summary>
public sealed class RunMotionChecks : Component
{
    [Property] public bool ReviewFootsteps { get; set; }
    [Property] public int CharacterIndex { get; set; }
    private PlayerController player;
    private PaintballMarker weapon;
    private float elapsed,maxTime,maxLowerAnkle,minSpeed=10000;
    private int samples;
    private Vector3 start;
    private bool input,look,camera,accept,selected,runOnly=true;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();
        weapon=player.Components.Get<PaintballMarker>();
        input=player.UseInputControls;look=player.UseLookControls;camera=player.UseCameraControls;accept=weapon.AcceptInput;
        player.UseInputControls=player.UseLookControls=player.UseCameraControls=false;weapon.AcceptInput=false;
        player.WorldPosition=new Vector3(-950,-600,2);player.EyeAngles=new Angles(0,90,0);start=player.WorldPosition;
    }
    protected override void OnUpdate()
    {
        // RosterSelection initializes in OnStart; choose the review character afterward.
        if(!selected){player.Components.Get<RosterSelection>().Select(CharacterIndex);selected=true;}
        if(player.Components.Get<MovementFootsteps>() is { } steps)steps.ReviewContacts=ReviewFootsteps;
        elapsed+=Time.Delta;
        player.WishVelocity=elapsed<3 ? Vector3.Left*180 : Vector3.Zero;
        WorldPosition=player.WorldPosition+new Vector3(170,-100,85);
        WorldRotation=Rotation.LookAt(player.WorldPosition+Vector3.Up*36-WorldPosition);
        if(elapsed>.8f && elapsed<2.9f)
        {
            minSpeed=MathF.Min(minSpeed,player.Velocity.WithZ(0).Length);
            runOnly &= player.Renderer.Sequence.Name=="run";
            maxTime=MathF.Max(maxTime,player.Renderer.Sequence.Time);
            if(player.Renderer.TryGetBoneTransform("LeftFoot",out var l) && player.Renderer.TryGetBoneTransform("RightFoot",out var r))
            {
                if(ReviewFootsteps)Log.Info($"FOOT_PHASE phase={player.Renderer.Sequence.TimeNormalized} left={l.Position.z-player.WorldPosition.z} right={r.Position.z-player.WorldPosition.z}");
                maxLowerAnkle=MathF.Max(maxLowerAnkle,MathF.Min(l.Position.z,r.Position.z)-player.WorldPosition.z);samples++;
            }
        }
        if(elapsed<4.5f)return;
        Check("forward travel",Vector3.Dot(player.WorldPosition-start,Vector3.Left)>400,$"travel={player.WorldPosition-start}");
        Check("sustained speed",minSpeed>150,$"minimum={minSpeed}");
        Check("cycle advances",runOnly && maxTime>.35f,$"run_only={runOnly},time={maxTime}");
        Check("selected character",player.Renderer.Model?.Name==RosterSelection.GetModelPath(CharacterIndex),player.Renderer.Model?.Name);
        Check("ankle samples",samples>60,$"samples={samples}");
        // Ankle origins sit above boot soles; this catches excessive whole-body flight,
        // but cannot establish sole contact or horizontal foot locking.
        Check("bounded lower ankle",maxLowerAnkle<12,$"maximum_inches={maxLowerAnkle}");
        Check("idle recovery",player.Renderer.Sequence.Name=="idle",player.Renderer.Sequence.Name);
        Log.Info("RUN_MOTION COMPLETE");Destroy();
    }
    private void Check(string name,bool pass,string detail)=>Log.Info($"RUN_MOTION {(pass ? "PASS" : "FAIL")} {name}: {detail}");
    protected override void OnDestroy()
    {
        if(player.IsValid()){player.WishVelocity=Vector3.Zero;player.UseInputControls=input;player.UseLookControls=look;player.UseCameraControls=camera;}
        if(weapon.IsValid())weapon.AcceptInput=accept;
    }
}
