using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Opt-in native geometry/action regression. Never saved in a playable scene.</summary>
public sealed class GearsControlsReview : Component
{
    public List<string> Results {get;}=new();
    public bool Done {get;private set;}
    public int Stage {get;private set;}
    PlayerController player;PaintballMarker marker;CloseCombat contact;CoverController cover;
    PaintballCombatant victim;GameObject obstacle;
    Vector3 oldPosition;Angles oldAngles;bool oldLook;float elapsed;int contacts,shots;
    IDisposable hook,fixedHook;bool primary,focus,coverPress,previousCover,previousPrimary,previousFocus;
    void Check(bool ok,string label){Results.Add((ok ? "PASS " : "FAIL ")+label);Log.Info("GEARS_CONTROLS "+Results.Last());}
    void Next(){Stage++;elapsed=0;primary=false;focus=false;}
    void Place(float target=42)
    {
        contact.ResetState();victim.Components.Get<CloseCombat>()?.ResetState();
        cover.Leave();cover.TestAim=false;cover.TestMovement=Vector3.Zero;
        player.WorldPosition=new(0,0,1202);player.EyeAngles=Angles.Zero;
        player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;
        victim.WorldPosition=new(target,0,1200);
        contacts=contact.Contacts;shots=marker.Shots;
    }
    protected override void OnStart()
    {
        Scene.GetAllComponents<PaintballControls>().First().StartPractice();
        player=MultiplayerSession.LocalPlayer(Scene);marker=player.Components.Get<PaintballMarker>();
        contact=player.Components.Get<CloseCombat>();cover=player.Components.Get<CoverController>();
        oldPosition=player.WorldPosition;oldAngles=player.EyeAngles;oldLook=player.UseLookControls;
        player.UseLookControls=false;
        TrainingRange.Box(GameObject,"Gears review platform",new(0,0,1198),new(750,700,4),Color.Gray,true);
        var target=new GameObject(GameObject){Name="Gears review target",WorldPosition=new(42,0,1200)};
        var hitbox=target.Components.Create<BoxCollider>();hitbox.Scale=new(28,28,65);hitbox.Center=new(0,0,32.5f);
        victim=target.Components.Create<PaintballCombatant>();victim.Team=91;victim.StayInMatch=true;
        hook=Scene.AddHook(GameObjectSystem.Stage.StartUpdate,-35,()=>
        {
            Input.AnalogMove=Vector3.Zero;Input.AnalogLook=Angles.Zero;
            Input.SetAction("attack1",primary);Input.SetLastAction("attack1",previousPrimary);previousPrimary=primary;
            Input.SetAction("attack2",focus);Input.SetLastAction("attack2",previousFocus);previousFocus=focus;
            Input.SetAction("cover",coverPress);Input.SetLastAction("cover",previousCover);previousCover=coverPress;
        },nameof(GearsControlsReview),"Temporary native action edges");
        fixedHook=Scene.AddHook(GameObjectSystem.Stage.StartFixedUpdate,-35,()=>{Input.SetAction("attack2",focus);Input.AnalogMove=Vector3.Zero;},nameof(GearsControlsReview),"Hold test focus through physics");
        Place();
    }
    protected override void OnUpdate()
    {
        if(Done)return;elapsed+=Time.Delta;
        switch(Stage)
        {
            case 0:
                if(elapsed<.4f)return;
                Check(contact.MeleeAvailable,"close frontal target advertises melee");focus=true;Stage=1;elapsed=0;break;
            case 1:
                if(elapsed<.4f)return;
                Check(contact.Contacts==contacts && !contact.Attacking && marker.Aiming,"right mouse focuses without striking");
                primary=true;Stage=2;elapsed=0;break;
            case 2:
                if(elapsed<.4f)return;
                Check(marker.Shots>shots && contact.Contacts==contacts,"focused left click fires at point blank");Next();break;
            case 3:
                if(elapsed<.4f)return;Place();primary=true;Stage=4;elapsed=0;break;
            case 4:
                if(elapsed<.7f)return;
                Check(contact.Contacts==contacts+1 && victim.Components.Get<CloseCombat>().Downed,"fresh close left click knocks down once");
                Check(marker.Shots==shots,"melee click emits no paintball");
                victim.Components.Get<CloseCombat>().ResetState();victim.WorldPosition=player.WorldPosition+Vector3.Forward*42;
                Stage=5;elapsed=0;break;
            case 5:
                if(elapsed<.8f)return;
                Check(contact.Contacts==contacts+1 && marker.Shots==shots,"held melee neither repeats nor becomes a shot");Next();break;
            case 6:
                if(elapsed<.3f)return;Place(100);primary=true;Stage=7;elapsed=0;break;
            case 7:
                if(elapsed<.25f)return;victim.WorldPosition=player.WorldPosition+Vector3.Forward*42;Stage=8;elapsed=0;break;
            case 8:
                if(elapsed<.5f)return;
                Check(marker.Shots>shots && contact.Contacts==contacts,"held shooting stays shooting when target approaches");Next();break;
            case 9:
                if(elapsed<.25f)return;Place();
                victim.WorldPosition=player.WorldPosition+Vector3.Forward*49;Check(!contact.MeleeAvailable,"target beyond 1.2m does not select melee");
                victim.WorldPosition=player.WorldPosition+Vector3.Left*42;Check(!contact.MeleeAvailable,"target beside player does not steal attack");
                victim.WorldPosition=player.WorldPosition-Vector3.Forward*42;Check(!contact.MeleeAvailable,"target behind player rejected");
                victim.WorldPosition=player.WorldPosition+Vector3.Forward*42;
                obstacle=TrainingRange.Box(GameObject,"Melee obstruction",new(21,0,1235),new(5,90,70),Color.Gray,true);
                Next();break;
            case 10:
                if(elapsed<.2f)return;
                Check(!contact.MeleeAvailable && !contact.TryAttack(),"solid wall prevents melee selection and hit");
                obstacle.Destroy();victim.WorldPosition=new(300,200,1200);
                obstacle=TrainingRange.Box(GameObject,"Angled cover",new(65,0,1236),new(20,160,72),Color.Gray,true);
                obstacle.Components.Create<CoverSurface>();player.EyeAngles=new(0,50,0);cover.TestInput=true;Next();break;
            case 11:
                if(elapsed<.25f)return;
                Check(cover.Activate(false) && cover.Attached,"forgiving cone finds cover missed by central ray");
                Check(cover.Activate(true) && !cover.Attached && !player.Components.Get<VaultController>().IsVaulting,"forward plus Space still leaves and never vaults");
                obstacle.WorldPosition=new(85,0,1236);player.WorldPosition=new(0,0,1202);player.EyeAngles=Angles.Zero;Next();break;
            case 12:
                if(elapsed<.25f)return;
                Check(!cover.RequestCover(false),"early cover press waits while outside reach");
                player.WorldPosition=new(10,0,1202);Next();break;
            case 13:
                if(elapsed<.12f)return;
                Check(cover.Attached,"buffered Space attaches after entering range");cover.Leave();
                player.WorldPosition=new(0,0,1202);player.EyeAngles=Angles.Zero;Next();break;
            case 14:
                if(elapsed<.25f)return;
                cover.RequestCover(false);player.EyeAngles=new(0,180,0);player.WorldPosition=new(10,0,1202);Next();break;
            case 15:
                if(elapsed<.25f)return;
                Check(!cover.Attached && player.UseInputControls,"changing direction cancels buffered cover");
                player.WorldPosition=new(0,0,1202);player.EyeAngles=Angles.Zero;obstacle.WorldPosition=new(180,0,1224);
                obstacle.LocalScale=new Vector3(20,160,48)/50;Next();break;
            case 16:
                player.WorldPosition=new(0,0,1202);player.Body.Velocity=Vector3.Forward*190;
                if(elapsed<.3f)return;
                player.Body.Velocity=Vector3.Forward*190;
                Check(cover.Activate(true)&&cover.Sliding,"running Space starts native slide");
                Check(!marker.FireAt(new(300,0,1240)),"slide cannot fire through traversal");Next();break;
            case 17:
                if(elapsed<.12f)return;
                Check(cover.Activate(false)&&!cover.Sliding&&player.UseInputControls,"second Space cancels slide and restores movement");
                player.WorldPosition=new(0,0,1202);player.EyeAngles=Angles.Zero;Next();break;
            case 18:
                player.WorldPosition=new(0,0,1202);player.Body.Velocity=Vector3.Forward*190;
                if(elapsed<.35f)return;
                player.Body.Velocity=Vector3.Forward*190;Check(cover.TrySlide(),"slide can restart after cancellation");
                cover.TestMovement=Vector3.Backward;Next();break;
            case 19:
                if(elapsed<.2f)return;
                Check(!cover.Sliding&&player.UseInputControls,"backward intent cancels slide without locking input");
                cover.TestMovement=Vector3.Zero;cover.Leave();Place();obstacle.WorldPosition=new(60,0,1222);
                victim.WorldPosition=new(300,200,1200);
                obstacle.LocalScale=new Vector3(20,160,44)/50;cover.TestInput=false;Next();break;
            case 20:
                if(elapsed<.3f)return;focus=true;Stage++;elapsed=0;break;
            case 21:
                if(elapsed<.07f)return;focus=false;Stage++;elapsed=0;break;
            case 22:
                if(elapsed<.4f)return;
                Check(!cover.Attached,"right click never takes cover");
                coverPress=true;Stage++;elapsed=0;break;
            case 23:
                if(elapsed<.5f)return;coverPress=false;
                Check(cover.Attached&&cover.LowCover&&player.IsDucking,"stationary Space takes crouched cover without movement");
                focus=true;Stage++;elapsed=0;break;
            case 24:
                if(elapsed<.5f)return;
                Check(cover.Attached&&cover.Peeking&&!player.IsDucking,"holding right mouse peeks from low cover");
                focus=false;Stage++;elapsed=0;break;
            case 25:
                if(elapsed<.5f)return;
                Check(cover.Attached&&!cover.Peeking&&player.IsDucking,"releasing right mouse returns to shelter without leaving");
                cover.Leave();Place();victim.WorldPosition=new(300,200,1200);focus=true;Stage++;elapsed=0;break;
            case 26:
                if(elapsed<.4f)return;
                Check(!cover.Attached&&marker.Aiming,"holding focus near cover does not attach");focus=false;Stage++;elapsed=0;break;
            case 27:
                if(elapsed<.2f)return;
                Check(!cover.Attached,"releasing a held focus does not attach");
                focus=primary=true;Stage++;elapsed=0;break;
            case 28:
                if(elapsed<.07f)return;focus=primary=false;Stage++;elapsed=0;break;
            case 29:
                if(elapsed<.2f)return;
                Check(!cover.Attached,"a quick focused shot does not take cover on release");
                obstacle.WorldPosition=new(250,0,1222);focus=true;Stage++;elapsed=0;break;
            case 30:
                if(elapsed<.07f)return;focus=false;Stage++;elapsed=0;break;
            case 31:
                if(elapsed<.2f)return;
                Check(!cover.Attached&&player.UseInputControls,"right tap on open ground leaves movement free");
                Place();victim.WorldPosition=new(300,200,1200);obstacle.WorldPosition=new(60,0,1222);
                player.WorldPosition=new(34.05f,0,1202);Stage++;elapsed=0;break;
            case 32:
                if(elapsed<.5f)return;coverPress=true;Stage++;elapsed=0;break;
            case 33:
                if(elapsed<.35f)return;coverPress=false;
                Check(cover.Attached&&cover.LowCover,"Space attaches at body-contact distance while stationary");
                Results.Add($"DIAGNOSTIC contact position={player.WorldPosition} grounded={player.IsOnGround}");
                Stage++;elapsed=0;break;
            case 34:
                if(elapsed<.2f)return;coverPress=true;Stage++;elapsed=0;break;
            case 35:
                if(elapsed<.2f)return;coverPress=false;
                Check(!cover.Attached&&player.UseInputControls,"second Space leaves contact cover and restores movement");
                obstacle.WorldPosition=new(60,0,1248);obstacle.LocalScale=new Vector3(20,160,96)/50;
                player.WorldPosition=new(34.05f,0,1202);Stage++;elapsed=0;break;
            case 36:
                if(elapsed<.5f)return;coverPress=true;Stage++;elapsed=0;break;
            case 37:
                if(elapsed<.35f)return;coverPress=false;
                Check(cover.Attached&&!cover.LowCover&&!player.IsDucking,"stationary Space takes standing cover at a tall wall");
                primary=focus=false;cover.Leave();player.WorldPosition=new(0,0,1202);obstacle.WorldPosition=new(280,0,1248);Next();break;
            case 38:
                if(elapsed<.35f)return;coverPress=true;Next();break;
            case 39:
                if(elapsed<.3f)return;coverPress=false;
                Check(cover.FreeCrouched&&player.IsDucking&&!cover.Attached,"Space crouches on open ground without attaching");
                Check(!player.Components.Get<VaultController>().IsVaulting,"open-ground Space never starts a vault");Next();break;
            case 40:
                if(elapsed<.5f)return;
                Check(cover.FreeCrouched&&player.IsDucking,"open-ground crouch remains after Space release");
                shots=marker.Shots;primary=true;Stage++;elapsed=0;break;
            case 41:
                if(elapsed<.45f)return;
                Check(marker.Shots>shots&&player.IsDucking,"open-ground crouching allows ordinary firing");
                primary=false;coverPress=true;Next();break;
            case 42:
                if(elapsed<.3f)return;coverPress=false;
                Check(!cover.FreeCrouched&&!player.IsDucking,"second open-ground Space stands up");
                cover.RequestCoverOrCrouch(false);Next();break;
            case 43:
                if(elapsed<.35f)return;
                obstacle.WorldPosition=new(60,0,1222);obstacle.LocalScale=new Vector3(20,160,44)/50;
                coverPress=true;Next();break;
            case 44:
                if(elapsed<.4f)return;coverPress=false;
                Check(cover.Attached&&cover.LowCover&&!cover.FreeCrouched,"nearby low cover takes priority over free crouch");
                Next();break;
            case 45:
                if(elapsed<.2f)return;coverPress=true;Next();break;
            case 46:
                if(elapsed<.3f)return;coverPress=false;
                Check(!cover.Attached&&!cover.FreeCrouched&&!player.IsDucking,"Space leaves low cover standing without an extra crouch toggle");
                Done=true;primary=focus=false;break;
        }
    }
    protected override void OnDestroy()
    {
        hook?.Dispose();fixedHook?.Dispose();Input.ReleaseActions();Scene.TimeScale=1;
        if(cover.IsValid()){cover.Leave();cover.TestInput=false;cover.TestMovement=Vector3.Zero;}
        contact?.ResetState();
        if(player.IsValid()){player.WorldPosition=oldPosition;player.EyeAngles=oldAngles;player.UseLookControls=oldLook;player.Body.Velocity=Vector3.Zero;player.WishVelocity=Vector3.Zero;}
    }
}
