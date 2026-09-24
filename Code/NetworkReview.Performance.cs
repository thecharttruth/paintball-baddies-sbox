using Sandbox;
using System;
namespace PaintballBaddies;

// Opt-in scripted load, never part of ordinary player/AI control.
public sealed partial class NetworkReview
{
    private bool soak;
    private double perfStart,perfPrevious,perfSum,perfMax,soakNextShot,soakNextRecovery;
    private int perfFrames,perfSlow33,perfSlow50,perfSlow100;
    private readonly int[] perfBins=new int[1001];
    private Vector3 perfLastPosition;
    private double perfTravel;
    private void BeginPerformance()
    {
        perfStart=perfPrevious=RealTime.NowDouble;perfSum=perfMax=perfTravel=0;
        perfFrames=perfSlow33=perfSlow50=perfSlow100=0;Array.Clear(perfBins);
        perfLastPosition=MultiplayerSession.LocalPlayer(Scene)?.WorldPosition ?? Vector3.Zero;
    }
    private void UpdatePerformance()
    {
        if(!soak)return;
        double now=RealTime.NowDouble,ms=(now-perfPrevious)*1000;perfPrevious=now;
        if(now-perfStart>5)
        {
            perfFrames++;perfSum+=ms;perfMax=Math.Max(perfMax,ms);
            perfBins[Math.Clamp((int)(ms*4),0,1000)]++;
            if(ms>33.333)perfSlow33++;if(ms>50)perfSlow50++;if(ms>100)perfSlow100++;
        }
        var player=MultiplayerSession.LocalPlayer(Scene);
        if(!player.IsValid())return;
        float travel=(player.WorldPosition-perfLastPosition).Length;
        if(travel<150)perfTravel+=travel;
        perfLastPosition=player.WorldPosition;
        if(MultiplayerSession.Find(Scene)?.InRound!=true)return;
        var contact=player.Components.Get<CloseCombat>();
        if(contact?.Incapacitated==true)
        {
            if(contact.Downed && contact.ReadyToRecover && now>=soakNextRecovery)
            {soakNextRecovery=now+.16;contact.RegisterRecoveryPress();}
            return;
        }
        var targets=Scene.GetAllComponents<PaintballCombatant>().Where(x=>x.GameObject!=player.GameObject
            && (x.Components.Get<NetworkPawn>().IsValid() || x.Components.Get<NetworkBot>().IsValid())).OrderBy(x=>x.Team).ToArray();
        if(targets.Length==0)return;
        var target=targets[(int)(now/4)%targets.Length];
        var targetPoint=target.EyePosition-Vector3.Up*12;
        player.EyeAngles=Rotation.LookAt(targetPoint-player.EyePosition).Angles();
        var marker=player.Components.Get<PaintballMarker>();marker.ReviewAimOverride=true;
        if(now<soakNextShot)return;
        soakNextShot=now+.32;
        marker.FireAt(targetPoint);
    }
    private double Percentile(double p)
    {
        if(perfFrames==0)return 0;
        int wanted=(int)Math.Ceiling(perfFrames*p),total=0;
        for(int i=0;i<perfBins.Length;i++){total+=perfBins[i];if(total>=wanted)return (i+1)*.25;}
        return perfMax;
    }
    private object PerformanceReport()=>new {Active=soak,Seconds=soak ? RealTime.NowDouble-perfStart : 0,Frames=perfFrames,
        MeanMs=perfFrames>0 ? perfSum/perfFrames : 0,P95Ms=Percentile(.95),P99Ms=Percentile(.99),MaxMs=perfMax,
        Over33Ms=perfSlow33,Over50Ms=perfSlow50,Over100Ms=perfSlow100,Travel=perfTravel,
        Width=Screen.Width,Height=Screen.Height,Renderers=Scene.GetAllComponents<ModelRenderer>().Count()};
}
