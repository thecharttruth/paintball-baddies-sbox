using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Opt-in actual movement across the aim-relative diagonal boundary.</summary>
public sealed class DiagonalAimChecks : Component
{
    private PlayerController player;
    private PaintballMarker weapon;
    private float elapsed,maxTime;
    private int samples,wrongClip;
    private bool input,look,accept,forwardRecovered;
    protected override void OnStart()
    {
        player=Components.Get<PlayerController>();weapon=Components.Get<PaintballMarker>();
        input=player.UseInputControls;look=player.UseLookControls;accept=weapon.AcceptInput;
        player.UseInputControls=player.UseLookControls=false;weapon.AcceptInput=false;weapon.ReviewAimOverride=true;
        WorldPosition=new Vector3(-950,-300,2);player.EyeAngles=new Angles(0,0,0);
    }
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        var sideways=55+((int)(elapsed*10)%2==0 ? 2 : -2);
        player.WishVelocity=elapsed<1 ? Vector3.Left*55 : elapsed<4 ? new Vector3(55,sideways,0) : elapsed<5.5f ? Vector3.Forward*55 : Vector3.Zero;
        if(elapsed>1.3f && elapsed<3.9f)
        {
            samples++;
            if(player.Renderer.Sequence.Name!="walk_left")wrongClip++;
            maxTime=MathF.Max(maxTime,player.Renderer.Sequence.Time);
        }
        if(elapsed>4.5f && elapsed<5.4f && player.Renderer.Sequence.Name=="walk")forwardRecovered=true;
        if(elapsed<6.8f)return;
        Check("diagonal remains lateral",samples>60 && wrongClip==0,$"samples={samples},unexpected_clip_frames={wrongClip}");
        Check("cycle keeps advancing",maxTime>.8f,$"max_time={maxTime}");
        Check("forward input leaves strafe",forwardRecovered,$"forward_recovered={forwardRecovered}");
        Check("returns to idle",player.Renderer.Sequence.Name=="idle",player.Renderer.Sequence.Name);
        Log.Info("DIAGONAL_AIM COMPLETE");Destroy();
    }
    private void Check(string name,bool pass,string detail)=>Log.Info($"DIAGONAL_AIM {(pass ? "PASS" : "FAIL")} {name}: {detail}");
    protected override void OnDestroy()
    {
        if(player.IsValid()){player.WishVelocity=Vector3.Zero;player.UseInputControls=input;player.UseLookControls=look;}
        if(weapon.IsValid()){weapon.AcceptInput=accept;weapon.ReviewAimOverride=null;}
    }
}
