using Sandbox;
namespace PaintballBaddies;
/// <summary>Temporary candidate recovery interruption fixture.</summary>
public sealed class LandingRecoveryInterrupt : Component
{
    [Property] public string Mode { get; set; } = "move";
    private PlayerController player;
    private bool actionAccepted;
    private bool requested;
    private bool releasedAim;
    private int landingFrames;
    private bool? previousAim;
    private float elapsed;
    private int frames;
    private Vector3 start;
    protected override void OnUpdate()
    {
        player ??= Scene.GetAllComponents<PlayerController>().FirstOrDefault();
        if ( player is null ) return;
        var body = player.Renderer;
        if(!releasedAim && player.Components.Get<VaultController>()?.IsVaulting==true)
        {
            var heldWeapon=player.Components.Get<PaintballMarker>();
            previousAim=heldWeapon.ReviewAimOverride;
            heldWeapon.ReviewAimOverride=false;
            releasedAim=true;
        }
        if(releasedAim && player.Components.Get<VaultController>()?.Outcome=="Landed" && landingFrames++<20)
            Log.Info($"LANDING_STATE clip={body.Sequence.Name} time={body.Sequence.Time} model={body.Model?.Name} duck={player.IsDucking} wish={player.WishVelocity} aim={player.Components.Get<PaintballMarker>().Aiming} reload={player.Components.Get<PaintballMarker>().ReloadRemaining}");
        if ( !requested )
        {
            if ( body.Sequence.Name != "landing_recovery" || body.Sequence.TimeNormalized < .15f ) return;
            requested = true; start = player.WorldPosition;
            var weapon = player.Components.Get<PaintballMarker>();
            if ( Mode == "aim" ) weapon.ReviewAimOverride = true;
            if ( Mode == "reload" )
            {
                actionAccepted = weapon.Ammo < 40;
                weapon.Reload();
            }
        }
        elapsed += Time.Delta; frames++;
        if ( Mode == "move" ) player.WishVelocity = Vector3.Forward * 55;
        if ( frames == 3 ) Log.Info($"LANDING_INTERRUPT {(body.Sequence.Name != "landing_recovery" ? "PASS" : "FAIL")} {Mode} cancels recovery: clip={body.Sequence.Name} elapsed={elapsed}");
        if ( elapsed < .22f ) return;
        var marker = player.Components.Get<PaintballMarker>();
        var accepted = Mode == "aim" ? marker.Aiming
            : Mode == "reload" ? actionAccepted && marker.ReloadRemaining > 0
            : (player.WorldPosition-start).WithZ(0).Length > 3;
        Log.Info($"LANDING_INTERRUPT {(accepted ? "PASS" : "FAIL")} {Mode} action active: travel={(player.WorldPosition-start).WithZ(0).Length} reload={marker.ReloadRemaining} aiming={marker.Aiming}");
        Log.Info($"LANDING_INTERRUPT {(body.LocalPosition == player.Components.Get<VaultController>().VisualBasePosition ? "PASS" : "FAIL")} visual offset restored");
        Destroy();
    }
    protected override void OnDestroy()
    {
        if ( player.IsValid() ) { player.WishVelocity = Vector3.Zero; player.Components.Get<PaintballMarker>().ReviewAimOverride = previousAim; }
    }
}
