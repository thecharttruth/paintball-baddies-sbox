using Sandbox;
namespace PaintballBaddies;
public sealed class CitizenRosterReview : Component
{
    [Property] public string CaptureId { get; set; }="";
    private CitizenPlayerPresentation presentation;
    private RosterSelection roster;
    private float elapsed;
    private int index=-1;
    private bool waiting;
    private bool previousCitizen;
    protected override void OnUpdate()
    {
        elapsed+=Time.Delta;
        if(!presentation.IsValid())
        {
            if(elapsed<.3f)return;
            if(!roster.IsValid())
            {
                roster=Scene.GetAllComponents<RosterSelection>().First();
                previousCitizen=roster.UseCitizenCharacters;
                roster.UseCitizenCharacters=true;
            }
            presentation=roster.Components.Get<CitizenPlayerPresentation>();
            if(!presentation.IsValid())return;
        }
        if(elapsed<.5f || index>=6)return;
        elapsed=0;
        if(!waiting)
        {
            index++;
            if(index>=6)return;
            roster.Select(index);waiting=true;
        }
        else
        {
            Log.Info("CITIZEN_ROSTER_REVIEW "+Json.Serialize(new {capture_id=CaptureId,index,
                selected=roster.Selected,expected=CitizenRosterAssets.Suit(index),actual=presentation.EquippedSuit,
                helmet=presentation.EquippedHelmet,expected_helmet=CitizenRosterAssets.Helmet(index),ready=presentation.IsReady}));
            waiting=false;
        }
    }
    protected override void OnDestroy()
    {
        if(roster.IsValid()) { roster.UseCitizenCharacters=previousCitizen;roster.Select(0); }
    }
}
