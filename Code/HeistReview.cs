using Sandbox;
using Sandbox.UI;
using System;
namespace PaintballBaddies;

// Temporary private-match review helpers. Never placed in a saved scene.
public static class HeistReview
{
    public static object Snapshot(Scene scene)
    {
        var session=MultiplayerSession.Find(scene);
        var hud=scene.GetAllComponents<HeistHud>().FirstOrDefault();
        return new {Mode=session?.Mode.ToString(),ActiveMode=session?.ActiveMode.ToString(),session?.BankIndex,
            Bank=session?.BankPosition.ToString(),session?.BankSeconds,session?.IsHeist,
            Visuals=new {Banks=scene.GetAllComponents<HeistBankPresentation>().Count(),
                Lines=scene.GetAllComponents<HeistBankPresentation>().SelectMany(x=>x.Components.GetAll<LineRenderer>(FindMode.EverythingInSelfAndDescendants))
                    .Select(x=>new{x.GameObject.Name,First=x.VectorPoints.FirstOrDefault().ToString(),Origin=x.WorldPosition.ToString()}).ToArray(),
                Bursts=scene.GetAllComponents<HeistBankBurst>().Select(x=>new{x.Amount,x.Age}).ToArray(),
                Bloom=scene.GetAllComponents<Bloom>().Select(x=>new{x.Strength,x.Threshold}).ToArray()},
            Audio=new[]{"collect","deposit","spill","bank_move"}.Select(name=>{
                var sound=HeistAudio.Get(name);
                return new{Name=name,Loaded=sound is not null,sound?.Sounds,SourceAvailable=FileSystem.Mounted.FileExists($"sounds/heist/{name}.vsnd_c"),
                    CompiledAudioAvailable=FileSystem.Mounted.FileExists($"sounds/heist/{name}.vsnd_c")};
            }).ToArray(),
            Wallets=scene.GetAllComponents<PaintballCombatant>().Where(a=>a.Components.Get<NetworkPawn>().IsValid() || a.Components.Get<NetworkBot>().IsValid())
                .Select(a=>new{a.GameObject.Name,Seat=a.Components.Get<NetworkPawn>()?.Seat ?? a.Components.Get<NetworkBot>()?.Seat,
                    a.CarriedTokens,a.BankedTokens,a.BankProgress,a.Points,a.TokenEvent,a.TokenAmount,a.TokenSerial,
                    a.TokenNotice,a.TokenNoticeRemaining,a.TokenSoundsPlayed,a.LastTokenSound,a.LastTokenSoundStarted,Task=a.Components.Get<ArenaOpponent>()?.HeistTask}).ToArray(),
            Chips=scene.GetAllComponents<HeistChip>().Select(c=>new{c.Amount,c.Round,c.Collected,Position=c.WorldPosition.ToString()}).ToArray(),
            Labels=hud?.Panel?.Descendants.OfType<Label>().Select(l=>l.Text).ToArray(),
            Boxes=hud?.Panel?.Descendants.Where(p=>p.HasClass("heist-wallet") || p.HasClass("bank-guide")).Select(p=>new{Kind=p.HasClass("heist-wallet") ? "wallet" : "bank-guide",Rect=p.Box.Rect.ToString()}).ToArray()};
    }
    public static void Wallet(Scene scene,int seat,int carried,int banked)
    {
        var session=MultiplayerSession.Find(scene);
        if(!session.IsValid() || session.PublicLobby || !MultiplayerSession.Authority)return;
        var actor=session.Players.FirstOrDefault(p=>p.Seat==seat)?.Components.Get<PaintballCombatant>();
        if(!actor.IsValid())return;
        actor.CarriedTokens=Math.Max(0,carried);actor.BankedTokens=Math.Max(0,banked);actor.BankProgress=0;
    }
    public static string[] Rules(Scene scene)
    {
        var session=MultiplayerSession.Find(scene);
        if(!session.IsValid() || session.PublicLobby || !session.IsHeist || !session.InRound || session.BankIndex<0)
            return new[]{"FAIL requires initialized private Heist round"};
        var checks=new List<string>();
        void Check(bool pass,string label){checks.Add((pass ? "PASS " : "FAIL ")+label);}
        var a=new GameObject(true,"Heist rule attacker"){NetworkMode=NetworkMode.Never,WorldPosition=session.BankPosition};
        var b=new GameObject(true,"Heist rule victim"){NetworkMode=NetworkMode.Never,WorldPosition=session.BankPosition+Vector3.Right*160};
        var attacker=a.Components.Create<PaintballCombatant>();var victim=b.Components.Create<PaintballCombatant>();
        attacker.Team=900;victim.Team=901;attacker.StayInMatch=victim.StayInMatch=true;
        var initialDrops=scene.GetAllComponents<HeistChip>().ToArray();
        try
        {
            Check(!victim.RegisterHit(victim.Team,attacker:attacker) && attacker.CarriedTokens==0,"same-team hit creates no tokens");
            victim.CarriedTokens=5;victim.BankedTokens=7;
            Check(victim.RegisterHit(attacker.Team,attacker:attacker),"accepted body hit");
            Check(attacker.CarriedTokens==1 && victim.CarriedTokens==4 && victim.BankedTokens==7,"body hit earns 1, spills 1, bank stays safe");
            var drops=scene.GetAllComponents<HeistChip>().Where(c=>!initialDrops.Contains(c)).ToArray();
            Check(drops.Sum(c=>c.Amount)==1,"spill creates exactly one unit of collectible value");
            victim.CarriedTokens=5;
            Check(victim.RegisterKnockdown(attacker) && victim.CarriedTokens==2 && victim.BankedTokens==7,"odd-number knockdown spills rounded-up half without touching bank");
            Check(attacker.CarriedTokens==1,"knockdown awards no duplicate Rush points as Heist tokens");
            attacker.AcceptHits=false;int before=attacker.CarriedTokens;
            Check(!attacker.RegisterHit(victim.Team,attacker:victim) && attacker.CarriedTokens==before,"disabled combatant rejects hits");
            attacker.AcceptHits=true;attacker.CarriedTokens=6;
            a.WorldPosition=session.BankPosition+Vector3.Right*500;
            session.AdvanceDeposit(attacker,10);Check(attacker.CarriedTokens==6 && attacker.BankedTokens==0,"outside bank cannot deposit");
            a.WorldPosition=session.BankPosition;
            session.AdvanceDeposit(attacker,.5f);Check(attacker.BankProgress>0 && attacker.BankedTokens==0,"deposit requires hold time");
            a.WorldPosition=session.BankPosition+Vector3.Up*100;
            session.AdvanceDeposit(attacker,2);Check(attacker.BankProgress==0 && attacker.BankedTokens==0,"another floor cannot bank and leaving resets progress");
            a.WorldPosition=session.BankPosition;
            session.AdvanceDeposit(attacker,1.26f);Check(attacker.BankedTokens==6 && attacker.CarriedTokens==0,"full hold moves carried value to bank");
            session.AdvanceDeposit(attacker,3);Check(attacker.BankedTokens==6,"remaining in ring cannot bank twice");
            attacker.CarriedTokens=20;
            Check(attacker.Points==6,"only banked value counts as score");
            session.AdvanceDeposit(attacker,.5f);attacker.RegisterHit(victim.Team,attacker:victim);session.AdvanceDeposit(attacker,2);
            Check(attacker.BankProgress==0 && attacker.BankedTokens==6,"actual body hit interrupts deposit");
            attacker.ResetPaintHits();Check(attacker.CarriedTokens==0 && attacker.BankedTokens==0 && attacker.BankProgress==0,"round reset clears wallet and progress");
            var first=new ArenaMatch.Standing("A",9,2,true,Carried:999,Banked:3,Heist:true);
            var second=new ArenaMatch.Standing("B",1,8,false,Banked:4,Heist:true);
            Check(first.Points<second.Points,"carrying and hits cannot outrank more banked tokens");
            Check(new ArenaMatch.Standing("Rush",9,2,true,2,1).Points==9,"Paint Rush scoring unchanged");
        }
        finally
        {
            foreach(var c in scene.GetAllComponents<HeistChip>().Where(c=>!initialDrops.Contains(c)).ToArray())c.GameObject.Destroy();
            a.Destroy();b.Destroy();
        }
        return checks.ToArray();
    }
}
