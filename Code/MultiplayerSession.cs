using Sandbox;
using Sandbox.Network;
using System;
namespace PaintballBaddies;

// Native lobby, connection ownership and host-owned six-seat match.
// Ordinary comment avoids duplicate Description metadata across generated partial declarations.
public sealed partial class MultiplayerSession : Component, Component.INetworkListener
{
    public static MultiplayerSession Find(Scene scene)=>scene.GetAllComponents<MultiplayerSession>().FirstOrDefault();
    public static bool Online=>Networking.IsActive;
    public static bool Authority=>!Online || Networking.IsHost;
    public static PlayerController LocalPlayer(Scene scene)=>scene.GetAllComponents<PlayerController>()
        .FirstOrDefault(x=>!x.IsProxy && (!Online || x.Components.Get<NetworkPawn>().IsValid()));
    [Sync(SyncFlags.FromHost)] public bool InRound { get; private set; }
    [Sync(SyncFlags.FromHost)] public float Remaining { get; private set; }
    [Sync(SyncFlags.FromHost)] public int Minutes { get; private set; }=3;
    [Sync(SyncFlags.FromHost)] public int ActiveMinutes { get; private set; }=3;
    [Sync(SyncFlags.FromHost)] public string Status { get; private set; }="LOBBY";
    [Sync(SyncFlags.FromHost)] public int RoundId { get; private set; }
    [Sync(SyncFlags.FromHost)] public int MarkerSoundAssignments { get; private set; }=-1;
    public const string Protocol="pb-beta-3";
    [Sync(SyncFlags.FromHost)] public bool PublicLobby {get;private set;}=true;
    public string Notice { get; private set; }="";
    public string JoinAddress { get; set; }="";
    public List<LobbyInformation> Lobbies { get; private set; }=new();
    private bool registered;
    private bool hadNetworkSession;
    private float reconcileTime;
    private readonly HashSet<Guid> disconnected=new();
    private static readonly Vector3[] Spawns={new(-630,0,4),new(570,-400,4),new(570,0,4),new(570,400,4),new(-650,1250,4),new(650,1250,4)};
    public IEnumerable<NetworkPawn> Players=>Scene.GetAllComponents<NetworkPawn>().Where(x=>x.GameObject.Active);
    public IEnumerable<NetworkBot> Bots=>Scene.GetAllComponents<NetworkBot>().Where(x=>x.GameObject.Active);
    public ArenaMatch.Standing[] Standings=>Scene.GetAllComponents<PaintballCombatant>()
        .Where(x=>x.Components.Get<NetworkPawn>().IsValid() || x.Components.Get<NetworkBot>().IsValid())
        .Select(x=>new ArenaMatch.Standing(x.Components.Get<PlayerProfile>()?.DisplayName ??
            RosterSelection.Names[x.Components.Get<ArenaOpponent>()?.CharacterIndex ?? 0],x.HitsLanded,x.PaintHits,
            x.GameObject==LocalPlayer(Scene)?.GameObject,x.KnockdownsLanded,x.KnockdownsReceived,x.Components.Get<NetworkBot>().IsValid(),
            x.CarriedTokens,x.BankedTokens,IsHeist)).OrderByDescending(x=>x.Points).ThenBy(x=>IsHeist ? 0 : x.Received).ThenBy(x=>x.Name).ToArray();

