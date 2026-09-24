using System;
using Sandbox;

namespace PaintballBaddies;

/// <summary>Single-player practice range. Targets are spawned only in the running scene.</summary>
public sealed class TrainingRange : Component
{
    [Property] public bool UseCitizenCharacters { get; set; }
    [Property] public bool RunIntegrationChecks { get; set; }
    [Property] public bool RunRosterChecks { get; set; }
    [Property] public bool RunNavigationChecks { get; set; }
    [Property] public bool RunMatchChecks { get; set; }
    [Property] public bool RunCoveredMatchChecks { get; set; }
    [Property] public int MatchCheckCharacter { get; set; } = -1;
    [Property] public bool RunCoverChecks { get; set; }
    [Property] public bool RunCombatMotionChecks { get; set; }
    [Property] public bool RunVaultChecks { get; set; }
    [Property] public bool RunVaultTraversalChecks { get; set; }
    [Property] public string VaultCheckModelPath { get; set; } = "";
    private GameObject targetsRoot;
    public void SetTargetsVisible( bool visible ) { if ( targetsRoot is not null ) targetsRoot.Enabled = visible; }
    public static GameObject Box( GameObject parent, string name, Vector3 position, Vector3 size, Color color, bool solid = false )
    {
        var go = new GameObject( parent, true, name );
        go.LocalPosition = position;
        var renderer = go.Components.Create<ModelRenderer>();
        renderer.Model = Model.Load( "models/dev/box.vmdl" );
        renderer.Tint = color;
        go.LocalScale = size / 50;
        if ( solid )
        {
            var collider = go.Components.Create<BoxCollider>();
            collider.Scale = new Vector3( 50 );
            collider.Static = true;
        }
        return go;
    }

    public static void CreateLocalHud(Scene scene)
    {
        if(!scene.GetAllComponents<ArenaVisualPolish>().Any())
            new GameObject(true,"Arena visual polish"){NetworkMode=NetworkMode.Never}.Components.Create<ArenaVisualPolish>();
        if(scene.GetAllComponents<TrainingHud>().Any())return;
        var ui=new GameObject(true,"Training HUD"){NetworkMode=NetworkMode.Never};
        ui.Components.Create<ScreenPanel>();ui.Components.Create<TrainingHud>();
    }
    protected override void OnStart()
    {
        if(!MultiplayerSession.Find(Scene).IsValid())
            new GameObject(true,"Paintball multiplayer session").Components.Create<MultiplayerSession>();
        if(MultiplayerSession.Online){CreateLocalHud(Scene);return;}
        var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
        if ( player is null ) return;
        if ( PaintImpactSystem.Find(Scene) is null ) Components.Create<PaintImpactSystem>();
        if ( player.Components.Get<PaintballMarker>() is null ) player.Components.Create<PaintballMarker>();
        if ( player.Components.Get<RosterSelection>() is null )
            player.Components.Create<RosterSelection>().UseCitizenCharacters=UseCitizenCharacters;
        if ( player.Components.Get<ArenaMatch>() is null ) player.Components.Create<ArenaMatch>();
        if ( player.Components.Get<CoverController>() is null ) player.Components.Create<CoverController>();
        if ( player.Components.Get<VaultController>() is null ) player.Components.Create<VaultController>();
        if ( player.Components.Get<ForwardAutoRun>() is null ) player.Components.Create<ForwardAutoRun>();
        foreach ( var collider in Scene.GetAllComponents<BoxCollider>().ToArray() )
        {
            var name = collider.GameObject.Name;
            if ( collider.Static && (name.StartsWith( "Cover " ) || name.StartsWith( "Bunker -" ) || name == "foundry_low_bunker" || name == "foundry_equipment_crate")
                && collider.Components.Get<CoverSurface>() is null ) collider.Components.Create<CoverSurface>();
        }
        var rangeRoot = new GameObject( true, "Training range targets" ){NetworkMode=NetworkMode.Never};
        targetsRoot = rangeRoot;
        for ( int i = 0; i < (ArenaMap.Find(Scene)?.ShowPracticeTargets==false ? 0 : 5); i++ )
        {
            // The first three are in the clear starting lane; two reward navigating cover.
            var position = i < 3 ? new Vector3( -420, (i - 1) * 125, 55 )
                : new Vector3( 100, i == 3 ? -390 : 390, 65 );
            var target = new GameObject( rangeRoot, true, $"Practice target {i+1}" );
            target.LocalPosition = position;
            var plate = Model.Load( "models/foundry/practice_target/practice_plate.vmdl" );
            target.Components.Create<ModelRenderer>().Model = plate;
            var collider = target.Components.Create<ModelCollider>();
            collider.Model = plate;
            collider.Static = true;
            target.Components.Create<PaintballTarget>();
            var stand = new GameObject( rangeRoot, true, $"Practice target {i+1} stand" );
            stand.LocalPosition = position.WithZ( 0 );
            stand.LocalScale = new Vector3( 1, 1, (position.z - 23) / 32 );
            stand.Components.Create<ModelRenderer>().Model = Model.Load( "models/foundry/practice_target/practice_stand.vmdl" );
        }
        CreateLocalHud(Scene);
        ArenaShield.ResetArena(Scene);
        if ( RunIntegrationChecks ) player.Components.Create<TrainingChecks>();
        if ( RunRosterChecks ) player.Components.Create<RosterChecks>();
        if ( RunNavigationChecks ) player.Components.Create<OpponentNavigationChecks>();
        if ( RunMatchChecks ) { var checks = player.Components.Create<ArenaMatchChecks>(); checks.CoveredModels = RunCoveredMatchChecks; checks.PlayerCharacterIndex = MatchCheckCharacter; }
        if ( RunCoverChecks ) player.Components.Create<CoverChecks>();
        if ( RunCombatMotionChecks ) player.Components.Create<CombatMotionChecks>();
        if ( RunVaultChecks ) player.Components.Create<VaultChecks>();
        if ( RunVaultTraversalChecks ) player.Components.Create<VaultTraversalChecks>().TestModelPath = VaultCheckModelPath;
    }
}

public sealed class PaintballTarget : Component
{
    // The rigid practice plate stays still; physical paint decals provide hit feedback.
    public int HitsReceived { get; private set; }
    public void Hit() => HitsReceived++;
}
