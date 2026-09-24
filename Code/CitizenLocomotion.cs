using Sandbox;
using Sandbox.Citizen;

namespace PaintballBaddies;

/// <summary>Native animation inputs shared by player and AI presentation.</summary>
public sealed class CitizenLocomotion : Component
{
    public CitizenAnimationHelper Animation { get; private set; }
    public float VaultWeight { get; private set; }
    private SkinnedModelRenderer body;
    private float aimPitch;
    private float previousYaw;
    private bool rotationReady;
    private bool positionReady;
    private Vector3 previousPosition;
    private float stationaryTime;
    private float duckLevel;
    private bool postureReady;
    private float visibleTurn;
    public float AimPitch => aimPitch;
    public float AimYaw { get; private set; }
    public float RotationSpeed => Animation?.MoveRotationSpeed ?? 0;

    public void Configure(SkinnedModelRenderer renderer, CitizenAnimationHelper.HoldTypes hold)
    {
        body = renderer;
        Animation ??= Components.Create<CitizenAnimationHelper>();
        Animation.Target = body;
        Animation.HoldType = hold;
        Animation.IsGrounded = true;
        Animation.IsWeaponLowered = false;
        aimPitch = 0;
        VaultWeight = 0;
        rotationReady=false;
        positionReady=false;stationaryTime=0;postureReady=false;
        visibleTurn=0;
    }

    public void UpdateMotion(Vector3 velocity, Vector3 wishVelocity, bool grounded,
        float duck, float turnSpeed, Rotation lookYaw, float pitch, bool lowerWeapon, float delta, float bodyAimWeight=1, Vector3? rootPosition=null)
    {
        if (!body.IsValid() || !Animation.IsValid()) return;
        if(rootPosition.HasValue)
        {
            var position=rootPosition.Value;
            stationaryTime=positionReady && grounded && (position-previousPosition).WithZ(0).Length<.02f
                ? stationaryTime+delta : 0;
            previousPosition=position;positionReady=true;
            // Allow ordinary network interpolation gaps, but don't keep a run
            // loop alive from stale replicated velocity or a blocked nav agent.
            if(stationaryTime>.18f)
            {
                velocity=velocity.WithX(0).WithY(0);
                wishVelocity=wishVelocity.WithX(0).WithY(0);
            }
        }
        float bodyYaw=body.WorldRotation.Angles().yaw;
        float measuredTurn=rotationReady&&delta>.0001f&&delta<.2f
            ? System.MathF.IEEERemainder(bodyYaw-previousYaw,360)/delta : 0;
        previousYaw=bodyYaw;rotationReady=true;
        // Rapid camera reversals can exceed 1,000 degrees/sec. Feeding that
        // into Citizen's additive turn poses crosses the legs dramatically.
        // Keep a restrained stepping response while the root turns freely.
        // Citizen expects clockwise-positive; engine yaw increases the other
        // way. Match the native controller's sign and half-rate turn response.
        float targetTurn=(turnSpeed!=0 ? turnSpeed : -measuredTurn*.5f).Clamp(-180,180);
        // Filter only the additive stepping pose. Movement, root facing and aim
        // remain immediate; replicated yaw corrections cannot jerk the hips.
        visibleTurn=delta>=.2f ? 0 : visibleTurn+(targetTurn-visibleTurn)*(1-System.MathF.Exp(-16*delta));
        Animation.MoveRotationSpeed=visibleTurn;
        Animation.IsGrounded = grounded;
        Animation.WithVelocity(velocity);
        Animation.WithWishVelocity(wishVelocity);
        // Keep physical clearance immediate; ease only the visible Citizen pose.
        // Player and AI use the same transition, including remote presentations.
        duckLevel=postureReady ? duckLevel+(duck-duckLevel)*(1-System.MathF.Exp(-20*delta)) : duck;
        postureReady=true;
        Animation.DuckLevel = duckLevel;
        Animation.IsWeaponLowered = lowerWeapon;
        aimPitch += (pitch.Clamp(-45,45)-aimPitch)*(1-System.MathF.Exp(-12*delta));
        // Human aim poses have a limited twist range. Targets farther behind
        // require a root/body turn, not a 180-degree spine and pelvis override.
        AimYaw=System.MathF.IEEERemainder(lookYaw.Angles().yaw-bodyYaw,360).Clamp(-45,45);
        Animation.WithLook((Rotation.FromYaw(bodyYaw+AimYaw)*Rotation.FromPitch(aimPitch)).Forward,1,1,bodyAimWeight);
    }

    public void UpdateVault(bool vaulting, float progress, float delta)
    {
        if (!body.IsValid()) return;
        body.UseAnimGraph = true;
        if (vaulting)
        {
            var entry = (progress/.25f).Clamp(0,1);
            VaultWeight = entry*entry*(3-2*entry);
            body.Set("pb_vault_phase", progress);
        }
        else VaultWeight *= System.MathF.Exp(-20*delta);
        body.Set("pb_vault_weight", VaultWeight);
    }
}
