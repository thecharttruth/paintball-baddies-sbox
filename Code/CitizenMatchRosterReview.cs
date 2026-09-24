using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in real round lifecycle check for all six player choices.</summary>
public sealed class CitizenMatchRosterReview : Component
{
    [Property] public string CaptureId { get; set; }="";
    private RosterSelection roster;
    private ArenaMatch match;
    private CitizenOpponentPresentation[] bots;
    private int index,phase;
    private float elapsed,previousDuration;
    private bool previousCitizen,ready,identities,hidden;
    protected override void OnUpdate()
    {
        if(index>=6)return;
        if(!match.IsValid())
        {
            match=Scene.GetAllComponents<ArenaMatch>().FirstOrDefault();
            if(!match.IsValid() || !match.PlayerState.IsValid()) { match=null;return; }
            roster=match.Components.Get<RosterSelection>();
            previousCitizen=roster.UseCitizenCharacters;previousDuration=match.RoundDuration;
            roster.UseCitizenCharacters=true;match.RoundDuration=1.5f;
        }
        elapsed+=Time.Delta;
        if(phase==0)
        {
            if(!roster.Select(index))return;
            match.StartRound();elapsed=0;phase=1;
        }
        else if(phase==1 && elapsed>.6f)
        {
            bots=Scene.GetAllComponents<CitizenOpponentPresentation>().ToArray();
            ready=bots.Length==5 && bots.All(x=>x.IsReady && x.VisualActive);
            var actual=bots.Select(x=>x.Components.Get<ArenaOpponent>().CharacterIndex).OrderBy(x=>x).ToArray();
            identities=actual.SequenceEqual(Enumerable.Range(0,6).Where(x=>x!=index)) &&
                bots.All(x=>x.EquippedHelmet==CitizenRosterAssets.Helmet(x.Components.Get<ArenaOpponent>().CharacterIndex)
                    && x.EquippedSuit==CitizenRosterAssets.Suit(x.Components.Get<ArenaOpponent>().CharacterIndex));
            phase=2;
        }
        else if(phase==2 && !match.InRound)
        {
            hidden=bots.All(x=>x.IsValid() && !x.VisualActive);
            match.ReturnToTraining();elapsed=0;phase=3;
        }
        else if(phase==3 && elapsed>.2f)
        {
            Log.Info("CITIZEN_MATCH_ROSTER "+Json.Serialize(new {capture_id=CaptureId,index,ready,identities,hidden,
                removed=bots.All(x=>!x.IsValid()),players=Scene.GetAllComponents<CitizenPlayerPresentation>().Count()}));
            index++;phase=0;
        }
    }
    protected override void OnDestroy()
    {
        if(roster.IsValid()) { roster.UseCitizenCharacters=previousCitizen;roster.Select(0); }
        if(match.IsValid())match.RoundDuration=previousDuration;
    }
}
