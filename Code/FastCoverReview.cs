using Sandbox;
namespace PaintballBaddies;
public sealed class FastCoverReview : Component
{
    PlayerController player;CoverController cover;float elapsed,peekTime;int stage;float peekHold;
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(stage==0 && elapsed>1)
        {
            player=Scene.GetAllComponents<PlayerController>().First();cover=player.Components.Get<CoverController>();
            cover.Leave();cover.TestInput=true;player.UseLookControls=false;player.WorldPosition=new(-10,0,2);player.EyeAngles=new(0,0,0);stage=1;elapsed=0;
        }
        else if(stage==1&&elapsed>.5f){Log.Info("FAST_COVER attach="+cover.TryEnter());cover.TestAim=true;stage=2;elapsed=0;}
        else if(stage==2)
        {
            if(cover.Peeking)peekHold+=Time.Delta;
            if(peekHold>.8f || elapsed>3)
            {
                peekTime=elapsed;Log.Info("FAST_COVER "+Json.Serialize(new{cover.Peeking,Seconds=peekTime,Standing=!player.IsDucking,Position=player.WorldPosition.ToString(),MarkerAlignment=Vector3.Dot(player.Components.Get<PaintballMarker>().PresentationAnchor.WorldRotation.Forward,player.EyeAngles.ToRotation().Forward)}));cover.TestAim=false;elapsed=0;stage=3;
            }
        }
        else if(stage==3&&elapsed>1){Log.Info("FAST_RETURN "+Json.Serialize(new{cover.Attached,cover.Peeking,Position=player.WorldPosition.ToString()}));stage=4;}
    }
    protected override void OnDestroy(){if(cover.IsValid()){cover.Leave();cover.TestInput=false;}if(player.IsValid())player.UseLookControls=true;}
}
