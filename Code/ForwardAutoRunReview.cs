using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Feeds mapped actions before the production auto-run/native movement hooks.</summary>
public sealed class ForwardAutoRunReview : Component
{
    private PlayerController player;
    private ForwardAutoRun runner;
    private PaintballMarker weapon;
    private int initialShots;
    private GameObject runway;
    private IDisposable updateHook,fixedHook;
    private float elapsed;
    private int phase;
    private bool initialized,originalAutomatic;
    protected override void OnUpdate()
    {
        if(initialized)return;
        player=Scene.GetAllComponents<PlayerController>().FirstOrDefault();
        runner=player?.Components.Get<ForwardAutoRun>();
        if(!runner.IsValid())return;
        initialized=true;originalAutomatic=runner.Automatic;
        weapon=player.Components.Get<PaintballMarker>();initialShots=weapon.Shots;
        if(!runner.Automatic)runner.ToggleAutomatic();
        // Keep native physics, but isolate the movement test from new arena cover props.
        runway=TrainingRange.Box(GameObject,"Focus run review floor",new Vector3(0,1250,1500),new Vector3(3200,600,20),Color.Gray,true);
        player.WorldPosition=new Vector3(-900,1250,1512);player.EyeAngles=Angles.Zero;player.UseLookControls=false;
        weapon.AcceptInput=true;
        updateHook=Scene.AddHook(GameObjectSystem.Stage.StartUpdate,-95,Inputs,nameof(ForwardAutoRunReview),"Review actions");
        fixedHook=Scene.AddHook(GameObjectSystem.Stage.StartFixedUpdate,-95,()=>{elapsed+=Time.Delta;Inputs();},nameof(ForwardAutoRunReview),"Review actions");
    }
    private void Inputs()
    {
        var forward=elapsed<2.2f || (elapsed>=2.7f && elapsed<7.1f);
        Input.SetAction("forward",forward);Input.SetAction("backward",false);
        Input.SetAction("run",elapsed>=5.8f && elapsed<6.6f);
        Input.SetAction("attack2",elapsed>=2 && elapsed<7.1f);
        Input.SetAction("attack1",elapsed>=6.6f && elapsed<7f);
        Input.SetAction("duck",elapsed>=4.4f && elapsed<4.7f);
        Input.SetAction("walk",elapsed>=4.9f && elapsed<5.2f);
        Input.AnalogMove=forward ? Vector3.Forward : Vector3.Zero;
    }
    private void Check(string name,bool pass)=>Log.Info($"AUTO_RUN_REVIEW {(pass ? "PASS" : "FAIL")} {name} speed={player.Body.Velocity.WithZ(0).Length:0.0} held={runner.HeldSeconds:0.00}");
    protected override void OnFixedUpdate()
    {
        if(!initialized)return;
        if(phase==0 && elapsed>.8f){Check("starts walking",!runner.Running && player.Body.Velocity.WithZ(0).Length>60 && player.Body.Velocity.WithZ(0).Length<105);phase++;}
        if(phase==1 && elapsed>1.8f){Check("held forward reaches native run speed",runner.Running && Input.Down("run") && player.Body.Velocity.WithZ(0).Length>150);phase++;}
        if(phase==2 && elapsed>2.1f){Check("entering focus preserves automatic run",runner.Running && Input.Down("run") && weapon.Aiming && player.Body.Velocity.WithZ(0).Length>150);phase++;}
        if(phase==3 && elapsed>2.5f){Check("release clears timer",runner.HeldSeconds==0 && !Input.Down("run"));phase++;}
        if(phase==4 && elapsed>3.2f){Check("holding forward while focused starts with walk delay",!runner.Running && !Input.Down("run") && weapon.Aiming);phase++;}
        if(phase==5 && elapsed>4.2f){Check("sustained forward enters run while focused",runner.Running && Input.Down("run") && weapon.Aiming && player.Body.Velocity.WithZ(0).Length>150);phase++;}
        if(phase==6 && elapsed>4.5f){Check("crouch resets timer",runner.HeldSeconds==0 && !Input.Down("run"));phase++;}
        if(phase==7 && elapsed>5){Check("walk override resets timer",runner.HeldSeconds==0 && !Input.Down("run"));phase++;}
        if(phase==8 && elapsed>5.9f){Check("old manual run cannot bypass forward delay",!Input.Down("run") && !runner.Running && weapon.Aiming && player.Body.Velocity.WithZ(0).Length<105);phase++;}
        if(phase==9 && elapsed>6.9f){Check("forward-held focused running permits native marker fire",weapon.Shots>initialShots && weapon.Aiming && Input.Down("run") && player.Body.Velocity.WithZ(0).Length>150);phase++;}
    }
    protected override void OnDestroy()
    {
        updateHook?.Dispose();fixedHook?.Dispose();
        if(runway.IsValid())runway.Destroy();
        if(runner.IsValid() && runner.Automatic!=originalAutomatic)runner.ToggleAutomatic();
        if(player.IsValid()){player.UseLookControls=true;player.Components.Get<PaintballMarker>().AcceptInput=true;}
    }
}
