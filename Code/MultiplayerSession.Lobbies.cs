using Sandbox;
using Sandbox.Network;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace PaintballBaddies;

public sealed partial class MultiplayerSession
{
    public bool Searching {get;private set;}
    public bool QuickPlaying {get;private set;}
    public bool CanQuickPlay=>!Online || (Networking.IsHost && RoundId==0 && Players.Count()<=1);
    public int LobbyRevision {get;private set;}
    private CancellationTokenSource lobbySearch;
    private int searchVersion;
    private int playVersion;

    public static bool CompatibleLobby(LobbyInformation lobby)=>lobby.Get("pb_protocol","")==Protocol;
    public static string LobbyLabel(LobbyInformation lobby)
        =>$"{lobby.Name} / {(lobby.Get("pb_mode","")=="paint_heist" ? "PAINT HEIST" : "PAINT RUSH")} / {lobby.Members}/{lobby.MaxMembers} PEOPLE / {lobby.Get("pb_minutes","3")} MIN"
        +(lobby.IsFull ? " / FULL" : !CompatibleLobby(lobby) ? " / DIFFERENT BUILD" : "");

    public async void RefreshLobbies()=>await RefreshLobbiesAsync();
    public async Task<bool> RefreshLobbiesAsync()
    {
        if(Searching || !CanQuickPlay || Networking.IsConnecting)return false;
        int version=++searchVersion;
        Searching=true;LobbyRevision++;Notice="Looking for games...";
        using var search=new CancellationTokenSource(TimeSpan.FromSeconds(12));
        lobbySearch=search;
        try
        {
            var found=await Networking.QueryLobbies(search.Token);
            await GameTask.MainThread();
            if(!IsValid || search.IsCancellationRequested || version!=searchVersion)return false;
            Lobbies=found.Where(x=>!x.IsHidden && x.OwnerId!=Connection.Local.SteamId.ValueUnsigned)
                .OrderBy(x=>x.IsFull || !CompatibleLobby(x)).ThenByDescending(x=>x.Members).ToList();
            Notice=Lobbies.Count==0 ? "No matches found. Play starts one with AI, ready for people to join."
                : "Choose a game. Full lobbies and different builds cannot be joined.";
            return true;
        }
        catch(OperationCanceledException){await GameTask.MainThread();if(IsValid && version==searchVersion)Notice="Search timed out. Check Steam and try Play again.";}
        catch(Exception){await GameTask.MainThread();if(IsValid && version==searchVersion)Notice="Cannot reach matches. Check Steam and try Play again.";}
        finally {if(IsValid && version==searchVersion){Searching=false;LobbyRevision++;lobbySearch=null;}}
        return false;
    }
    // Use the engine's lobby directory and connection transport, with game-specific
    // compatibility, public-access and self-host filters before joining.
    public static IEnumerable<LobbyInformation> PlayCandidates(IEnumerable<LobbyInformation> lobbies,ulong localOwner)
        =>lobbies.Where(x=>!x.IsHidden && !x.IsFull && x.Members>0 && CompatibleLobby(x)
            && x.Get("pb_access","")=="public" && x.OwnerId!=localOwner && x.LobbyId!=0)
            .OrderByDescending(x=>x.Members).ThenBy(x=>x.LobbyId);
    public async void QuickPlay()
    {
        if(QuickPlaying || Searching || Networking.IsConnecting)return;
        var controls=Scene.GetAllComponents<PaintballControls>().FirstOrDefault();
        if(!CanQuickPlay){controls?.ResumeGame();return;}
        if(controls.IsValid() && !controls.Open)controls.Toggle();
        int request=++playVersion;
        QuickPlaying=true;
        bool success=await RefreshLobbiesAsync();
        await GameTask.MainThread();
        if(!IsValid || !QuickPlaying || request!=playVersion)return;
        QuickPlaying=false;LobbyRevision++;
        if(!success)return;
        // A person may have joined our platform-created lobby while searching.
        // Keep their match rather than moving its host to a different lobby.
        if(!CanQuickPlay){controls?.ResumeGame();return;}
        var candidates=PlayCandidates(Lobbies,Connection.Local.SteamId.ValueUnsigned).Where(x=>x.Get("pb_mode","paint_rush")==ModeKey(Mode)).ToArray();
        if(candidates.Length>0)JoinLobby(candidates[0]);
        else if(Online)controls?.ResumeGame();
        else Host();
    }
    public void CancelPlaySearch()
    {
        lobbySearch?.Cancel();lobbySearch=null;searchVersion++;playVersion++;
        Searching=false;QuickPlaying=false;LobbyRevision++;Notice="Search cancelled.";
    }
    public void JoinLobby(LobbyInformation lobby)
    {
        if(lobby.IsFull){Notice="That lobby is full. Refresh to find another game.";return;}
        if(!CompatibleLobby(lobby)){Notice="That lobby uses a different build. Update the game and refresh.";return;}
        Join(lobby.LobbyId);
    }
    protected override void OnDestroy()=>CancelPlaySearch();
}
