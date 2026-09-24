using Sandbox;
using System;
namespace PaintballBaddies;

// Opt-in, temporary editor fixture. Never placed in the playable scene.
// Keep fixture notes as ordinary comments: XML summaries on this partial type
// cause duplicate Description metadata during s&box's incremental generation.
public sealed partial class NetworkReview : Component
{
    public Dictionary<Guid,string> Reports {get;}=new();
    private int recoveryTaps;
    private float nextTap;
    private float nextReport,walkTime;
    public string SoundResult {get;private set;}="";
    private IDisposable walkHook;
    private IDisposable aimHook;
    private float aimTime;
    private float voiceTestTime;
    private bool voiceTest;
    private bool voiceWasDown;
    private bool aimPrevious,aimInjected;
    private float primaryTime;
    private bool primaryPrevious,primaryInjected;
    private bool controlled;
    private IDisposable carryAuditHook;
    public int CarrySamples {get;private set;}
    public float CarryMaxError {get;private set;}
    public string CarryWorst {get;private set;}="";
    private int ownerEchoSamples,ownerEchoVisuals,remoteBallVisuals,localPredictionVisuals;
    private readonly Dictionary<ArenaOpponent,bool> frozenBots=new();
    public void FreezeBots()
    {
        if(!MultiplayerSession.Authority)return;
        foreach(var bot in Scene.GetAllComponents<ArenaOpponent>())
        {
            if(!frozenBots.ContainsKey(bot))frozenBots[bot]=bot.CombatEnabled;
            bot.CombatEnabled=false;bot.Components.Get<NavMeshAgent>()?.Stop();
        }
    }
    protected override void OnStart()
    {
        carryAuditHook=Scene.AddHook(GameObjectSystem.Stage.FinishUpdate,200,AuditCarry,nameof(NetworkReview),"Measure final shield hand alignment");
        walkHook=Scene.AddHook(GameObjectSystem.Stage.StartFixedUpdate,-50,()=>
        {
            if(controlled){Input.AnalogMove=Vector3.Zero;Input.AnalogLook=Angles.Zero;}
            // Physical bindings are refreshed at both frame stages. Keep a
            // simulated focus hold alive through the fixed-step cover update.
            if(aimInjected)Input.SetAction("attack2",aimTime>0);
            if(soak && MultiplayerSession.Find(Scene)?.InRound==true)
            {
                Input.AnalogMove=RealTime.NowDouble%8<4 ? Vector3.Right : Vector3.Left;
                return;
            }
            if(walkTime<=0){if(controlled)Input.SetAction("forward",false);return;}
            walkTime-=Time.Delta;Input.AnalogMove=Vector3.Forward;Input.SetAction("forward",true);
        },nameof(NetworkReview),"Temporary owner movement test");
        aimHook=Scene.AddHook(GameObjectSystem.Stage.StartUpdate,-40,()=>
        {
            if(voiceTest)
            {
                bool voiceDown=voiceTestTime>0;Input.SetAction("voice",voiceDown);Input.SetLastAction("voice",voiceWasDown);
                voiceWasDown=voiceDown;voiceTestTime-=Time.Delta;
                if(voiceTestTime<=-.1f)voiceTest=false;
            }
            if(controlled)
            {
                Input.AnalogLook=Angles.Zero;Input.AnalogMove=Vector3.Zero;
                foreach(var action in new[]{"attack1","cover","vault","shoulder","reload","use"})Input.SetAction(action,false);
                if(!aimInjected){Input.SetAction("attack2",false);Input.SetLastAction("attack2",false);}
            }
            if(primaryInjected)
            {
                bool primaryDown=primaryTime>0;primaryTime-=Time.Delta;
                Input.SetAction("attack1",primaryDown);Input.SetLastAction("attack1",primaryPrevious);
                if(!primaryDown && !primaryPrevious)primaryInjected=false;
                primaryPrevious=primaryDown;
            }
            if(!aimInjected)return;
            bool down=aimTime>0;aimTime-=Time.Delta;
            Input.SetAction("attack2",down);Input.SetLastAction("attack2",aimPrevious);
            if(!down && !aimPrevious)aimInjected=false;
            aimPrevious=down;
        },nameof(NetworkReview),"Temporary owner aim press test");
    }
    protected override void OnDestroy()
    {
        foreach(var saved in frozenBots)if(saved.Key.IsValid())saved.Key.CombatEnabled=saved.Value;
        frozenBots.Clear();
        walkHook?.Dispose();aimHook?.Dispose();carryAuditHook?.Dispose();
        Input.ReleaseActions();Input.AnalogMove=Vector3.Zero;Input.AnalogLook=Angles.Zero;
    }
    private void AuditCarry()
    {
        if(!controlled)return;
        foreach(var ball in Scene.GetAllComponents<OpponentPaintball>())
        {
            bool visible=ball.Components.GetAll<ModelRenderer>(FindMode.EverythingInSelfAndDescendants).Any();
            bool ownerEcho=!MultiplayerSession.Authority && ball.Shooter?.Components.Get<NetworkPawn>() is {IsProxy:false};
            if(ownerEcho){ownerEchoSamples++;if(visible)ownerEchoVisuals++;}
            else if(visible)remoteBallVisuals++;
        }
        localPredictionVisuals+=Scene.GetAllComponents<TrailRenderer>().Count(x=>x.GameObject.Parent?.Name=="Paintball");
        foreach(var shield in Scene.GetAllComponents<ArenaShield>())
        {
            if(!shield.Owner.IsValid() || shield.Stowed)continue;
            var body=shield.Owner.Components.Get<CitizenPlayerPresentation>()?.FootstepRenderer
                ?? shield.Owner.Components.Get<CitizenOpponentPresentation>()?.FootstepRenderer;
            if(!body.IsValid() || !body.TryGetBoneTransform("hand_L",out var hand))continue;
            CarrySamples++;
            var error=(hand.Position-shield.WorldTransform.PointToWorld(new Vector3(-5,8,0))).Length;
            if(error>CarryMaxError)
            {
                CarryMaxError=error;
                CarryWorst=Json.Serialize(new {Owner=shield.Owner.GameObject.Name,Hand=hand.Position.ToString(),
                    BoneObject=body.GetBoneObject("hand_L")?.WorldPosition.ToString(),Shield=shield.WorldPosition.ToString(),
                    shield.GripError,Model=body.Model?.Name});
            }
        }
    }
    protected override void OnUpdate()
    {
        UpdatePerformance();
        if(recoveryTaps>0)
        {
            nextTap-=Time.Delta;
            if(nextTap<=0){nextTap=.16f;recoveryTaps--;MultiplayerSession.LocalPlayer(Scene)?.Components.Get<CloseCombat>()?.RegisterRecoveryPress();}
        }
        nextReport-=Time.Delta;
        if(nextReport>0)return;
        nextReport=soak ? 2f : .2f;
        var local=MultiplayerSession.LocalPlayer(Scene);
        var session=MultiplayerSession.Find(Scene);
        Report(Json.Serialize(new {Revision="network-3",Host=Networking.IsHost,Local=Connection.Local.Id,LocalSeat=local?.Components.Get<NetworkPawn>()?.Seat,
            Menu=Scene.GetAllComponents<PaintballControls>().FirstOrDefault()?.Open,Scene.TimeScale,HudInfo=HudReport(),
            session?.Minutes,session?.ActiveMinutes,Standings=session?.Standings,session?.InRound,session?.Status,session?.Remaining,session?.RoundId,session?.MarkerSoundAssignments,
            Players=Scene.GetAllComponents<NetworkPawn>().Select(x=>new{x.Seat,x.ConnectionId,x.IsProxy,Position=x.WorldPosition.ToString(),
                CanCover=x.Components.Get<CoverController>()?.CanEnter,VaultReason=x.Components.Get<VaultController>()?.BlockReason,Airborne=x.Components.Get<PlayerController>()?.IsAirborne,
                x.AcceptedShots,x.RejectedShots,x.ContactState,x.RecoveryRequests,x.RecoveryAccepted,x.Covered,x.Low,x.Peek,x.Vaulting,x.Aiming,x.Ducking,x.CoverName,
                BodyHeight=x.Components.Get<PlayerController>().BodyHeight,AcceptInput=x.Components.Get<PaintballMarker>().AcceptInput,
                FocusInput=!x.IsProxy && Input.Down("attack2"),LocalShots=x.Components.Get<PaintballMarker>()?.Shots,
                CoverFire=x.Components.Get<PaintballMarker>()?.CoverFireRequested,
                Input=x.Components.Get<PlayerController>().UseInputControls,Camera=x.Components.Get<PlayerController>().UseCameraControls,
                MotionEnabled=x.Components.Get<PlayerController>().Body.MotionEnabled,Velocity=x.Components.Get<PlayerController>().Velocity.ToString(),
                Ready=x.Components.Get<CitizenPlayerPresentation>()?.IsReady,
                Character=x.Components.Get<RosterSelection>()?.Selected,Name=x.Components.Get<PlayerProfile>()?.DisplayName,MarkerSound=CharacterMarkerSound.For(x.GameObject),
                Hits=x.Components.Get<PaintballCombatant>().PaintHits,Landed=x.Components.Get<PaintballCombatant>().HitsLanded,
                IncomingRemaining=x.Components.Get<PaintballCombatant>().DirectionIndicatorRemaining,
                IncomingBearing=x.Components.Get<PaintballCombatant>().HitDirectionAngle(Rotation.Identity),
                Knockdowns=x.Components.Get<PaintballCombatant>().KnockdownsLanded,KnockedDown=x.Components.Get<PaintballCombatant>().KnockdownsReceived,Points=x.Components.Get<PaintballCombatant>().Points,
                Feedbacks=x.Components.Get<CloseCombat>().ImpactFeedbacks,Feedback=x.Components.Get<CloseCombat>().FeedbackText,
                CameraImpulses=x.Components.Get<CloseCombat>().CameraImpulses,ShakeAmplitude=x.Components.Get<CloseCombat>().LastShakeAmplitude,
                Contacts=x.Components.Get<CloseCombat>().Contacts,Sounds=x.Components.Get<CloseCombat>().ImpactSounds,Attacking=x.Components.Get<CloseCombat>().Attacking,
                Paint=x.Components.Get<CharacterPaint>().Count,PaintAttachment=x.Components.Get<CharacterPaint>().AttachmentReview(),Down=x.Components.Get<CloseCombat>().Downed,Recovering=x.Components.Get<CloseCombat>().Recovering,Presses=x.Components.Get<CloseCombat>().RecoveryPresses,
                Muzzle=x.Components.Get<PaintballMarker>()?.Muzzle.ToString()}).ToArray(),
            Bots=Scene.GetAllComponents<NetworkBot>().Select(x=>new{x.Seat,x.IsProxy,Position=x.WorldPosition.ToString(),Shots=x.Components.Get<ArenaOpponent>().ShotsFired,
                Hits=x.Components.Get<PaintballCombatant>().PaintHits,Landed=x.Components.Get<PaintballCombatant>().HitsLanded,MarkerSound=CharacterMarkerSound.For(x.GameObject)}).ToArray(),
            Shields=Scene.GetAllComponents<ArenaShield>().Select(x=>new{x.Remaining,Owner=x.Owner.IsValid() && x.Owner.GameObject.IsValid() ? x.Owner.GameObject.Name : null,x.PaintCount,x.GripError,x.Stowed,Position=x.WorldPosition.ToString()}).ToArray(),
            ShieldFragments=Scene.GetAllComponents<ShieldBreakEffect>().Sum(x=>x.Fragments),CarrySamples,CarryMaxError,CarryWorst,Performance=PerformanceReport(),
            MissingParticleModels=Scene.GetAllComponents<ParticleModelRenderer>().SelectMany(x=>x.Choices).Where(x=>x.Model is null || x.Model.IsError).Select(x=>x.Model?.Name ?? "null").Distinct().ToArray(),
            SoundResult,WorldPaint=PaintImpactSystem.Find(Scene)?.ActiveCount,OldestPaint=PaintImpactSystem.Find(Scene)?.OldestDecal?.WorldPosition.ToString(),MissingModels=Scene.GetAllComponents<ModelRenderer>().Where(r=>r.Model?.IsError==true).Select(r=>r.GameObject.Name+":"+r.Model.Name).Distinct().Take(12).ToArray(),
            Balls=Scene.GetAllComponents<OpponentPaintball>().Count(),Hud=Scene.GetAllComponents<TrainingHud>().Count(),
            ProjectilePresentation=new{OwnerEchoSamples=ownerEchoSamples,OwnerEchoVisuals=ownerEchoVisuals,RemoteBallVisuals=remoteBallVisuals,LocalPredictionVisuals=localPredictionVisuals}}));
    }
    [Rpc.Host]
    public async void Report(string json)
    {
        var caller=Rpc.Caller.Id;
        await GameTask.MainThread();
        if(this.IsValid())Reports[caller]=json;
    }
    [Rpc.Broadcast(NetFlags.HostOnly)]
    public async void Command(string action,int seat,Vector3 point)
    {
        await GameTask.MainThread();
        if(!this.IsValid())return;
        var player=MultiplayerSession.LocalPlayer(Game.ActiveScene);
        if(player?.Components.Get<NetworkPawn>()?.Seat!=seat)return;
        var controls=Scene.GetAllComponents<PaintballControls>().FirstOrDefault();
        if(action=="test_lock"){controlled=true;CarrySamples=0;CarryMaxError=0;}
        if(action=="test_unlock"){controlled=false;Input.ReleaseActions();}
        if(action=="soak_start"){controlled=true;soak=true;BeginPerformance();}
        if(action=="soak_stop"){soak=false;controlled=false;player.Components.Get<PaintballMarker>().ReviewAimOverride=null;Input.ReleaseActions();}
        if(action=="close" && controls?.Open==true)controls.Toggle();
        if(action=="open" && controls?.Open==false)controls.Toggle();
        if(action=="hide_text" && controls?.ShowGameplayText==true)controls.ToggleGameplayText();
        if(action=="show_text" && controls?.ShowGameplayText==false)controls.ToggleGameplayText();
        if(action=="hide_board" && controls?.ShowScoreboard==true)controls.ToggleScoreboard();
        if(action=="show_board" && controls?.ShowScoreboard==false)controls.ToggleScoreboard();
        if(action=="voice_press"){voiceTest=true;voiceWasDown=false;voiceTestTime=1.2f;}
        if(action=="voice_toggle")PlayerVoiceChat.Toggle();
        if(action=="voice_mode")PlayerVoiceChat.ToggleMode();
        if(action=="move"){player.WorldPosition=point;player.Body.Velocity=Vector3.Zero;player.Body.Sleeping=false;player.Network.ClearInterpolation();}
        if(action=="look")player.EyeAngles=Rotation.LookAt(point-player.EyePosition).Angles();
        if(action=="cover")player.Components.Get<CoverController>().Activate(false);
        if(action=="vault")player.Components.Get<VaultController>().TryBegin();
        if(action=="aim")player.Components.Get<PaintballMarker>().ReviewAimOverride=point.x>0;
        if(action=="sound"){var sound=Sound.Play(CharacterMarkerSound.For(player.GameObject),player.WorldPosition);SoundResult=$"{sound?.Name}: {sound?.IsPlaying}";}
        if(action=="attempt_round")MultiplayerSession.Find(Scene).StartRound();
        if(action=="attempt_minutes")MultiplayerSession.Find(Scene).SetMinutes(point.x==0 ? 3 : (int)point.x);
        if(action=="attempt_mode")MultiplayerSession.Find(Scene).SelectMode(ArenaGameMode.PaintRush);
        if(action=="reject")player.Components.Get<NetworkPawn>().RequestShot(new Vector3(999999,0,0),new Vector3(999999,10,0),int.MaxValue);
        if(action=="screenshot")Game.TakeHighResScreenshot(1280,720);
        if(action=="walk")walkTime=2;
        if(action=="fire")player.Components.Get<PaintballMarker>().FireAt(point);
        if(action=="attack_tap"){primaryTime=.04f;primaryPrevious=false;primaryInjected=true;}
        if(action=="clear_paint")PaintImpactSystem.Find(Scene)?.Clear();
        if(action=="quit_game")controls?.QuitGame();
        if(action=="elbow")player.Components.Get<CloseCombat>().TryAttack();
        if(action=="attack_press" || action=="attack_hold"){primaryTime=action=="attack_hold" ? 1.8f : .45f;primaryPrevious=false;primaryInjected=true;}
        if(action=="focus_press"){aimTime=2f;aimPrevious=false;aimInjected=true;}
        if(action=="focus_fire"){aimTime=.9f;aimPrevious=false;aimInjected=true;primaryTime=.45f;primaryPrevious=false;primaryInjected=true;}
        if(action=="recover_burst"){recoveryTaps=5;nextTap=0;}
        if(action=="recover"){var c=player.Components.Get<CloseCombat>();SoundResult=$"ready:{c.ReadyToRecover} proxy:{c.IsProxy} sent:{c.RegisterRecoveryPress()}";}
        if(action=="name")player.Components.Get<PlayerProfile>().SaveName($"Test Player {seat+1}");
        if(action=="leave")Networking.Disconnect();
    }
}
