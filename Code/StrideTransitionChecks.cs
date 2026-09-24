using Sandbox;
using System;
namespace PaintballBaddies;
public sealed class StrideTransitionChecks : Component
{
    [Property] public int CharacterIndex {get;set;}
    private PlayerController player;
    private PaintballMarker marker;
    private bool input,look,accept,selected;
    private float elapsed,phase,maxError;
    private string clip;
    private int changes,walkToRun,runToWalk;
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First(); marker=player.Components.Get<PaintballMarker>();
        input=player.UseInputControls;look=player.UseLookControls;accept=marker.AcceptInput;
        player.UseInputControls=player.UseLookControls=marker.AcceptInput=false;
        player.WorldPosition=new Vector3(-950,-700,2);player.EyeAngles=new Angles(0,90,0);
        marker.PoseApplied+=Observe;
    }
    private void Observe(SkinnedModelRenderer body)
    {
        var next=body.Sequence.Name;var p=body.Sequence.TimeNormalized;
        if((clip=="walk"&&next=="run")||(clip=="run"&&next=="walk"))
        {
            var expected=(phase+(next=="run"?.875f:.125f))%1f;
            var error=MathF.Abs(p-expected);error=MathF.Min(error,1-error);maxError=MathF.Max(maxError,error);
            changes++;if(next=="run")walkToRun++;else runToWalk++;
            Log.Info($"STRIDE_TRANSITION OBSERVATION {clip}->{next} prior={phase} next={p} error={error}");
        }
        clip=next;phase=p;
    }
    protected override void OnUpdate()
    {
        if(!selected){player.Components.Get<RosterSelection>().Select(CharacterIndex);selected=true;}
        elapsed+=Time.Delta;
        player.WishVelocity=elapsed<6 ? Vector3.Left*((int)(elapsed/.73f)%2==0?85:180) : Vector3.Zero;
        if(elapsed<6.6f)return;
        Check("transitions exercised",changes>=6,$"changes={changes}");
        Check("both directions",walkToRun>=3&&runToWalk>=3,$"up={walkToRun} down={runToWalk}");
        Check("stride phase retained",maxError<.1f,$"max_circular_error={maxError}");
        Check("idle recovery",player.Renderer.Sequence.Name=="idle",player.Renderer.Sequence.Name);
        Log.Info("STRIDE_TRANSITION COMPLETE");Destroy();
    }
    private void Check(string name,bool pass,string detail)=>Log.Info($"STRIDE_TRANSITION {(pass?"PASS":"FAIL")} {name}: {detail}");
    protected override void OnDestroy()
    {
        if(marker.IsValid()){marker.PoseApplied-=Observe;marker.AcceptInput=accept;}
        if(player.IsValid()){player.WishVelocity=Vector3.Zero;player.UseInputControls=input;player.UseLookControls=look;}
    }
}
