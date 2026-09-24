using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in native input/contact regression; removes its platform and restores player control.</summary>
public sealed class CloseCombatReview : Component
{
    public bool VisualOnly { get; set; }
    public bool PlayerFallVisual {get;set;}
    public bool ShieldMode {get;set;}
    private ArenaShield reviewShield;
    public float PauseAt { get; set; }=-1;
    public bool Done { get; private set; }
    public List<string> Results { get; }=new();
    public float Elapsed { get; private set; }
    private PlayerController player;
    private CloseCombat attacker,victim;
    private PaintballCombatant playerActor,botActor;
    private ArenaOpponent bot;
    private GameObject botObject,wall;
    private Vector3 oldPosition;
    private Angles oldAngles;
    private bool oldLook,oldCamera,button,previousPrimary,previousFocus;
    private System.IDisposable inputHook;
    private int stage;
    private float stageTime;
    private int initialContacts,initialSounds;
    private int initialPlayerHits,initialPlayerLanded;
    private int initialKnockdowns,initialPoints;
    private bool secondAttackPlaced;
    public List<string> MissTrace { get; }=new();
    public string State=>Json.Serialize(new {stage,Elapsed,stageTime,Done,PlayerDown=attacker?.Downed,PlayerRecovering=attacker?.Recovering,
        Presses=attacker?.RecoveryPresses,Input=player?.UseInputControls,Contacts=attacker?.Contacts,Sounds=attacker?.ImpactSounds,
        VictimDown=victim?.Downed,VictimRecovering=victim?.Recovering,BotPresses=victim?.RecoveryPresses});
    protected override void OnStart()
    {
        Scene.GetAllComponents<PaintballControls>().First().StartPractice();
        player=Scene.GetAllComponents<PlayerController>().First();
        playerActor=player.Components.Get<PaintballCombatant>();attacker=player.Components.Get<CloseCombat>();
        initialContacts=attacker.Contacts;initialSounds=attacker.ImpactSounds;
        initialPlayerHits=playerActor.PaintHits;initialPlayerLanded=playerActor.HitsLanded;
        initialKnockdowns=playerActor.KnockdownsLanded;
        initialPoints=playerActor.Points;
        oldPosition=player.WorldPosition;oldAngles=player.EyeAngles;oldLook=player.UseLookControls;oldCamera=player.UseCameraControls;
        player.UseLookControls=false;player.UseCameraControls=false;
        player.WorldPosition=new(0,0,1202);player.EyeAngles=Angles.Zero;player.Body.Velocity=Vector3.Zero;
        TrainingRange.Box(GameObject,"Contact review floor",new(0,0,1198),new(420,420,4),Color.Gray,true);
        botObject=new GameObject(GameObject){Name="Elbow review opponent",WorldPosition=new(42,0,1200),WorldRotation=Rotation.FromYaw(180)};
        botObject.Components.Create<CitizenOpponentPresentation>();
        bot=botObject.Components.Create<ArenaOpponent>();bot.Character="imani";bot.CombatEnabled=false;
        botActor=botObject.Components.Create<PaintballCombatant>();botActor.Team=91;botActor.StayInMatch=true;
        var collider=botObject.Components.Create<BoxCollider>();collider.Scale=new(28,28,66);collider.Center=new(0,0,33);
        if(ShieldMode)reviewShield=new GameObject(GameObject){Name="Melee shield review panel"}.Components.Create<ArenaShield>();
        inputHook=Scene.AddHook(GameObjectSystem.Stage.StartUpdate,-40,InjectInput,nameof(CloseCombatReview),"Contact test action edges");
    }
    private void InjectInput()
    {
        bool focus=button && (attacker.Downed || stage==2);
        bool primary=button && !focus;
        Input.SetAction("attack2",focus);Input.SetLastAction("attack2",previousFocus);previousFocus=focus;
        Input.SetAction("attack1",primary);Input.SetLastAction("attack1",previousPrimary);previousPrimary=primary;
        Input.AnalogMove=Vector3.Zero;Input.AnalogLook=Angles.Zero;
    }
    protected override void OnPreRender()
    {
        if((!VisualOnly && !ShieldMode) || !player.IsValid())return;
        Scene.Camera.WorldPosition=ShieldMode ? new Vector3(-60,-95,1268) : new Vector3(65,-145,1265);
        Scene.Camera.WorldRotation=Rotation.LookAt(new Vector3(22,0,1235)-Scene.Camera.WorldPosition);
    }
    private void Check(bool ok,string name){var result=(ok ? "PASS " : "FAIL ")+name;Results.Add(result);Log.Info("ELBOW_REVIEW "+result);}
    private void Next(){stage++;stageTime=0;button=false;}
    private void PlaceBot(){botObject.WorldPosition=new(42,0,1200);botObject.WorldRotation=Rotation.FromYaw(180);}
    protected override void OnUpdate()
    {
        if(Done)return;
        Elapsed+=Time.Delta;stageTime+=Time.Delta;
        if(!bot.IsValid() || !botObject.Components.Get<CitizenOpponentPresentation>().IsReady)return;
        victim ??= botObject.Components.Get<CloseCombat>();if(!victim.IsValid())return;
        if(stage!=15)
        {
            bot.Enabled=false;
            var agent=bot.Components.Get<NavMeshAgent>();
            agent?.Stop();
            // This fixture positions the victim explicitly. A stopped navigation
            // agent otherwise pulls a teleported victim back into elbow range.
            if(agent.IsValid())agent.UpdatePosition=false;
        }
        if(ShieldMode)
        {
            ShieldSequence();
            if(PauseAt>0 && Elapsed>=PauseAt){Scene.TimeScale=0;PauseAt=-1;}
            return;
        }
        if(VisualOnly){Visual();return;}
        if(Elapsed>36){Check(false,"timed out "+State);Done=true;button=false;return;}
        switch(stage)
        {
            case 0:
                if(stageTime<.7f)return;
                Check(attacker.CanContact(botActor),"near opponent is within elbow reach");
                botActor.Team=playerActor.Team;Check(!attacker.CanContact(botActor),"same team rejected");botActor.Team=91;
                botObject.WorldPosition=new(80,0,1200);Check(!attacker.CanContact(botActor),"distant opponent rejected");PlaceBot();
                wall=TrainingRange.Box(GameObject,"Contact test wall",new(21,0,1238),new(5,40,70),Color.Gray,true);
                Next();break;
            case 1:
                if(stageTime<.1f)return;
                Check(!attacker.TryAttack(),"wall blocks elbow");wall.Destroy();Next();break;
            case 2:
                botObject.WorldPosition=new(90,0,1200);
                button=stageTime<.35f;
                if(stageTime>.55f){Check(!attacker.Attacking && attacker.Contacts==initialContacts,"holding and releasing aim at range does not elbow");PlaceBot();Next();}
                break;
            case 3:
                button=stageTime<.55f;
                if(stageTime>.20f && !Results.Any(x=>x.Contains("within 200")))
                {
                    Check(victim.Downed,"single native press contacts within 200 milliseconds");
                    Check(attacker.ImpactFeedbacks>0 && player.EyeAngles==Angles.Zero,"native impact feedback fires without changing aim");
                }
                if(stageTime>.15f && stageTime<.25f && !Results.Any(x=>x.Contains("press starts")))
                    Check(attacker.Attacking,"native attack1 press starts gun-butt strike before release");
                if(stageTime>.6f){Check(attacker.Contacts==initialContacts+1 && victim.Downed,"quick native attack1 tap lands gun-butt strike and knocks down");
                    Check(attacker.ImpactSounds==initialSounds+1,"one impact sound for one contact");
                    Check(playerActor.HitsLanded==initialPlayerLanded && playerActor.PaintHits==initialPlayerHits && botActor.PaintHits==0,"strike keeps paint hit counts separate");
                    Check(playerActor.KnockdownsLanded==initialKnockdowns+1 && botActor.KnockdownsReceived==1 && playerActor.Points==initialPoints+2 && botActor.Points==-2,"one knockdown awards +2 and deducts 2 exactly once while held");Next();}
                break;
            case 4:
                if(stageTime<.7f)return;
                if(!secondAttackPlaced)
                {
                    Check(player.UseInputControls,"attacker movement restored after elbow");
                    victim.ResetState();PlaceBot();secondAttackPlaced=true;return;
                }
                Check(attacker.TryAttack(),"second elbow starts after cooldown");botObject.WorldPosition=new(140,0,1200);Next();break;
            case 5:
                if(MissTrace.Count<7 && stageTime>MissTrace.Count*.12f)
                    MissTrace.Add($"t={stageTime:0.000} attacker={player.WorldPosition} victim={botObject.WorldPosition} down={victim.Downed}");
                if(stageTime<.9f)return;
                Check(attacker.Contacts==initialContacts+1 && attacker.ImpactSounds==initialSounds+1 && !victim.Downed,"opponent leaving reach avoids hit and sound");
                attacker.ReceiveContact(botObject.WorldPosition,true);Next();break;
            case 6:
                if(stageTime<1.2f)return;
                Check(attacker.Downed && !player.UseInputControls,"player knockdown locks movement");
                Check(!attacker.GetUp(),"cannot bypass required recovery presses");Next();break;
            case 7:
                button=stageTime<.8f;
                if(stageTime>1){Check(attacker.Downed && attacker.RecoveryPresses==0,"holding right mouse does not recover; stale press expires");Next();}
                break;
            case 8:
                button=(stageTime<.08f || (stageTime>.65f && stageTime<.73f));
                if(stageTime>.85f){Check(attacker.Downed && attacker.RecoveryPresses==1,"slow presses restart at one");Next();}
                break;
            case 9:
                if(stageTime<.5f)return;
                Check(attacker.RecoveryPresses==0,"pause breaks consecutive sequence");Next();break;
            case 10:
                button=stageTime<.72f && stageTime% .18f<.075f;
                if(stageTime>.71f){Check(attacker.Downed && attacker.RecoveryPresses==4,"four fast native taps keep player down");Next();}
                break;
            case 11:
                button=stageTime>.08f && stageTime<.16f;
                if(stageTime>.3f){Check(attacker.Recovering && !attacker.Downed && !player.UseInputControls,"fifth fast tap starts animated get-up");Next();}
                break;
            case 12:
                if(stageTime<1.45f)return;
                Check(!attacker.Incapacitated && player.UseInputControls,"get-up restores player movement");
                attacker.ReceiveContact(botObject.WorldPosition,true);Next();break;
            case 13:
                if(stageTime<1.2f)return;
                var controls=Scene.GetAllComponents<PaintballControls>().First();controls.Toggle();
                attacker.ObserveAimInput(true,true,false,.02f);
                Check(attacker.RecoveryPresses==0,"menu clicks cannot advance recovery");controls.Toggle();
                attacker.ResetState();Check(!attacker.Incapacitated && player.UseInputControls,"reset during knockdown restores controls");
                PlaceBot();victim.ReceiveContact(player.WorldPosition,true);bot.CombatEnabled=true;bot.Enabled=true;
                bot.Components.Get<NavMeshAgent>().UpdatePosition=true;stage=15;stageTime=0;break;
            case 15:
                if(stageTime<4.2f)return;
                Check(!victim.Incapacitated && victim.RecoveryPresses==5,"AI uses the same five-press recovery sequence");
                attacker.ResetState();victim.ResetState();playerActor.ResetPaintHits();botActor.ResetPaintHits();
                player.WorldPosition=new(0,0,1202);player.EyeAngles=Angles.Zero;
                botObject.WorldPosition=new(42,0,1200);Next();break;
            case 16:
                // Fresh close press, then release. Ranged holds are covered by GearsControlsReview.
                button=stageTime<.045f;
                if(stageTime<.32f)return;
                Check(victim.Downed && playerActor.KnockdownsLanded==1,"fresh close press lands after round reset");
                Check(playerActor.KnockdownsReceived==0 && playerActor.PaintHits==0 && playerActor.Points==2,"round reset clears previous knockdown scoring");
                Next();break;
            case 17:
                if(stageTime<.15f || attacker.CooldownRemaining>.15f)return;
                victim.ResetState();botObject.WorldPosition=player.WorldPosition+Vector3.Forward*42;
                Next();break;
            case 18:
                // Cooldown from the previous strike has nearly ended.
                button=stageTime<.045f;
                if(stageTime<.45f)return;
                Check(victim.Downed && playerActor.KnockdownsLanded==2,"one buffered press near cooldown end starts the next strike");
                Check(botActor.KnockdownsReceived==2 && botActor.Points==-4,"successive actual knockdowns score symmetrically");
                attacker.ResetState();victim.ResetState();player.WorldPosition=new(0,0,1202);player.EyeAngles=Angles.Zero;
                wall=TrainingRange.Box(GameObject,"Melee cover test",new(40,0,1230),new(20,160,60),Color.Gray,true);
                wall.Components.Create<CoverSurface>();botObject.WorldPosition=new(0,42,1200);Next();break;
            case 19:
                if(stageTime<.2f)return;
                Check(player.Components.Get<CoverController>().TryEnter(Vector3.Forward),"native cover attachment available before close strike");
                player.EyeAngles=new Angles(0,90,0);Next();break;
            case 20:
                button=stageTime<.075f;
                if(stageTime<.55f)return;
                Check(victim.Downed && !player.Components.Get<CoverController>().Attached,"one press exits cover and strikes a clear nearby opponent");
                Check(player.UseInputControls,"cover strike restores movement after follow-through");
                victim.ResetState();attacker.ResetState();botActor.AcceptHits=false;
                int prior=playerActor.KnockdownsLanded;
                Check(!attacker.TryAttack() && playerActor.KnockdownsLanded==prior,"inactive round target rejects attack without score");
                Done=true;button=false;break;
        }
    }
    private void Visual()
    {
        if(stage==0 && stageTime>.8f){if(PlayerFallVisual)attacker.ReceiveContact(player.WorldPosition+Vector3.Forward*50,true);else attacker.TryAttack();Next();}
        else if(stage==1 && stageTime>2.2f){Next();}
        else if(stage==2)
        {
            if(stageTime>.18f){victim.RegisterRecoveryPress();stageTime=0;}
            if(victim.Recovering)Next();
        }
        if(PauseAt>0 && Elapsed>=PauseAt){Scene.TimeScale=0;PauseAt=-1;}
    }
    private void ShieldSequence()
    {
        if(Elapsed>26){Check(false,"shield melee review timed out");Done=true;return;}
        switch(stage)
        {
            case 0:
                if(stageTime<.8f)return;
                Check(reviewShield.Collect(botActor),"shield collected at full strength");Next();break;
            case 1:
                if(stageTime<.3f)return;
                Check(attacker.TryAttack(),"first gun-butt strike starts against raised shield");Next();break;
            case 2:
                if(stageTime<.75f)return;
                Check(reviewShield.Remaining==2 && reviewShield.Owner==botActor,"first frontal strike leaves two shield charges");
                Check(!victim.Downed,"first shield strike is absorbed without knockdown");
                Check(playerActor.KnockdownsLanded==initialKnockdowns && botActor.KnockdownsReceived==0,"absorbed shield strike gives no knockdown points");
                Check(attacker.Contacts==initialContacts+1 && attacker.ImpactSounds==initialSounds+1,"one confirmed shield contact and impact sound");
                Check(playerActor.HitsLanded==initialPlayerLanded && playerActor.PaintHits==initialPlayerHits && botActor.PaintHits==0,"shield melee adds no paint score");Next();break;
            case 3:
                if(stageTime<.65f)return;
                Check(attacker.TryAttack(),"second gun-butt strike starts after cooldown");Next();break;
            case 4:
                if(stageTime<.4f)return;
                Check(reviewShield.Remaining==0 && !reviewShield.Owner.IsValid(),"second strike breaks and releases shield");
                Check(victim.Downed,"shield-breaking strike knocks wearer down");
                Check(playerActor.KnockdownsLanded==initialKnockdowns+1 && botActor.KnockdownsReceived==1,"shield-breaking knockdown scores once for each participant");
                Check(reviewShield.Shatters==1,"one shield break event");
                Check(Scene.GetAllComponents<ShieldBreakEffect>().Sum(x=>x.Fragments)==16,"native break emits all 16 curved fragments");
                Check(attacker.Contacts==initialContacts+2 && playerActor.HitsLanded==initialPlayerLanded && playerActor.PaintHits==initialPlayerHits && botActor.PaintHits==0,"two contacts without paint scoring");Next();break;
            case 5:
                if(stageTime<4.3f)return;
                Check(!Scene.GetAllComponents<ShieldBreakEffect>().Any(),"fragments expire without leaving colliders or objects");
                Check(reviewShield.Remaining==5 && !reviewShield.Owner.IsValid() && reviewShield.RespawnDelay<=0,"broken shield respawns at five charges");
                Check(reviewShield.Shatters==1,"respawn does not replay shatter");
                victim.ResetState();reviewShield.Collect(botActor);Next();break;
            case 6:
                if(stageTime<.25f)return;
                var front=botActor.WorldPosition+reviewShield.WorldRotation.Forward*40;
                var rear=botActor.WorldPosition-reviewShield.WorldRotation.Forward*40;
                Check(ArenaShield.TryAbsorbMelee(botActor,rear)==ArenaShield.MeleeResult.None && reviewShield.Remaining==5,"rear strike bypasses shield without damaging it");
                victim.ReceiveContact(rear,true);
                Check(victim.Downed && !reviewShield.Owner.IsValid() && reviewShield.Remaining==0,"AI knockdown immediately forfeits a full shield");
                Check(reviewShield.Shatters==1 && reviewShield.RespawnDelay>0,"forfeited shield uses respawn delay without an extra damage shatter");
                Check(ArenaShield.TryAbsorbMelee(botActor,front)==ArenaShield.MeleeResult.None,"lost shield provides no protection");
                Next();break;
            case 7:
                if(stageTime<4.3f)return;
                Check(reviewShield.Remaining==5 && !reviewShield.Owner.IsValid(),"forfeited shield returns as an unowned pickup");
                Check(!reviewShield.Collect(botActor),"downed AI cannot collect a fresh shield");
                victim.ResetState();
                Check(!reviewShield.Owner.IsValid(),"AI recovery does not restore lost shield");
                Check(reviewShield.Collect(playerActor),"standing player can collect the returned shield");
                attacker.ReceiveContact(botObject.WorldPosition,true);
                Check(attacker.Downed && !reviewShield.Owner.IsValid() && reviewShield.Remaining==0,"player knockdown also forfeits a full shield");
                Next();break;
            case 8:
                if(stageTime<4.3f)return;
                Check(!reviewShield.Collect(playerActor),"downed player cannot collect a fresh shield");
                attacker.ResetState();
                Check(!reviewShield.Owner.IsValid(),"player recovery does not restore lost shield");
                Check(reviewShield.Collect(botActor),"standing AI can collect another shield");
                reviewShield.Absorb();
                Check(reviewShield.Remaining==4,"paintball still consumes one shield charge");
                var raisedFront=botActor.WorldPosition+reviewShield.WorldRotation.Forward*40;
                Check(ArenaShield.TryAbsorbMelee(botActor,raisedFront)==ArenaShield.MeleeResult.Absorbed && reviewShield.Remaining==1,"melee and paintball shield damage combine");
                Check(ArenaShield.TryAbsorbMelee(botActor,raisedFront)==ArenaShield.MeleeResult.Broken && reviewShield.Remaining==0,"weakened shield breaks without negative durability");
                Done=true;break;
        }
        if(PauseAt>0 && Elapsed>=PauseAt){Scene.TimeScale=0;PauseAt=-1;}
    }
    protected override void OnDestroy()
    {
        if(reviewShield.IsValid())reviewShield.GameObject.Destroy();
        inputHook?.Dispose();Input.ReleaseActions();Scene.TimeScale=1;
        if(player.IsValid())
        {
            attacker?.ResetState();player.WorldPosition=oldPosition;player.EyeAngles=oldAngles;
            player.UseLookControls=oldLook;player.UseCameraControls=oldCamera;
            player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;
        }
    }
}