    protected override void OnStart()
    {
        if(Authority && MarkerSoundAssignments<0)MarkerSoundAssignments=CharacterMarkerSound.Roll();
        if(!PaintImpactSystem.Find(Scene).IsValid())Components.Create<PaintImpactSystem>();
        if(Online)TrainingRange.CreateLocalHud(Scene);
    }
    public void Host(bool publicGame=true)
    {
        if(Online)return;
        CancelPlaySearch();
        var controls=Scene.GetAllComponents<PaintballControls>().FirstOrDefault();
        controls?.StartPractice();
        Minutes=(int)(LocalPlayer(Scene)?.Components.Get<ArenaMatch>()?.RoundDuration ?? 180)/60;
        try
        {
            PublicLobby=publicGame;
            Networking.CreateLobby(new LobbyConfig{Name=$"Paintball Baddies / {ArenaMap.DisplayTitle(Scene)}",MaxPlayers=6,Hidden=false,Privacy=publicGame ? LobbyPrivacy.Public : LobbyPrivacy.FriendsOnly,
                DestroyWhenHostLeaves=true});
            Networking.SetData("pb_protocol",Protocol);
            Networking.SetData("pb_access",publicGame ? "public" : "friends");
        }
        catch(Exception){Notice="Could not create a lobby. Check the Steam connection and try again.";return;}
        Scene.GetAllComponents<TrainingRange>().FirstOrDefault()?.SetTargetsVisible(false);
        foreach(var old in Scene.GetAllComponents<PlayerController>().Where(x=>!x.Components.Get<NetworkPawn>().IsValid()).ToArray())old.GameObject.Destroy();
        foreach(var shield in Scene.GetAllComponents<ArenaShield>().ToArray())shield.GameObject.Destroy();
        Notice=publicGame ? "Starting your public match. AI fills empty seats; people can join during play."
            : "Starting your friends-only match. AI fills empty seats.";
    }
    public void Join(ulong id)
    {
        if((Online && !CanQuickPlay) || Networking.IsConnecting)return;
        if(id==0){Notice="Enter a valid host Steam ID or lobby ID.";return;}
        CancelPlaySearch();
        Scene.GetAllComponents<PaintballControls>().FirstOrDefault()?.StartPractice();
        Notice="Connecting to the host...";
        Networking.Connect(id);
    }
    public void JoinTyped()
    {
        if(ulong.TryParse(JoinAddress.Trim(),out var id)){Join(id);return;}
        Notice="Enter the host Steam ID or a lobby ID.";
    }
    public void Leave()=>Game.Disconnect();
    public bool AcceptConnection(Connection channel,ref string reason)
    {
        if(Players.Count()>=6){reason="All six player seats are occupied.";return false;}
        return true;
    }
    public void OnActive(Connection channel)
    {
        if(!Networking.IsHost)return;
        disconnected.Remove(channel.Id);
        SpawnPlayer(channel);
    }
    public void OnDisconnected(Connection channel)
    {
        if(!Networking.IsHost)return;
        disconnected.Add(channel.Id);
        foreach(var pawn in Players.Where(x=>x.ConnectionId==channel.Id).ToArray())pawn.GameObject.Destroy();
        reconcileTime=0;
    }
    private void SpawnPlayer(Connection channel)
    {
        if(Players.Any(x=>x.ConnectionId==channel.Id))return;
        if(channel!=Connection.Host)channel.CanSpawnObjects=false;
        if(Players.Count()>=6)return;
        var occupied=Players.Select(x=>x.Seat).ToArray();
        // The host rolls once from characters not held by another human.
        // Seat replication keeps outfit, paint, AI replacement and spawning consistent.
        var available=Enumerable.Range(0,RosterSelection.Names.Length).Where(x=>!occupied.Contains(x)).ToArray();
        if(available.Length==0)return;
        int seat=available[Game.Random.Int(0,available.Length-1)];
        foreach(var bot in Bots.Where(x=>x.Seat==seat).ToArray())bot.GameObject.Destroy();
        var spawn=ArenaMap.PlayerSpawn(Scene,seat,Spawns[seat]);
        var go=GameObject.Clone("prefabs/network_baddie.prefab",new Transform(spawn),startEnabled:false,name:$"Player {seat+1}");
        go.Enabled=true;
        var pawn=go.Components.Get<NetworkPawn>();pawn.Seat=seat;pawn.ConnectionId=channel.Id;
        go.Components.Get<PaintballCombatant>().Team=seat+10;
        go.Components.Get<PaintballCombatant>().AcceptHits=InRound || Status=="LOBBY";
        go.Components.Get<PlayerProfile>().DisplayName=PlayerProfile.CleanName(channel.DisplayName);
        go.NetworkMode=NetworkMode.Object;go.Enabled=true;go.Network.SetOwnerTransfer(OwnerTransfer.Fixed);go.NetworkSpawn(channel);
        if(InRound)pawn.ResetForRound(spawn,RoundId);
        reconcileTime=0;
    }
    protected override void OnUpdate()
    {
        TickHeist();
        if(Online)hadNetworkSession=true;
        else if(hadNetworkSession && !Networking.IsConnecting)
        {
            // Client AI is simulated by its host. Once that connection ends,
            // exit the orphaned scene rather than leave stationary replicas.
            hadNetworkSession=false;
            Log.Info("PAINTBALL_CONNECTION_ENDED returning to game menu");
            Game.Disconnect();
            return;
        }
        if(!Online || !Networking.IsHost)return;
        if(!registered)
        {
            registered=true;
            foreach(var old in Scene.GetAllComponents<PlayerController>().Where(x=>!x.Components.Get<NetworkPawn>().IsValid()).ToArray())old.GameObject.Destroy();
            Scene.GetAllComponents<TrainingRange>().FirstOrDefault()?.SetTargetsVisible(false);
            // Platform-created lobbies need the same discovery data as in-game hosts.
            Networking.SetData("pb_protocol",Protocol);
            Networking.SetData("pb_access",PublicLobby ? "public" : "friends");
            Networking.SetData("pb_mode",ModeKey(Mode));
            Networking.SetData("pb_minutes",Minutes.ToString());
            GameObject.NetworkMode=NetworkMode.Object;GameObject.Network.SetOwnerTransfer(OwnerTransfer.Fixed);GameObject.NetworkSpawn();
        }
        foreach(var connection in Connection.All.Where(x=>x.IsActive && !disconnected.Contains(x.Id)))SpawnPlayer(connection);
        // Hosting is asynchronous. Start only after the local pawn and welcome
        // UI have initialized, including lobbies created by s&box's game browser.
        // RoundId keeps time-up/results and subsequent restarts host-controlled.
        if(RoundId==0 && Status=="LOBBY")
        {
            var host=Players.FirstOrDefault(x=>x.ConnectionId==Connection.Host?.Id);
            var controls=Scene.GetAllComponents<PaintballControls>().FirstOrDefault();
            if(controls.IsValid() && !controls.Open && host.IsValid()
                && host.Components.Get<CitizenPlayerPresentation>()?.IsReady==true
                && host.Components.Get<ArenaMatch>()?.PlayerState.IsValid()==true)
                BeginRound();
        }
        reconcileTime-=Time.Delta;
        if(reconcileTime<=0){reconcileTime=1;ReconcileBots();EnsureShields();}
        if(!InRound)return;
        var previous=Remaining;Remaining=MathF.Max(0,Remaining-Time.Delta);
        if(ArenaMatch.CountdownCue(previous,Remaining) is {} cue)NetworkEffects.Sound($"sounds/paintball/{cue}.sound",Vector3.Zero,Guid.Empty);
        if(Remaining<=0)
        {
            InRound=false;Status="TIME UP";
            foreach(var chip in Scene.GetAllComponents<HeistChip>().ToArray())chip.GameObject.Destroy();
            foreach(var state in Scene.GetAllComponents<PaintballCombatant>())state.AcceptHits=false;
            foreach(var pawn in Players)pawn.EndRound();
            foreach(var bot in Bots){bot.Components.Get<NavMeshAgent>()?.Stop();bot.Components.Get<ArenaOpponent>().CombatEnabled=false;}
            foreach(var ball in Scene.GetAllComponents<OpponentPaintball>().ToArray())ball.GameObject.Destroy();
        }
    }
    [Rpc.Host]
    public async void SetMinutes(int minutes)
    {
        var caller=Rpc.Caller;
        await GameTask.MainThread();
        if(!this.IsValid() || caller!=Connection.Host || !ArenaMatch.SupportsRoundMinutes(minutes))return;
        Minutes=minutes;
        Networking.SetData("pb_minutes",Minutes.ToString());
    }
    [Rpc.Host]
    public async void StartRound()
    {
        var caller=Rpc.Caller;
        await GameTask.MainThread();
        if(!this.IsValid() || caller!=Connection.Host)return;
        BeginRound();
    }
    private void BeginRound()
    {
        if(!Networking.IsHost)return;
        MarkerSoundAssignments=CharacterMarkerSound.Roll(MarkerSoundAssignments);
        RoundId++;ActiveMinutes=Minutes;ActiveMode=Mode;Remaining=ActiveMinutes*60;Status=ModeTitle(ActiveMode);InRound=true;
        StartHeistRound();
        foreach(var bot in Bots.ToArray())bot.GameObject.Destroy();
        foreach(var ball in Scene.GetAllComponents<OpponentPaintball>().ToArray())ball.GameObject.Destroy();
        foreach(var shield in Scene.GetAllComponents<ArenaShield>().ToArray())shield.GameObject.Destroy();
        foreach(var pawn in Players)
        {
            pawn.Components.Get<PaintballCombatant>().ResetPaintHits();
            pawn.ResetForRound(ArenaMap.PlayerSpawn(Scene,pawn.Seat,Spawns[pawn.Seat]),RoundId);
        }
        Scene.NavMesh.IsEnabled=true;Scene.NavMesh.IncludeStaticBodies=true;Scene.NavMesh.IncludeKeyframedBodies=false;
        Scene.NavMesh.AgentHeight=67;Scene.NavMesh.AgentRadius=17;Scene.NavMesh.SetDirty();
        ReconcileBots();EnsureShields();
        Notice="AI fills empty seats. Joining players replace AI; departing players are replaced by AI.";
        Log.Info($"PAINTBALL_ROUND_STARTED round={RoundId} humans={Players.Count()} ai={Bots.Count()} minutes={Minutes}");
        NetworkEffects.ClearPaint();NetworkEffects.Sound("sounds/paintball/round_start.sound",Vector3.Zero,Guid.Empty);
    }
    private void ReconcileBots()
    {
        if(!InRound)return;
        var humanSeats=Players.Select(x=>x.Seat).ToArray();
        foreach(var bot in Bots.Where(x=>humanSeats.Contains(x.Seat)).ToArray())bot.GameObject.Destroy();
        foreach(int seat in Enumerable.Range(0,6).Where(x=>!humanSeats.Contains(x)))
        {
            if(Bots.Any(x=>x.Seat==seat))continue;
            var go=new GameObject(false,$"Opponent {RosterSelection.Names[seat].ToLowerInvariant()}"){WorldPosition=ArenaMap.PlayerSpawn(Scene,seat,Spawns[seat])};
            go.Components.Create<NetworkBot>().Seat=seat;
            var state=go.Components.Create<PaintballCombatant>();state.Team=seat+10;state.StayInMatch=true;
            go.Components.Create<CloseCombat>();go.Components.Create<CharacterPaint>();go.Components.Create<CharacterVoice>();
            var bot=go.Components.Create<ArenaOpponent>();bot.Character=RosterSelection.Names[seat].ToLowerInvariant();bot.CombatEnabled=true;bot.ContestMode=true;
            go.Components.Create<CitizenOpponentPresentation>();
            go.NetworkMode=NetworkMode.Object;go.Enabled=true;go.Network.SetOwnerTransfer(OwnerTransfer.Fixed);go.NetworkSpawn();
        }
    }
    private void EnsureShields()
    {
        for(int i=Scene.GetAllComponents<ArenaShield>().Count();i<2;i++)
        {
            var go=new GameObject(false,"Arena shield");go.Components.Create<ArenaShield>();
            go.NetworkMode=NetworkMode.Object;go.Enabled=true;go.Network.SetOwnerTransfer(OwnerTransfer.Fixed);go.NetworkSpawn();
        }
    }
}
