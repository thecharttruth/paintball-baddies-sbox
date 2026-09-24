using Sandbox;
namespace PaintballBaddies;

public sealed partial class ArenaOpponent
{
    public string HeistTask {get;private set;}="";
    private bool UpdateHeistObjective()
    {
        var session=MultiplayerSession.Find(Scene);HeistTask="";
        if(!session.IsValid() || !session.IsHeist || !session.InRound || session.BankIndex<0 || !combatant.IsValid())return false;
        // Preserve immediate hit/cover reactions. After that brief escape, resume the objective.
        if(EvadingAfterHit && evadeAfterHit>1.8f)return false;
        bool bank=combatant.CarriedTokens>=3+CharacterIndex%4
            || (combatant.CarriedTokens>0 && (session.Remaining<18 || session.InBank(combatant)));
        Vector3 destination;
        if(bank)
        {
            // Different lanes avoid all five bots standing in exactly the same spot.
            var offset=Rotation.FromYaw(CharacterIndex*60).Forward*42;
            destination=session.BankPosition+offset;
            HeistTask="Bank tokens";
            if(session.InBank(combatant))
            {ReleaseTacticalCover();flankRemaining=0;agent.Stop();return true;}
            if(!Reachable(destination,6000,out destination,out _))return false;
        }
        else
        {
            var chip=Scene.GetAllComponents<HeistChip>().Where(c=>!c.Collected && c.Round==session.RoundId
                && (c.WorldPosition-WorldPosition).Length<600).OrderBy(c=>(c.WorldPosition-WorldPosition).Length).FirstOrDefault();
            if(!chip.IsValid() || !Reachable(chip.WorldPosition,900,out destination,out _))return false;
            HeistTask="Collect tokens";
        }
        ReleaseTacticalCover();flankRemaining=0;agent.MoveTo(destination);return true;
    }
}
