using Sandbox;
using System.Linq;
namespace PaintballBaddies;

/// <summary>Opt-in native cover/reload sequence against placed arena geometry.</summary>
public sealed class OpponentCoverChecks : Component
{
    [Property] public bool CitizenCharacters { get; set; }
    [Property] public bool ReviewFootsteps { get; set; }
    [Property] public bool SpoolMode { get; set; }
    private bool reachedSpool;
    [Property] public bool FlankMode { get; set; }
    private bool flanked;
    [Property] public bool PressureMode { get; set; }
    [Property] public bool TwoOpponents { get; set; }
    private ArenaOpponent opponent;
    private ArenaOpponent second;
    private PlayerController player;
    private float elapsed;
    private bool arrived,reloaded,crouched,sheltered,reengaged;
    private int previousShots,coveredShots;
    private bool pressureApplied,secondArrived,durabilityAdjusted;
    private int pressureAmmo,originalHitLimit;
    private int movingFrames,alignedFrames,animatedFrames;
    private void Check(string name,bool pass,string detail)=>Log.Info($"AI_COVER {(pass ? "PASS" : "FAIL")} {name}: {detail}");
    protected override void OnStart()
    {
        player=Scene.GetAllComponents<PlayerController>().First();
        player.UseInputControls=player.UseLookControls=false;
        player.WorldPosition=SpoolMode ? new Vector3(-950,400,1) : new Vector3(-630,100,1);
        Scene.GetAllComponents<TrainingRange>().First().SetTargetsVisible(false);
        Scene.NavMesh.IsEnabled=true;Scene.NavMesh.IncludeStaticBodies=true;
        Scene.NavMesh.IncludeKeyframedBodies=false;Scene.NavMesh.AgentHeight=67;Scene.NavMesh.AgentRadius=17;Scene.NavMesh.SetDirty();
        var go=new GameObject(true,"Tactical cover test opponent");go.WorldPosition=SpoolMode ? new Vector3(-590,520,1) : new Vector3(-190,-40,1);
        go.WorldRotation=Rotation.LookAt((player.WorldPosition-go.WorldPosition).WithZ(0));
        if(CitizenCharacters)go.Components.Create<CitizenOpponentPresentation>();
        opponent=go.Components.Create<ArenaOpponent>();opponent.CombatEnabled=true;
        // Pressure mode keeps ammunition above the low-ammo trigger while
        // leaving room to validate a real reload (a full magazine cannot reload).
        for(int i=0;i<(PressureMode ? 3 : 24);i++) { opponent.Magazine.TryFire();opponent.Magazine.Advance(1); }
        if(TwoOpponents)
        {
            var teammate=new GameObject(true,"Second tactical cover test opponent");teammate.WorldPosition=new Vector3(-190,80,1);
            teammate.WorldRotation=Rotation.LookAt((player.WorldPosition-teammate.WorldPosition).WithZ(0));
            if(CitizenCharacters)teammate.Components.Create<CitizenOpponentPresentation>();
            second=teammate.Components.Create<ArenaOpponent>();second.CombatEnabled=true;second.Character="leilani";
            for(int i=0;i<24;i++) { second.Magazine.TryFire();second.Magazine.Advance(1); }
        }
    }
    protected override void OnUpdate()
    {
        var state=player?.Components.Get<PaintballCombatant>();
        if(state is null || !opponent.IsValid())return;
        if(!durabilityAdjusted) { originalHitLimit=state.HitLimit;state.HitLimit=1000;durabilityAdjusted=true; }
        if(opponent.Components.Get<MovementFootsteps>() is { } steps)steps.ReviewContacts=ReviewFootsteps;
        elapsed+=Time.Delta;
        if(elapsed>1 && opponent.MovementSpeed>20)
        {
            movingFrames++;
            if(opponent.MovementFacingDot>.8f)alignedFrames++;
            var presentation=opponent.Components.Get<CitizenOpponentPresentation>();
            if(presentation is not null && presentation.IsReady && System.MathF.Abs(presentation.AnimationSpeed-opponent.NavigationVelocity.WithZ(0).Length)<20)animatedFrames++;
        }
        if(PressureMode && !pressureApplied && elapsed>1.5f)
        {
            pressureAmmo=opponent.Magazine.Ammo;
            pressureApplied=opponent.Components.Get<PaintballCombatant>().RegisterHit(0,player.WorldPosition);
        }
        if(FlankMode && opponent.UsingCover && !flanked)
        {
            var away=(opponent.WorldPosition-player.WorldPosition).WithZ(0).Normal;
            player.WorldPosition=opponent.WorldPosition+away*100;
            flanked=true;
        }
        if(FlankMode && elapsed>10)
        {
            Check("flank begins after cover arrival",flanked,"player moves onto exposed side");
            Check("observed flank invalidates cover",opponent.InvalidatedCoverPlans>0,$"invalidated={opponent.InvalidatedCoverPlans}");
            opponent.GameObject.Destroy();
            state.HitLimit=originalHitLimit;state.ResetPaintHits();
            player.UseInputControls=player.UseLookControls=true;
            Scene.GetAllComponents<TrainingRange>().First().SetTargetsVisible(true);
            Log.Info("AI_COVER COMPLETE");Destroy();return;
        }
        secondArrived |= second?.UsingCover==true;
        if(opponent.UsingCover)
        {
            arrived=true;
            reachedSpool |= (opponent.WorldPosition-new Vector3(-720,400,0)).WithZ(0).Length<70;
            var citizen=opponent.Components.Get<CitizenOpponentPresentation>();
            var lowered=CitizenCharacters ? citizen is not null && citizen.IsReady && citizen.HeadHeight>0 && citizen.HeadHeight<55 : opponent.Components.Get<SkinnedModelRenderer>().Sequence.Name=="crouch";
            crouched |= lowered && opponent.Components.Get<BoxCollider>().Scale.z==43
                && System.MathF.Abs(opponent.Components.Get<PaintballCombatant>().EyePosition.z-opponent.WorldPosition.z-35)<.01f;
            var shield=Scene.Trace.Ray(player.WorldPosition+Vector3.Up*55,opponent.WorldPosition+Vector3.Up*30)
                .IgnoreGameObjectHierarchy(player.GameObject).IgnoreGameObjectHierarchy(opponent.GameObject).Run();
            sheltered |= shield.Hit && shield.GameObject.Components.Get<CoverSurface>() is not null;
            coveredShots+=opponent.ShotsFired-previousShots;
        }
        if(arrived && !opponent.UsingCover && opponent.ShotsFired>previousShots)reengaged=true;
        previousShots=opponent.ShotsFired;
        reloaded |= opponent.UsingCover && opponent.Magazine.Reloads>0;
        if(elapsed<14)return;
        if(CitizenCharacters)
            Check("locomotion speed follows navigation",movingFrames>30 && animatedFrames>movingFrames*.8f,$"matched={animatedFrames}/{movingFrames}; native strafe/backpedal allowed");
        else
            Check("locomotion follows travel",movingFrames>30 && alignedFrames>(movingFrames*.8f),$"aligned={alignedFrames}/{movingFrames}");
        Check("shots follow marker aim",opponent.ShotsFired>0 && opponent.MinimumShotAlignment>=.966f,$"shots={opponent.ShotsFired}, minimumDot={opponent.MinimumShotAlignment}");
        Check("selects nearby cover",opponent.CoverSelections>0,$"selections={opponent.CoverSelections}, destination={opponent.CoverDestination}");
        Check("reaches selected cover",arrived,$"position={opponent.WorldPosition}");
        Check("crouches with smaller hit collider and lowered sight point",crouched,"observed while sheltered");
        Check("reloads in cover",reloaded,$"reloads={opponent.Magazine.Reloads}, ammo={opponent.Magazine.Ammo}, reserve={opponent.Magazine.Reserve}");
        Check("leaves reload cover",arrived && !opponent.UsingCover,"re-engagement state");
        Check("actual crouched position shielded",sheltered,"ray from observed threat blocked by marked cover");
        Check("does not fire while sheltered",arrived && coveredShots==0,$"shots={coveredShots}");
        Check("fires after re-engaging",reengaged,$"total shots={opponent.ShotsFired}");
        Check("does not cross obstruction",opponent.ObstructedMoves==0,$"crossings={opponent.ObstructedMoves}, distance={opponent.DistanceTravelled}");
        if(SpoolMode)Check("reaches placed spool shelter",reachedSpool,"within70in of west spool while UsingCover");
        if(PressureMode)Check("paint pressure triggers cover above low-ammo threshold",pressureApplied && pressureAmmo>6 && arrived,$"ammo at hit={pressureAmmo}");
        if(TwoOpponents)
        {
            Check("teammates choose separate cover destinations",opponent.CoverSelections>0 && second.CoverSelections>0
                && (opponent.CoverDestination-second.CoverDestination).WithZ(0).Length>=65,$"first={opponent.CoverDestination}, second={second.CoverDestination}");
            Check("second opponent reaches cover",secondArrived,$"selections={second.CoverSelections}");
            Check("second opponent avoids obstruction crossings",second.ObstructedMoves==0,$"crossings={second.ObstructedMoves}");
            second.GameObject.Destroy();
        }
        opponent.GameObject.Destroy();
        player.Components.Get<PaintballCombatant>().HitLimit=originalHitLimit;
        player.Components.Get<PaintballCombatant>().ResetPaintHits();
        player.UseInputControls=player.UseLookControls=true;
        Scene.GetAllComponents<TrainingRange>().First().SetTargetsVisible(true);
        Log.Info("AI_COVER COMPLETE");Destroy();
    }
}
