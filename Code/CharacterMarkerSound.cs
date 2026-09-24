using Sandbox;
using System;
namespace PaintballBaddies;
/// <summary>One marker voice per seat, reshuffled by the round authority.</summary>
public static class CharacterMarkerSound
{
    public const int Count=5;
    public static string For(int profile)=>$"sounds/paintball/selected/shot_{Math.Clamp(profile,0,Count-1)}.sound";
    public static int Profile(int assignments,int seat)=>assignments<0 ? Math.Clamp(seat,0,5)%Count
        : Math.Clamp((assignments>>(Math.Clamp(seat,0,5)*3))&7,0,Count-1);
    public static int Roll(int previous=-1)
    {
        // Six seats, five sounds: every sound is represented, only one repeats.
        // Reject assignments that give any seat its previous round's sound.
        for(int attempt=0;attempt<128;attempt++)
        {
            var bag=new[]{0,1,2,3,4,Game.Random.Int(0,Count-1)};
            for(int i=bag.Length-1;i>0;i--){int j=Game.Random.Int(0,i);(bag[i],bag[j])=(bag[j],bag[i]);}
            if(previous>=0 && Enumerable.Range(0,6).Any(i=>bag[i]==Profile(previous,i)))continue;
            int packed=0;for(int i=0;i<6;i++)packed|=bag[i]<<(i*3);
            return packed;
        }
        // Bounded fallback preserves both coverage and the no-repeat guarantee.
        int fallback=0;for(int i=0;i<6;i++)fallback|=((Profile(previous,i)+1)%Count)<<(i*3);
        return fallback;
    }
    public static string For(GameObject actor)
    {
        int seat=actor.Components.Get<NetworkPawn>()?.Seat ?? actor.Components.Get<NetworkBot>()?.Seat
            ?? actor.Components.Get<ArenaOpponent>()?.CharacterIndex ?? actor.Components.Get<RosterSelection>()?.Selected ?? 0;
        int assignments=MultiplayerSession.Online ? MultiplayerSession.Find(actor.Scene)?.MarkerSoundAssignments ?? -1
            : actor.Scene.GetAllComponents<ArenaMatch>().FirstOrDefault(x=>!x.IsProxy)?.MarkerSoundAssignments ?? -1;
        return For(Profile(assignments,seat));
    }
}
