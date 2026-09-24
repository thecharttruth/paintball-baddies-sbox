using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Delayed forward running through the existing native run action.</summary>
public sealed class ForwardAutoRun : Component
{
    public const float Delay=1.25f;
    [Property] public float RunSpeed { get; set; }=210f;
    public bool Automatic { get; private set; }
    public float HeldSeconds { get; private set; }
    public bool Running => Automatic && HeldSeconds>=Delay;
    private PlayerController player;
    private IDisposable fixedHook,updateHook;
    protected override void OnStart()
    {
        player=Components.Get<PlayerController>();
        if(IsProxy)return;
        Automatic=Game.Cookies.Get("paintball-auto-run",true);
        fixedHook=Scene.AddHook(GameObjectSystem.Stage.StartFixedUpdate,-90,Tick,nameof(ForwardAutoRun),"Forward hold timer");
        updateHook=Scene.AddHook(GameObjectSystem.Stage.StartUpdate,-90,Apply,nameof(ForwardAutoRun),"Native automatic run action");
    }
    public void ToggleAutomatic()
    {
        Automatic=!Automatic;HeldSeconds=0;
        Game.Cookies.Set("paintball-auto-run",Automatic);
    }
    private bool Eligible => !IsProxy && Enabled && GameObject.Active && Automatic && player.IsValid() && player.UseInputControls
        && !Input.UsingController && Input.AnalogMove.x>.5f && !Input.Down("backward")
        && !Input.Down("duck") && !Input.Down("walk")
        && Components.Get<CoverController>()?.Sliding!=true
        && Components.Get<CoverController>()?.Attached!=true
        && Components.Get<VaultController>()?.IsVaulting!=true
        && Scene.GetAllComponents<PaintballControls>().FirstOrDefault()?.Open!=true;
    private void Tick()
    {
        HeldSeconds=Eligible ? MathF.Min(Delay,HeldSeconds+Time.Delta) : 0;
        Apply();
    }
    private void Apply()
    {
        if(player.IsValid())player.RunSpeed=RunSpeed;
        if(!Eligible)HeldSeconds=0;
        // Keyboard sprint comes only from the forward-hold gesture. Ignore old
        // Shift/custom run bindings while preserving native gamepad controls.
        if(!Input.UsingController)Input.SetAction("run",Running);
    }
    protected override void OnDisabled()=>HeldSeconds=0;
    protected override void OnDestroy(){fixedHook?.Dispose();updateHook?.Dispose();}
}
