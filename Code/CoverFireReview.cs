using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Opt-in native input/pose/geometry check; never saved into the arena.</summary>
public sealed class CoverFireReview : Component
{
    public List<string> Results {get;}=new();
    public bool Done {get;private set;}
    public int Stage {get;private set;}
    public int Layout {get;private set;}
    PlayerController player; PaintballMarker marker; CoverController cover;
    GameObject obstacle; IDisposable hook,fixedHook;
    Vector3 oldPosition; Angles oldAngles; bool oldLook;
    bool primary,focus,previousPrimary,previousFocus;
    float elapsed; int shots;
    void Check(bool ok,string label)=>Results.Add((ok?"PASS ":"FAIL ")+Layout+" "+label);
    void Next(){Stage++;elapsed=0;}
    protected override void OnStart()
    {
        Scene.GetAllComponents<PaintballControls>().First().StartPractice();
        player=MultiplayerSession.LocalPlayer(Scene);marker=player.Components.Get<PaintballMarker>();cover=player.Components.Get<CoverController>();
        oldPosition=player.WorldPosition;oldAngles=player.EyeAngles;oldLook=player.UseLookControls;player.UseLookControls=false;
        TrainingRange.Box(GameObject,"Cover firing platform",new(0,0,1198),new(900,900,4),Color.Gray,true);
        obstacle=TrainingRange.Box(GameObject,"Cover firing obstacle",new(60,0,1222),new(20,160,44),Color.Gray,true);
        obstacle.Components.Create<CoverSurface>();
        hook=Scene.AddHook(GameObjectSystem.Stage.StartUpdate,-35,()=>
        {
            Input.AnalogMove=Vector3.Zero;Input.AnalogLook=Angles.Zero;
            Input.SetAction("attack1",primary);Input.SetLastAction("attack1",previousPrimary);previousPrimary=primary;
            Input.SetAction("attack2",focus);Input.SetLastAction("attack2",previousFocus);previousFocus=focus;
        },nameof(CoverFireReview),"Temporary cover fire action edges");
        fixedHook=Scene.AddHook(GameObjectSystem.Stage.StartFixedUpdate,-35,()=>
        {Input.AnalogMove=Vector3.Zero;Input.SetAction("attack1",primary);Input.SetAction("attack2",focus);},nameof(CoverFireReview),"Temporary held cover actions");
        Place();
    }
    void Place()
    {
        primary=focus=false;cover.Leave();cover.TestInput=false;marker.ReviewAimOverride=null;marker.AcceptInput=true;
        player.Components.Get<CloseCombat>().ResetState();
        obstacle.WorldPosition=new(60,0,Layout==0?1222:1248);
        obstacle.LocalScale=new Vector3(20,Layout==3?400:160,Layout==0?44:96)/50;
        player.WorldPosition=new(24,Layout==1?-62:Layout==2?62:0,1202);player.EyeAngles=Angles.Zero;
        player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;elapsed=0;Stage=0;
    }
    protected override void OnUpdate()
    {
        if(Done)return;elapsed+=Time.Delta;
        switch(Stage)
        {
            case 0:
                if(elapsed<.5f)return;
                Check(cover.TryEnter()&&cover.Attached,"stationary attachment");Next();break;
            case 1:
                if(elapsed<.6f)return;
                Check(!cover.Peeking,"starts sheltered");shots=marker.Shots;primary=true;Next();break;
            case 2:
                if(elapsed<.04f)return;primary=false;Next();break;
            case 3:
                if(elapsed<1.65f)return;
                Check(marker.Shots-shots==(Layout==3?0:1),$"quick left click emits {(Layout==3?0:1)} shot (actual {marker.Shots-shots})");
                Check(cover.Attached&&!cover.Peeking&&!marker.CoverFireRequested,"returns to shelter after tap / timeout");
                if(Layout==3){Done=true;return;}
                shots=marker.Shots;primary=true;Next();break;
            case 4:
                if(elapsed<1.5f)return;
                Check(marker.Shots-shots>=2,$"held left fires without right mouse ({marker.Shots-shots})");
                Check(cover.Peeking&&marker.Aiming,"held attack exposes rifle pose");
                Check(!PaintballFlight.Trace(Scene,player.EyePosition,marker.Muzzle,player.GameObject).Hit,"barrel clears eye segment");
                Check(PaintballFlight.Trace(Scene,marker.Muzzle,marker.Muzzle+Vector3.Forward*96,player.GameObject).GameObject!=obstacle,"barrel clears cover");
                primary=false;shots=marker.Shots;Next();break;
            case 5:
                if(elapsed<.8f)return;
                Check(marker.Shots==shots&&cover.Attached&&!cover.Peeking,"release stops fire and hides without leaving");
                focus=true;Next();break;
            case 6:
                if(elapsed<.9f)return;
                Check(cover.Peeking&&marker.Aiming&&marker.Shots==shots,"optional right focus peeks without firing");
                focus=false;Next();break;
            case 7:
                if(elapsed<.7f)return;
                primary=true;shots=marker.Shots;Next();break;
            case 8:
                if(elapsed<.025f)return;
                primary=false;marker.AcceptInput=false;Next();break;
            case 9:
                if(elapsed<1.3f)return;
                Check(marker.Shots==shots&&!marker.CoverFireRequested,"input lock cancels queued shot");
                marker.AcceptInput=true;Layout++;Place();break;
        }
    }
    protected override void OnDestroy()
    {
        hook?.Dispose();fixedHook?.Dispose();Input.ReleaseActions();
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;cover.TestAim=false;}
        if(marker.IsValid()){marker.AcceptInput=true;marker.ReviewAimOverride=null;}
        if(player.IsValid()){player.WorldPosition=oldPosition;player.EyeAngles=oldAngles;player.UseLookControls=oldLook;player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;}
    }
}
