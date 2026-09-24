using Sandbox;
using System;

namespace PaintballBaddies;

/// <summary>Opt-in measurement of imported clips on the native skeleton.</summary>
public sealed class CombatMotionChecks : Component
{
    private readonly string[] clips = { "vault", "reload_standing", "walk_backward", "crouch_left", "crouch_right" };
    private PlayerController player;
    private ViperAvatar avatar;
    private float elapsed, maxTime, minFoot, maxFoot, maxHipOffset;
    private int index, character;
    protected override void OnStart()
    {
        player = Components.Get<PlayerController>(); avatar = Components.Get<ViperAvatar>();
        avatar.Enabled = false; player.UseInputControls = false; player.UseLookControls = false;
        Components.Get<PaintballMarker>().AcceptInput = false;
        player.Renderer.UseAnimGraph = false; player.Renderer.PlaybackRate = 1;
        SetCharacter();
        Begin();
    }
    private void SetCharacter()
    {
        var slug = RosterSelection.Names[character].ToLowerInvariant();
        player.Renderer.Model = Model.Load( $"models/characters/{slug}/{slug}_combat_preview.vmdl" );
    }
    private void Begin()
    {
        elapsed = maxTime = maxHipOffset = 0; minFoot = 10000; maxFoot = -10000;
        player.Renderer.Sequence.Name = clips[index]; player.Renderer.Sequence.Looping = true; player.Renderer.Sequence.Blending = false;
    }
    protected override void OnUpdate()
    {
        elapsed += Time.Delta;
        maxTime = MathF.Max( maxTime, player.Renderer.Sequence.Time );
        if ( elapsed > .1f )
        {
            foreach ( var name in new[] { "LeftFoot", "RightFoot" } )
                if ( player.Renderer.TryGetBoneTransform( name, out var foot ) )
                {
                    var z = foot.Position.z - WorldPosition.z;
                    minFoot = MathF.Min( minFoot, z ); maxFoot = MathF.Max( maxFoot, z );
                }
            if ( player.Renderer.TryGetBoneTransform( "Hips", out var hip ) )
                maxHipOffset = MathF.Max( maxHipOffset, (hip.Position - WorldPosition).WithZ( 0 ).Length );
        }
        if ( elapsed < 4 ) return;
        var valid = maxTime > .2f && minFoot > -10 && maxFoot < 100 && maxHipOffset < 25;
        Log.Info( $"ROSTER_COMBAT {(valid ? "PASS" : "FAIL")} {RosterSelection.Names[character]} {clips[index]} time={maxTime} footZ={minFoot}..{maxFoot} hipXY={maxHipOffset}" );
        index++;
        if ( index < clips.Length ) { Begin(); return; }
        character++; index = 0;
        if ( character < RosterSelection.Names.Length ) { SetCharacter(); Begin(); return; }
        Components.Get<RosterSelection>().Select( 0 ); avatar.Enabled = true;
        player.UseInputControls = player.UseLookControls = true; Components.Get<PaintballMarker>().AcceptInput = true;
        Log.Info( "ROSTER_COMBAT COMPLETE: structural playback only; visual motion quality requires review" ); Destroy();
    }
}
