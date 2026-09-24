using Sandbox;
using System;

namespace PaintballBaddies;

/// <summary>Opt-in live covered-player muzzle and pitch checks; removed after the run.</summary>
public sealed class CoveredAimChecks : Component
{
    [Property] public string Character { get; set; } = "mei";
    private PlayerController player;
    private PaintballMarker weapon;
    private float elapsed;
    private int stage;
    private float reloadStarted;
    private readonly float[] pitches = { -30, 30, 0 };
    private void Check( string label, bool pass, string detail ) => Log.Info( $"COVERED_AIM {(pass ? "PASS" : "FAIL")} {label}: {detail}" );

    protected override void OnStart()
    {
        player = Scene.GetAllComponents<PlayerController>().First();
        weapon = player.Components.Get<PaintballMarker>();
        player.UseInputControls = player.UseLookControls = false;
        weapon.AcceptInput = false;
    }

    protected override void OnUpdate()
    {
        elapsed += Time.Delta;
        if ( stage == 0 && elapsed > 1 )
        {
            player.Renderer.Model = Model.Load( $"models/characters/{Character}_protected_morph/{Character}_protected.vmdl" );
            player.WorldPosition = new Vector3( 0, 0, 410 );
            player.Body.MotionEnabled = false;
            player.EyeAngles = new Angles( pitches[0], 0, 0 );
            Check( "covered model loaded", player.Renderer.Model.IsValid(), player.Renderer.Model.Name );
            stage = 1;
        }
        else if ( stage >= 1 && stage <= 3 && elapsed > stage + 1.2f )
        {
            var marker = Scene.GetAllComponents<ModelRenderer>().First( x => x.GameObject.Name == "VX-9 training marker" );
            var target = player.WorldPosition + Vector3.Forward * 500 + Vector3.Up * 70;
            Check( $"shot accepted {stage}", weapon.FireAt( target ), $"pitch={pitches[stage-1]}" );
            var barrel = marker.WorldTransform.PointToWorld( PaintballMarker.MuzzleOffset );
            var ball = Scene.GetAllComponents<ModelRenderer>().Where( x => x.GameObject.Name == "Paintball" )
                .OrderBy( x => (x.WorldPosition - barrel).Length ).FirstOrDefault();
            var distance = ball.IsValid() ? (ball.WorldPosition - barrel).Length : float.MaxValue;
            Check( $"paintball starts at barrel {stage}", distance < .01f, $"distance={distance} inches" );
            var actual = marker.WorldRotation.Angles().pitch;
            Check( $"marker follows pitch {stage}", MathF.Abs( actual - pitches[stage-1] ) < 1, $"actual={actual} expected={pitches[stage-1]}" );
            stage++;
            if ( stage <= 3 ) player.EyeAngles = new Angles( pitches[stage-1], 0, 0 );
            else
            {
                weapon.Reload(); reloadStarted = elapsed;
                Check( "authored reload duration", MathF.Abs( weapon.ActiveReloadDuration - 2.4f ) < .001f, $"duration={weapon.ActiveReloadDuration}" );
            }
        }
        else if ( stage == 4 && elapsed - reloadStarted > 1.8f )
        {
            Check( "reload stays blocked beyond legacy timing", weapon.Ammo == 37 && weapon.ReloadRemaining > 0 && !weapon.FireAt( player.WorldPosition + Vector3.Forward * 500 ), $"ammo={weapon.Ammo} remaining={weapon.ReloadRemaining}" );
            var presentation = player.Components.Get<ReloadPresentationTrack>();
            Check( "character reload presentation active", presentation.IsValid() && presentation.TrackPath == $"animations/{Character}_reload_presentation.json"
                && Scene.GetAllComponents<ModelRenderer>().Any( x => x.GameObject.Name == "Reload presentation 0" ), $"{Character} track and pod spawned" );
            Check( "open hopper equipped", Scene.GetAllComponents<ModelRenderer>().Any( x => x.GameObject.Name == "VX-9 training marker"
                && x.Model?.Name == "models/weapons/vx9_speed_feed/vx9_speed_feed.vmdl" ), "speed-feed marker" );
            stage = 5;
        }
        else if ( stage == 5 && elapsed - reloadStarted > 2.6f )
        {
            Check( "authored reload completes and conserves ammo", weapon.Ammo == 40 && weapon.Reserve == 157 && weapon.ReloadRemaining == 0, $"ammo={weapon.Ammo} reserve={weapon.Reserve}" );
            Check( "gear stays docked after reload", player.Components.Get<ReloadPresentationTrack>() is { Docked: true }
                && Scene.GetAllComponents<ModelRenderer>().Any( x => x.GameObject.Name == "Reload presentation 0" ), "belt pod persists" );
            player.Renderer.Model = Model.Load( "models/characters/viper/viper_helmet_hair_preview.vmdl" );
            stage = 6;
        }
        else if ( stage == 6 && elapsed - reloadStarted > 3.1f )
        {
            Check( "gear removed on body change", !player.Components.Get<ReloadPresentationTrack>().IsValid()
                && !Scene.GetAllComponents<ModelRenderer>().Any( x => x.GameObject.Name.StartsWith( "Reload presentation " ) ), "no orphan props" );
            stage = 7; Log.Info( "COVERED_AIM COMPLETE" );
        }
    }
}
