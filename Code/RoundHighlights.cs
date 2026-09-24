using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Local personal records and awards derived from existing match statistics.</summary>
public sealed class RoundHighlights
{
    public const string CookieKey="paintball-personal-bests-v1";
    private readonly string cookieKey;
    private string StorageKey=>cookieKey ?? CookieKey;
    public RoundHighlights(string cookieKey=CookieKey){this.cookieKey=cookieKey;}
    public sealed record Award(string Title,string Winners,string Detail);
    private Dictionary<string,int> records;
    private string recordedRound;
    private int? previousBest;
    public bool NewBest {get;private set;}
    public int? Best {get;private set;}
    public int? ResultPoints {get;private set;}
    public bool ObservedRound {get;private set;}
    public Dictionary<string,int> Snapshot()=>new(records ??= Game.Cookies.Get(StorageKey,new Dictionary<string,int>()));

    public static string RecordKey(string map,bool online,int minutes)=>$"{map}|{(online ? "multiplayer" : "solo")}|{minutes}";
    public int? Read(string key)
    {
        records??=Game.Cookies.Get(StorageKey,new Dictionary<string,int>());
        return records.TryGetValue(key,out var value) ? value : null;
    }
    public void WatchActiveRound()
    {
        ObservedRound=true;recordedRound=null;ResultPoints=null;NewBest=false;Best=null;
    }
    public void ClearObservation(){ObservedRound=false;recordedRound=null;ResultPoints=null;NewBest=false;Best=null;}
    public void Complete(string key,int serial,int score)
    {
        if(!ObservedRound)return;
        var token=$"{key}|{serial}";
        if(recordedRound!=token){previousBest=Read(key);recordedRound=token;ResultPoints=null;}
        if(ResultPoints==score)return;
        ResultPoints=score;
        NewBest=previousBest is null || score>previousBest;
        Best=previousBest is null ? score : Math.Max(previousBest.Value,score);
        // Replicated final scores can arrive after TIME UP. Reconcile against the
        // previous round's record, so a transient score is not kept as a false best.
        if(Read(key)!=Best){records[key]=Best.Value;Game.Cookies.Set(StorageKey,records);}
    }
    public static Award[] Awards(ArenaMatch.Standing[] standings)
    {
        var rows=standings ?? Array.Empty<ArenaMatch.Standing>();
        string Names(ArenaMatch.Standing[] winners)
        {
            var names=winners.Take(2).Select(x=>x.IsPlayer ? $"{x.Name} (YOU)" : x.Name);
            return string.Join(" / ",names)+(winners.Length>2 ? $" +{winners.Length-2} tied" : "");
        }
        Award Most(string title,Func<ArenaMatch.Standing,int> count,string unit)
        {
            int max=rows.Select(count).DefaultIfEmpty().Max();
            var winners=rows.Where(x=>count(x)==max).ToArray();
            return max>0 ? new(title,Names(winners),$"{max} {unit}{(winners.Length>1 ? " / TIED" : "")}") : new(title,"No winner yet",$"No {unit.ToLowerInvariant()}");
        }
        var active=rows.Where(x=>x.Landed+x.Received+x.Knockdowns+x.KnockedDown>0).ToArray();
        if(rows.Any(x=>x.Heist))return new[]{Most("TOP BANKER",x=>x.Banked,"BANKED TOKENS"),Most("TOP TAGGER",x=>x.Landed,"TAGS"),Most("MOST KNOCKDOWNS",x=>x.Knockdowns,"KNOCKDOWNS")};
        int least=active.Select(x=>x.Received).DefaultIfEmpty().Min();
        var clean=active.Where(x=>x.Received==least).ToArray();
        return new[]{Most("TOP TAGGER",x=>x.Landed,"TAGS"),
            active.Length>0 ? new Award("CLEANEST SUIT",Names(clean),$"{least} HITS TAKEN{(clean.Length>1 ? " / TIED" : "")}") : new Award("CLEANEST SUIT","No winner yet","No active competitors"),
            Most("MOST KNOCKDOWNS",x=>x.Knockdowns,"KNOCKDOWNS")};
    }
}
