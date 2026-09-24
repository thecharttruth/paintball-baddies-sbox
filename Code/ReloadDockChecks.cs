using Sandbox;
using System;

namespace PaintballBaddies;

/// <summary>Compare track and hip-docked gear at the same final reload pose.</summary>
public sealed class ReloadDockChecks : Component
{
    [Property] public string ModelPath { get; set; } = "models/characters/mei_protected_morph/mei_protected.vmdl";
    private float elapsed;
    private bool done;
    protected override void OnUpdate() => elapsed += Time.Delta;
    protected override void OnPreRender()
    {
        if ( done || elapsed < .4f ) return;
        var track = Scene.GetAllComponents<ReloadPresentationTrack>().FirstOrDefault(x => x.Body.IsValid()
            && x.Body.Model?.Name == ModelPath && x.Body.Sequence.Name == "reload_standing" && !x.Docked);
        if ( !track.IsValid() || !track.Body.IsValid() ) return;
        var props = track.PresentationProps.Take(3).Select(x => x.Components.Get<ModelRenderer>()).ToArray();
        if ( props.Length != 3 ) return;
        track.Docked = false; track.Phase = 1; track.ApplyFrame();
        var before = props.Select( x => x.WorldTransform ).ToArray();
        track.Docked = true; track.ApplyFrame();
        for ( int i = 0; i < props.Length; i++ )
        {
            var distance = (props[i].WorldPosition - before[i].Position).Length;
            var forwardError = (props[i].WorldRotation.Forward - before[i].Rotation.Forward).Length;
            var upError = (props[i].WorldRotation.Up - before[i].Rotation.Up).Length;
            Log.Info( $"RELOAD_DOCK {(distance < .1f && forwardError < .01f && upError < .01f ? "PASS" : "FAIL")} {props[i].GameObject.Name}: position={distance} inches forward={forwardError} up={upError}" );
        }
        track.Docked = false; track.ApplyFrame(); done = true;
        Log.Info( "RELOAD_DOCK COMPLETE" );
    }
}
