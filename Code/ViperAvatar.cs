using Sandbox;
using System;

namespace PaintballBaddies;

/// <summary>Retargeted locomotion driven by actual movement and collider posture.</summary>
public sealed class ViperAvatar : Component
{
    [Property] public string PreviewSequence { get; set; } = "";
    private static bool UsesMeiLanding(string path) => path is "models/characters/mei_planted_review/mei_protected.vmdl" or "models/characters/mei_protected_morph/mei_protected.vmdl";
    private PlayerController controller;
    private float playbackRate = 1;
    private float smoothedSpeed;
    private float candidateLandingGrace;
    private bool candidateWasVaulting;
    private float landingRecovery = -1;
    protected override void OnStart()
    {
        controller = Components.Get<PlayerController>();
        if(Components.Get<MovementFootsteps>() is null) Components.Create<MovementFootsteps>();
        if ( controller?.Renderer is not { } renderer ) return;
        controller.UseAnimatorControls = false;
        renderer.UseAnimGraph = false;
        renderer.Sequence.Name = "idle";
        renderer.Sequence.Looping = true;
        renderer.Sequence.Blending = true;
    }

    protected override void OnUpdate()
    {
        if ( controller?.Renderer is not { } renderer ) return;
        if (Components.Get<CitizenPlayerPresentation>()?.IsReady == true)
        {
            candidateWasVaulting=false;candidateLandingGrace=0;landingRecovery=-1;
            smoothedSpeed=0;
            return;
        }
        var vault = Components.Get<VaultController>();
        if ( vault?.IsVaulting == true )
        {
            if ( UsesMeiLanding(renderer.Model?.Name) ) { candidateLandingGrace = .12f; candidateWasVaulting = true; landingRecovery = -1; }
            if ( renderer.Sequence.Name != "vault" ) renderer.Sequence.Name = "vault";
            renderer.Sequence.Looping = false; renderer.PlaybackRate = 0;
            if ( UsesMeiLanding(renderer.Model?.Name) ) renderer.Sequence.TimeNormalized = vault.Progress;
            else renderer.Sequence.Time = vault.Progress * .8f;
            var groundOffset = CoveredVaultGroundProfiles.TrySample( renderer.Model?.Name, vault.Progress, out var coveredOffset )
                ? coveredOffset : VaultGroundProfiles.Sample( Components.Get<RosterSelection>()?.Selected ?? 0, vault.Progress );
            renderer.LocalPosition = vault.VisualBasePosition + Vector3.Down * groundOffset;
            renderer.WorldRotation = vault.Facing;
            return;
        }
        if ( candidateWasVaulting )
        {
            candidateWasVaulting = false;
            if ( vault?.Outcome == "Landed" ) landingRecovery = 0;
        }
        if ( landingRecovery >= 0 )
        {
            var marker = Components.Get<PaintballMarker>();
            var interrupt = controller.WishVelocity.WithZ(0).Length > 1 || controller.IsDucking
                || marker?.ReloadRemaining > 0 || marker?.Aiming == true
                || (marker?.AcceptInput == true && Input.Down("attack1"))
                || Components.Get<CoverController>()?.Attached == true;
            if ( !interrupt && landingRecovery < .3f && UsesMeiLanding(renderer.Model?.Name) )
            {
                if ( renderer.Sequence.Name != "landing_recovery" ) renderer.Sequence.Name = "landing_recovery";
                renderer.Sequence.Blending = false; renderer.Sequence.Looping = false; renderer.PlaybackRate = 0;
                var p = landingRecovery / .3f;
                renderer.Sequence.Time = landingRecovery;
                renderer.LocalPosition = vault.VisualBasePosition + Vector3.Down * MeiLandingRecoveryGround.Sample(p);
                landingRecovery += Time.Delta; smoothedSpeed = 0;
                return;
            }
            landingRecovery = -1;
            renderer.LocalPosition = vault.VisualBasePosition;
            renderer.Sequence.Blending = true;
        }
        var velocity = controller.Velocity.WithZ( 0 );
        var speed = velocity.Length;
        if ( candidateLandingGrace > 0 )
        {
            candidateLandingGrace -= Time.Delta;
            if ( controller.WishVelocity.WithZ(0).Length < 1 ) { speed = 0; smoothedSpeed = 0; }
        }
        smoothedSpeed += (speed - smoothedSpeed) * (1 - MathF.Exp( -16 * Time.Delta ));
        // Slight hysteresis prevents a sequence restart around a speed boundary.
        var moving = smoothedSpeed > (renderer.Sequence.Name == "idle" || renderer.Sequence.Name == "crouch" ? 9 : 5);
        var running = smoothedSpeed > (renderer.Sequence.Name == "run" ? 120 : 135);
        var clip = controller.IsDucking ? (moving ? "crouch_walk" : "crouch")
            : running ? "run" : moving ? "walk" : "idle";
        var cover = Components.Get<CoverController>();
        var weapon = Components.Get<PaintballMarker>();
        var roster = Components.Get<RosterSelection>();
        var combatClips = roster is not null;
        var motionScale = roster?.MotionScale ?? 1;
        var facing = cover?.Attached == true && !cover.Peeking
            ? Rotation.LookAt( -cover.Normal ) : Rotation.FromYaw( controller.EyeAngles.yaw );
        var localVelocity = facing.Inverse * velocity;
        if ( combatClips )
        {
            var facingTarget = cover?.Attached == true || weapon?.Aiming == true;
            if ( moving && facingTarget )
            {
                var wasLateral = renderer.Sequence.Name is "walk_left" or "walk_right" or "crouch_left" or "crouch_right";
                // A small angular dead band prevents alternating clips at diagonal input.
                var lateral = MathF.Abs(localVelocity.y) > MathF.Abs(localVelocity.x) * (wasLateral ? .9f : 1.1f);
                if ( controller.IsDucking && lateral )
                    clip = localVelocity.y > 0 ? "crouch_left" : "crouch_right";
                else if ( !controller.IsDucking && renderer.Model?.Name == RosterSelection.GetModelPath( roster.Selected )
                    && lateral )
                    clip = localVelocity.y > 0 ? "walk_left" : "walk_right";
                else if ( !controller.IsDucking && localVelocity.x < -MathF.Abs( localVelocity.y ) ) clip = "walk_backward";
            }
            // Covered characters keep their locomotion underneath the reload layer,
            // including idle, so entering reload does not replace the whole pose.
            if ( !moving && !controller.IsDucking && weapon?.ReloadRemaining > 0
                && CoveredRosterAssets.ReloadTrack(renderer.Model?.Name) is null ) clip = "reload_standing";
        }
        if ( !string.IsNullOrEmpty( PreviewSequence ) ) clip = PreviewSequence;
        // Assigning Name restarts the native sequence, even when unchanged.
        // Change it only at a state transition so the gait can advance.
        if ( renderer.Sequence.Name != clip )
        {
            CoveredStrideTransition.SetSequence(renderer, clip);
            if ( clip == "reload_standing" ) renderer.Sequence.Time = MathF.Max( 0, (weapon.ActiveReloadDuration - weapon.ReloadRemaining) * 2 );
        }
        var targetRate = clip == "walk" ? (smoothedSpeed / (85f * motionScale)).Clamp( 0.65f, 1.5f )
            : clip == "run" ? (smoothedSpeed / (177f * motionScale)).Clamp( 0.75f, 1.55f )
            : clip == "crouch_walk" ? (smoothedSpeed / (55f * motionScale)).Clamp( 0.65f, 1.4f ) : 1;
        if ( clip == "crouch_left" ) targetRate = (smoothedSpeed / (60.5f * motionScale)).Clamp( .5f, 1.6f );
        if ( clip == "crouch_right" ) targetRate = (smoothedSpeed / (52f * motionScale)).Clamp( .5f, 1.6f );
        if ( (clip == "crouch_right" || clip == "crouch_left" || clip == "crouch_walk")
            && (renderer.Model?.Name == "models/characters/freya_shuffle_pilot/freya_protected.vmdl"
                || UsesMeiLanding(renderer.Model?.Name)
                || renderer.Model?.Name == "models/characters/imani_shuffle_pilot/imani_protected.vmdl"
                || renderer.Model?.Name == "models/characters/imani_protected_morph/imani_protected.vmdl"
                || renderer.Model?.Name == "models/characters/freya_protected_morph/freya_protected.vmdl") )
            targetRate = (smoothedSpeed / 25.59055f).Clamp( .1f, 3f );
        if ( clip == "walk_backward" ) targetRate = (smoothedSpeed / (52.75f * motionScale)).Clamp( .5f, 1.8f );
        if ( clip == "walk_left" || clip == "walk_right" ) targetRate = (smoothedSpeed / (41.36f * motionScale)).Clamp( .5f, 3f );
        if ( clip == "reload_standing" )
        {
            var authoredReload = CoveredRosterAssets.ReloadTrack( renderer.Model?.Name ) is not null;
            targetRate = authoredReload ? 0 : 2;
            if ( authoredReload ) renderer.Sequence.TimeNormalized = weapon.ReloadProgress;
            playbackRate = targetRate;
            renderer.Sequence.Looping = false;
        }
        else renderer.Sequence.Looping = true;
        playbackRate += (targetRate - playbackRate) * (1 - MathF.Exp( -10 * Time.Delta ));
        renderer.PlaybackRate = playbackRate;
        var yaw = speed > 10 && weapon?.Aiming != true && !(weapon?.AcceptInput == true && Input.Down( "attack1" ))
            ? Rotation.LookAt( velocity ).Yaw() : controller.EyeAngles.yaw;
        if ( cover?.Attached == true && !cover.Peeking ) yaw = Rotation.LookAt( -cover.Normal ).Yaw();
        renderer.WorldRotation = Rotation.Slerp( renderer.WorldRotation, Rotation.FromYaw( yaw ), 1 - MathF.Exp( -9 * Time.Delta ) );
    }
}
