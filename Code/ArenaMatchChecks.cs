using Sandbox;

namespace PaintballBaddies;

public sealed class ArenaMatchChecks : Component
{
    public bool CoveredModels { get; set; }
    public int PlayerCharacterIndex { get; set; } = -1;
    private string ModelPath( ArenaOpponent bot ) => RosterSelection.GetModelPath( bot.CharacterIndex );
    private bool ExpectedEquipment( ArenaOpponent bot )
    {
        if(bot.Components.Get<CitizenOpponentPresentation>() is {} citizen)
            return citizen.IsReady && citizen.VisualActive && citizen.ProtectiveGearReady
                && citizen.EquippedSuit==CitizenRosterAssets.Suit(bot.CharacterIndex)
                && citizen.EquippedHelmet==CitizenRosterAssets.Helmet(bot.CharacterIndex)
                && bot.PresentationAnchor.IsValid() && !bot.Body.Enabled;
        var path = ModelPath( bot );
        var integrated = CoveredRosterAssets.CharacterIndex( path ) >= 0;
        var track = CoveredRosterAssets.ReloadTrack( path );
        return bot.Body.Model == Model.Load( path )
            && bot.Components.Get<RosterHelmet>()?.Equipped == !integrated
            && (track is null ? !bot.Components.Get<ReloadPresentationTrack>().IsValid()
                : bot.Components.Get<ReloadPresentationTrack>() is { } gear && gear.TrackPath == track);
    }
    private float time;
    private int stage;
    private int timeoutAmmo, timeoutReserve, timeoutShots;
    private RosterHelmet[] defeatedGear;
    private ArenaOpponent[] defeatedBots;
    private int[] defeatedAmmo, defeatedReserve;
    private GameObject shotReceiver;
    private void Check( string name, bool pass ) => Log.Info( $"MATCH_CHECK {(pass ? "PASS" : "FAIL")} {name}" );
    protected override void OnUpdate()
    {
        time += Time.Delta;
        var match = Components.Get<ArenaMatch>();
        if ( match is null ) return;
        if ( stage == 0 && time > 2 )
        {
            if ( PlayerCharacterIndex >= 0 )
            {
                Components.Get<RosterSelection>().Select(PlayerCharacterIndex);
                Check("requested player model selected", Components.Get<PlayerController>().Renderer.Model?.Name == RosterSelection.GetModelPath(PlayerCharacterIndex));
            }
            var controller=Components.Get<PlayerController>();
            controller.Body.Velocity=new Vector3(200,80,120);
            controller.WishVelocity=new Vector3(200,80,0);
            match.StartRound();
            Check("round spawn clears previous physical and requested movement",controller.Body.Velocity.Length<.001f && controller.WishVelocity.Length<.001f);
            Check("countdown warns at ten seconds", ArenaMatch.CountdownCue(10.01f,9.99f)=="time_warning" && ArenaMatch.CountdownCue(9.99f,9.98f)==null);
            Check("countdown ticks once at final second boundaries", ArenaMatch.CountdownCue(5.01f,4.99f)=="countdown_tick" && ArenaMatch.CountdownCue(4.99f,4.98f)==null && ArenaMatch.CountdownCue(7,2)=="countdown_tick");
            Check("countdown silent on expiry and timer reset", ArenaMatch.CountdownCue(.01f,0)==null && ArenaMatch.CountdownCue(0,180)==null);
            Check("opponent vision sees forward within range", ArenaOpponent.InSightCone(Vector3.Zero,Vector3.Forward,new Vector3(500,0,0)) && ArenaOpponent.InSightCone(Vector3.Zero,Vector3.Forward,new Vector3(200,300,0)));
            Check("opponent vision rejects rear flank and distant targets", !ArenaOpponent.InSightCone(Vector3.Zero,Vector3.Forward,new Vector3(-100,0,0)) && !ArenaOpponent.InSightCone(Vector3.Zero,Vector3.Forward,new Vector3(0,500,0)) && !ArenaOpponent.InSightCone(Vector3.Zero,Vector3.Forward,new Vector3(1100,0,0)));
            // Keep the unattended player alive while measuring AI navigation and fire.
            // Restore normal durability before testing the loss transition.
            match.PlayerState.HitLimit = 1000;
            Check( "round starts with five opponents", match.InRound && match.OpponentsRemaining == 5 );
            Check( "friendly paint ignored", !match.PlayerState.RegisterHit( 0 ) && match.PlayerState.PaintHits == 0 );
            var magazine = new OpponentMagazine();
            magazine.TryFire();
            Check( "opponent shot cooldown", !magazine.TryFire() );
            magazine.Advance( .2f ); magazine.TryFire();
            magazine.Advance( .2f ); magazine.TryFire();
            magazine.Advance( .2f );
            Check( "three-shot burst has pause", !magazine.TryFire() && magazine.Ammo == 27 );
            for ( int i = 0; i < 27; i++ ) { magazine.Advance( 1 ); magazine.TryFire(); }
            Check( "empty opponent magazine reloads", magazine.Ammo == 0 && magazine.ReloadRemaining > 0 && !magazine.TryFire() );
            magazine.Advance( 1 );
            Check( "opponent cannot fire during reload", !magazine.TryFire() );
            magazine.Advance( 1.3f );
            Check( "opponent reload conserves ammunition", magazine.Ammo == 30 && magazine.Reserve == 90 && magazine.Reloads == 1 );
            stage++;
        }
        else if ( stage == 1 && time > 3 )
        {
            var fittedBots=Scene.GetAllComponents<ArenaOpponent>().ToArray();
            Check("all opponents use expected protective equipment",fittedBots.Length==5 && fittedBots.All(ExpectedEquipment) && fittedBots.Select(x=>x.CharacterIndex).Distinct().Count()==5 && fittedBots.All(x=>x.CharacterIndex != Components.Get<RosterSelection>().Selected));
            Check("opponents use fitted roster bodies",fittedBots.All(x=>x.Body.Model==Model.Load(ModelPath(x))));
            shotReceiver=new GameObject(true,"Match projectile receiver");
            shotReceiver.WorldPosition=new Vector3(-420,0,55);
            var receiverShape=shotReceiver.Components.Create<BoxCollider>();
            receiverShape.Scale=new Vector3(24,24,40);
            shotReceiver.Components.Create<PaintballCombatant>().Team=1;
            Check("player shot launches toward enemy receiver",Components.Get<PaintballMarker>().FireAt(shotReceiver.WorldPosition));
            var shot = new GameObject( true, "Test physical opponent paintball" );
            shot.WorldPosition = WorldPosition + Vector3.Up * 40 + Vector3.Backward * 60;
            var ball = shot.Components.Create<OpponentPaintball>();
            ball.Shooter = shot;
            ball.Velocity = Vector3.Forward * 2283;
            stage++;
        }
        else if ( stage == 2 && time > 4 )
        {
            Check( "physical opponent paintball hits player", match.PlayerState.PaintHits > 0 );
            var marker=Components.Get<PaintballMarker>();
            Check("physical player shot updates enemy damage and statistics",shotReceiver.Components.Get<PaintballCombatant>().PaintHits==1 && marker.Shots==1 && marker.Hits==1);
            Check("physical shots produce paint effects",PaintImpactSystem.Find(Scene)?.ActiveCount>0);
            shotReceiver.Destroy();
            stage++;
        }
        else if ( stage == 3 && time > 15 )
        {
            var bots = Scene.GetAllComponents<ArenaOpponent>().ToArray();
            Log.Info( $"MATCH_CHECK OBSERVATION status={match.Status} bots={bots.Length} receivedHits={match.PlayerState.PaintHits}" );
            Check( "opponents navigate", bots.Any( x => x.DistanceTravelled > 100 ) );
            Check( "all live opponents avoid obstacle crossings", bots.Length == 5 && bots.All( x => x.ObstructedMoves == 0 ) );
            foreach ( var bot in bots )
                Log.Info( $"MATCH_CHECK OBSERVATION navigation character={bot.Character} travelled={bot.DistanceTravelled} crossings={bot.ObstructedMoves} coverSelections={bot.CoverSelections}" );
            Log.Info( $"MATCH_CHECK OBSERVATION opponent shots={bots.Sum( x => x.ShotsFired )}" );
            Check( "opponents fire after acquiring player", bots.Sum( x => x.ShotsFired ) > 0 );
            defeatedGear=bots.Select(x=>x.Components.Get<RosterHelmet>()).ToArray();
            // Arrange an active reload on each actual opponent before tagging out.
            foreach (var bot in bots)
            {
                bot.Magazine.CancelReload();
                bot.Magazine.Advance(1);
                bot.Magazine.TryFire();
                bot.Magazine.BeginReload();
            }
            Check("opponents reloading before elimination", bots.All(x=>x.Magazine.ReloadRemaining>0));
            defeatedBots=bots;
            defeatedAmmo=bots.Select(x=>x.Magazine.Ammo).ToArray();
            defeatedReserve=bots.Select(x=>x.Magazine.Reserve).ToArray();
            foreach ( var bot in bots )
                for ( int i = 0; i < 3; i++ ) bot.Components.Get<PaintballCombatant>().RegisterHit( 0 );
            stage++;
        }
        else if ( stage == 4 && time > 16 )
        {
            Check( "eliminating opponents wins round", match.Status == "VICTORY" && !match.InRound );
            Check("elimination cancels opponent reload without transferring paint", defeatedBots.Select((bot,i)=>
                bot.Magazine.ReloadRemaining==0 && bot.Magazine.Ammo==defeatedAmmo[i] && bot.Magazine.Reserve==defeatedReserve[i]).All(x=>x));
            Check( "elimination notice names defeated opponents", new[]{"IMANI","FREYA","LEILANI"}.All(name=>match.EliminationNotice.Contains(name)) );
            Check("eliminated opponents clear equipment",defeatedGear.All(x=>x.IsValid() && !x.Equipped)
                && !Scene.GetAllComponents<ArenaOpponent>().Any(x=>x.Components.Get<ReloadPresentationTrack>().IsValid()));
            match.StartRound();
            var impacts=PaintImpactSystem.Find(Scene);
            Check("round restart clears paint decals and droplets",impacts is not null && impacts.ActiveCount==0 && impacts.ActiveDroplets==0);
            match.PlayerState.HitLimit = 5;
            for ( int i = 0; i < 5; i++ ) match.PlayerState.RegisterHit( 1 );
            stage++;
        }
        else if ( stage == 5 && time > 17 )
        {
            Check( "player elimination ends round", match.Status == "ELIMINATED" && !match.InRound );
            match.StartRound();
            stage++;
        }
        else if ( stage == 6 && time > 18 )
        {
            Check( "restart restores player and opponents", match.InRound && match.PlayerState.PaintHits == 0 && match.OpponentsRemaining == 5 );
            Check( "restart clears elimination notice", match.EliminationNotice == "" );
            Check( "restart preserves five opponents only", Scene.GetAllComponents<ArenaOpponent>().Count() == 5 );
            Check("restart restores opponent equipment",Scene.GetAllComponents<ArenaOpponent>().All(ExpectedEquipment));
            match.RoundDuration = .25f;
            match.StartRound();
            var weapon = Components.Get<PaintballMarker>();
            Check("timeout fixture launches paintball", weapon.FireAt(WorldPosition + Vector3.Up*1000) && weapon.ActivePaintballs > 0);
            weapon.Reload();
            Check("timeout fixture begins reload", weapon.ReloadRemaining > 0);
            timeoutAmmo=weapon.Ammo; timeoutReserve=weapon.Reserve; timeoutShots=weapon.Shots;
            stage++;
        }
        else if ( stage == 7 && time > 19 )
        {
            Check("timeout ends round and preserves surviving count", !match.InRound && match.Status == "TIME UP" && match.OpponentsRemaining == 5);
            Check("timeout blocks player input", !Components.Get<PlayerController>().UseInputControls && !Components.Get<PaintballMarker>().AcceptInput);
            var weapon = Components.Get<PaintballMarker>();
            Check("timeout clears airborne paint and reload", weapon.ActivePaintballs == 0 && weapon.ReloadRemaining == 0);
            weapon.Reload();
            Check("result rejects firing and reload without changing ammo", !weapon.FireAt(WorldPosition + Vector3.Up*1000) && weapon.ReloadRemaining == 0 && weapon.Ammo == timeoutAmmo && weapon.Reserve == timeoutReserve && weapon.Shots == timeoutShots);
            match.RoundDuration = 180;
            match.StartRound();
            stage++;
        }
        else if ( stage == 8 && time > 20 )
        {
            Check("restart after timeout restores timer and input", match.InRound && match.Remaining > 175 && Components.Get<PlayerController>().UseInputControls && Components.Get<PaintballMarker>().AcceptInput);
            Check("cannot exit a live round into training",!match.ReturnToTraining() && match.InRound);
            for(int i=0;i<5;i++)match.PlayerState.RegisterHit(1);
            stage++;
        }
        else if(stage==9 && time>21)
        {
            Check("result returns to training",match.ReturnToTraining() && match.Status=="TRAINING" && !match.InRound && match.OpponentsRemaining==0);
            var weapon=Components.Get<PaintballMarker>();
            Check("training restores health ammo movement and targets",match.PlayerState.PaintHits==0 && weapon.Ammo==40 && weapon.Reserve==160 && weapon.AcceptInput && Components.Get<PlayerController>().UseInputControls && Scene.GetAllComponents<PaintballTarget>().Any());
            Check("training removes round opponents and allows roster choice",!Scene.GetAllComponents<ArenaOpponent>().Any() && Components.Get<RosterSelection>().Select(2));
            Log.Info( "MATCH_CHECK COMPLETE" );
            Destroy();
        }
    }
}
