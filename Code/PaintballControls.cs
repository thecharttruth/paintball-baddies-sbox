using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Player-local bindings feeding the native controller and action system.</summary>
public sealed class PaintballControls : Component
{
    public bool Open { get; private set; }
    public bool Welcome { get; private set; }
    public bool ShowGameplayText { get; private set; } = true;
    public bool ShowScoreboard { get; private set; } = true;
    public bool EditingName { get; set; }
    public string NameDraft { get; set; } = "Player";
    public PlayerProfile Profile => MultiplayerSession.LocalPlayer(Scene)?.Components.Get<PlayerProfile>();
    public Dictionary<string,int> ObservedPresses { get; } = new();
    public string Capturing { get; private set; }
    public string LastCaptureButton { get; private set; }
    public string Notice { get; private set; } = "Select an action, then press its new key. Esc or M opens this menu.";
    public Dictionary<string,string> Bindings { get; private set; } = new();
    public Dictionary<string,string> Titles { get; private set; } = new();
    private Dictionary<string,string> defaults=new();
    private IDisposable updateHook,fixedHook;
    private float previousScale;
    private int captureDelay;
    private readonly QuickTurnGesture quickTurn=new();
    public bool QuickTurning => quickTurn.Turning;
    public int QuickTurnsCompleted => quickTurn.Completed;
    // Opt-in native review: prime the same gesture, then let normal input
    // eligibility, view updates and animation run on subsequent game frames.
    public void BeginQuickTurnReview()
    {
        quickTurn.Reset();
        quickTurn.Advance(true,0,true);
        quickTurn.Advance(false,0,true);
        quickTurn.Advance(true,0,true);
    }
    private readonly HashSet<string> blockedUntilRelease=new();
    private static readonly string[] Keys=("abcdefghijklnopqrstuvwxyz0123456789".Select(c=>c.ToString()))
        .Concat(new[]{"space","shift","ctrl","alt","tab","enter","backspace","uparrow","downarrow","leftarrow","rightarrow","home","end","ins","del","pgup","pgdn","capslock","kp_enter","kp_del","kp_plus","kp_minus","kp_multiply","kp_divide","mouse1","mouse2","mouse3","mouse4","mouse5","f2","f3","f4","f5","f7","f8","f9","f10","f11","f12"})
        .Concat(Enumerable.Range(0,10).Select(i=>$"kp_{i}")).ToArray();
    protected override void OnStart()
    {
        var localPlayer = MultiplayerSession.LocalPlayer(Scene);
        if (localPlayer.IsValid() && !Profile.IsValid()) localPlayer.Components.Create<PlayerProfile>();
        NameDraft = PlayerProfile.CleanName(Game.Cookies.Get("paintball-player-name", "Player"));
        ShowGameplayText = Game.Cookies.Get("paintball-show-gameplay-text", true);
        ShowScoreboard = Game.Cookies.Get("paintball-show-scoreboard", true);
        var excluded=new[]{"run","jump","slot7","slot8","slot9","slot0","slotprev","slotnext","view","drop","score","menu","chat","voicetransmit"};
        foreach(var action in Input.GetActions())
        {
            var name=action.Name.ToLowerInvariant();
            if(excluded.Contains(name))continue;
            defaults[name]=action.KeyboardCode.ToLowerInvariant();
            Titles[name]=action.Title ?? action.Name;
        }
        defaults["shoulder"]="q";Titles["shoulder"]="Switch shoulder";
        defaults["roundlength"]="t";Titles["roundlength"]="Round length";
        defaults["togglehud"]="h";Titles["togglehud"]="Show / hide gameplay text (keep scoreboard)";
        defaults["togglescoreboard"]="j";Titles["togglescoreboard"]="Show / hide scoreboard";
        defaults["voice"]="v";Titles["voice"]="Voice microphone (tap on/off; optional hold mode)";
        defaults["vault"]="x";
        Titles["attack1"]="Fire / cover peek / close strike";Titles["attack2"]="Focus (optional) / rapid recovery";Titles["duck"]="Hold crouch (optional)";
        defaults["flashlight"]="b";Titles["flashlight"]="Play / restart round (host)";Titles["use"]="Reset practice targets";
        Titles["cover"]="Cover nearby / toggle crouch in the open";
        for(int i=0;i<6;i++)Titles[$"slot{i+1}"]=$"Select {RosterSelection.Names[i]}";
        var saved=Game.Cookies.Get("paintball-bindings-v3",new Dictionary<string,string>());
        bool migrate=saved.Count==0;
        if(saved.Count==0)
        {
            saved=Game.Cookies.Get("paintball-bindings-v2",new Dictionary<string,string>());
            if(saved.Count==0){saved=Game.Cookies.Get("paintball-bindings-v1",new Dictionary<string,string>());saved["flashlight"]="b";}
            if(saved.GetValueOrDefault("vault")=="v")
                saved["vault"]=saved.Any(x=>x.Key!="vault" && x.Value=="x")
                    ? Keys.First(k=>k!="v" && !defaults.Values.Contains(k) && !saved.Values.Contains(k)) : "x";
            // Voice has not shipped before v3; reserve its new default without overwriting other custom actions.
            saved.Remove("voice");
        }
        foreach(var pair in defaults)Bindings[pair.Key]=saved.TryGetValue(pair.Key,out var key) && Keys.Contains(key) ? key : pair.Value;
        // Preserve older custom H/J bindings when adding these new actions.
        foreach(var action in new[]{"togglehud","togglescoreboard","voice"})
            if(!saved.ContainsKey(action) && Bindings.Any(x=>x.Key!=action && x.Value==Bindings[action]))
                Bindings[action]=Keys.First(key=>!Bindings.Any(x=>x.Key!=action && x.Value==key));
        if(migrate)Game.Cookies.Set("paintball-bindings-v3",Bindings);
        updateHook=Scene.AddHook(GameObjectSystem.Stage.StartUpdate,-100,UpdateControls,nameof(PaintballControls),"Player bindings");
        fixedHook=Scene.AddHook(GameObjectSystem.Stage.StartFixedUpdate,-100,ApplyMappings,nameof(PaintballControls),"Native movement bindings");
        Welcome = !ArenaMatch.RoundAfterReload.HasValue;
        if (Welcome) Toggle();
    }
    public string Label(string action) => Bindings.TryGetValue(action.ToLowerInvariant(),out var key) ? KeyLabel(key) : Input.GetButtonOrigin(action);
    private static string KeyLabel(string key) => key switch
    {
        "uparrow" => "UP ARROW", "downarrow" => "DOWN ARROW",
        "leftarrow" => "LEFT ARROW", "rightarrow" => "RIGHT ARROW",
        _ when key.StartsWith("kp_") => $"NUM {key[3..].ToUpperInvariant()}",
        _ => key.ToUpperInvariant()
    };
    public void SetMovementLayout(bool arrows)
    {
        var actions=new[]{"forward","backward","left","right"};
        var keys=arrows ? new[]{"uparrow","downarrow","leftarrow","rightarrow"} : new[]{"w","s","a","d"};
        for(int i=0;i<actions.Length;i++)Rebind(actions[i],keys[i]);
        Notice=arrows ? "Arrow-key movement saved. Other controls stay available below." : "WASD movement saved. Other controls stay available below.";
    }
    public void ToggleGameplayText()
    {
        ShowGameplayText=!ShowGameplayText;
        Game.Cookies.Set("paintball-show-gameplay-text",ShowGameplayText);
    }
    public void ToggleScoreboard()
    {
        ShowScoreboard=!ShowScoreboard;
        Game.Cookies.Set("paintball-show-scoreboard",ShowScoreboard);
    }
    public void Toggle()
    {
        quickTurn.Reset();
        if(Open && MultiplayerSession.Find(Scene)?.QuickPlaying==true)MultiplayerSession.Find(Scene)?.CancelPlaySearch();
        Open=!Open;Capturing=null;
        if(Open){previousScale=Scene.TimeScale;if(!MultiplayerSession.Online)Scene.TimeScale=0;}
        else
        {
            Profile?.SaveName(NameDraft);
            NameDraft = Profile?.DisplayName ?? "Player";
            Welcome=false;EditingName=false;
            foreach(var key in Keys)if(Input.Keyboard.Down(key))blockedUntilRelease.Add(key);
            Scene.TimeScale=previousScale;Input.ReleaseActions();
        }
    }
    public void Capture(string action){Capturing=action;captureDelay=2;Notice="Press a key or mouse button. Escape cancels.";}
    // Focused UI receives navigation keys before the gameplay keyboard poll.
    // Normalize UI event names to the engine's physical keyboard binding names.
    public bool CaptureButton(string key, bool pressed)
    {
        if(!Open || Capturing is null || !pressed)return false;
        LastCaptureButton=key;
        key=(key ?? "").ToLowerInvariant();
        if(key.StartsWith("key_"))key=key[4..];
        if(key.StartsWith("pad_") && key.Length==5 && char.IsDigit(key[4]))key=$"kp_{key[4]}";
        key=key switch
        {
            "up" or "arrowup" => "uparrow", "down" or "arrowdown" => "downarrow",
            "left" or "arrowleft" => "leftarrow", "right" or "arrowright" => "rightarrow",
            "pad_enter" => "kp_enter", "pad_decimal" => "kp_del",
            "pad_plus" => "kp_plus", "pad_minus" => "kp_minus",
            "pad_multiply" => "kp_multiply", "pad_divide" => "kp_divide",
            "insert" => "ins", "delete" => "del", "pageup" => "pgup", "pagedown" => "pgdn",
            "return" => "enter", "control" or "lctrl" or "rctrl" => "ctrl",
            "mouseleft" => "mouse1", "mouseright" => "mouse2", "mousemiddle" => "mouse3",
            "mouseback" => "mouse4", "mouseforward" => "mouse5",
            "lcontrol" or "rcontrol" => "ctrl", "lshift" or "rshift" => "shift",
            "lalt" or "ralt" => "alt", var name => name
        };
        if(key=="escape"){Capturing=null;Input.EscapePressed=false;Notice="Binding cancelled.";return true;}
        if(key=="m"){Notice="M is reserved for the menu. Choose another key; Escape cancels.";return true;}
        if(captureDelay>0 && key.StartsWith("mouse"))return true;
        if(!Rebind(Capturing,key))Notice="That key is not supported. Try another key; Escape cancels.";
        return true;
    }
    public bool Rebind(string action,string key)
    {
        key=key.ToLowerInvariant();
        if(!Bindings.ContainsKey(action) || !Keys.Contains(key))return false;
        quickTurn.Reset();
        var old=Bindings[action];
        var conflict=Bindings.FirstOrDefault(x=>x.Key!=action && x.Value==key);
        if(conflict.Key is not null)Bindings[conflict.Key]=old;
        Bindings[action]=key;Game.Cookies.Set("paintball-bindings-v3",Bindings);
        Notice=conflict.Key is null ? "Binding saved." : $"Swapped with {Titles[conflict.Key]} to avoid a conflict.";
        Capturing=null;return true;
    }
    public void ResetBindings()
    {
        quickTurn.Reset();
        Bindings=new(defaults);Game.Cookies.Set("paintball-bindings-v3",Bindings);Capturing=null;Notice="Default controls restored.";
    }
    public void StartMatch()
    {
        if(!MultiplayerSession.Online){MultiplayerSession.Find(Scene)?.QuickPlay();return;}
        if(Open)Toggle();
        MultiplayerSession.LocalPlayer(Scene)?.Components.Get<ArenaMatch>()?.StartRound();
    }
    public void ResumeGame(){if(Open)Toggle();}
    public void QuitGame()
    {
        MultiplayerSession.Find(Scene)?.CancelPlaySearch();
        Game.Close();
    }
    public void StartPractice()
    {
        if(MultiplayerSession.Online){if(Open)Toggle();return;}
        if(Open)Toggle();
        var match=MultiplayerSession.LocalPlayer(Scene)?.Components.Get<ArenaMatch>();
        if(match?.Status != "TRAINING")match?.ReturnToTraining(endCurrentRound:true);
    }
    public void RestartGame()
    {
        if(MultiplayerSession.Online){if(Open)Toggle();MultiplayerSession.Find(Scene)?.StartRound();return;}
        var options=new SceneLoadOptions { ShowLoadingScreen=true, IsAdditive=false };
        if(!options.SetScene("scenes/neon_foundry.scene"))
        {
            Notice="Could not load the arena. Try again.";
            return;
        }
        ArenaMatch.RoundAfterReload=MultiplayerSession.LocalPlayer(Scene)?.Components.Get<ArenaMatch>()?.RoundDuration ?? 180;
        if(Open)Toggle();
        Scene.TimeScale=1;
        Input.ReleaseActions();
        Scene.Load(options);
    }
    private void UpdateControls()
    {
        foreach(var key in Keys.Concat(new[]{"m"}))
            if(Input.Keyboard.Pressed(key))ObservedPresses[key]=ObservedPresses.GetValueOrDefault(key)+1;
        if(Input.EscapePressed)
        {
            if(Capturing is not null){Capturing=null;Notice="Binding cancelled.";}else Toggle();
            Input.EscapePressed=false;
        }
        else if(Input.Keyboard.Pressed("m") && Capturing is not null)CaptureButton("m",true);
        else if(Input.Keyboard.Pressed("m") && !(Open && EditingName))Toggle();
        if(Open && Capturing is not null && --captureDelay<=0)
            foreach(var key in Keys)if(Input.Keyboard.Pressed(key)){CaptureButton(key,true);break;}
        ApplyMappings();
        if(!Open && Input.Pressed("togglehud"))ToggleGameplayText();
        if(!Open && Input.Pressed("togglescoreboard"))ToggleScoreboard();
        UpdateQuickTurn();
    }
    private void UpdateQuickTurn()
    {
        var player=Profile?.Components.Get<PlayerController>();
        var cover=player?.Components.Get<CoverController>();
        var key=Bindings.GetValueOrDefault("backward","s");
        bool down=Input.Keyboard.Down(key)&&!blockedUntilRelease.Contains(key);
        bool allowed=!Open&&!Input.UsingController&&player.IsValid()&&player.UseLookControls&&player.UseCameraControls
            && (player.UseInputControls || cover?.Attached==true)
            && player.Components.Get<PaintballMarker>()?.AcceptInput==true
            && cover?.Sliding!=true&&player.Components.Get<VaultController>()?.IsVaulting!=true;
        float turn=quickTurn.Advance(down,Time.Delta,allowed);
        if(turn!=0)
        {
            cover?.Leave();
            var angles=player.EyeAngles;
            angles.yaw=MathF.IEEERemainder(angles.yaw+turn,360);
            player.EyeAngles=angles;
        }
        SuppressConsumedBackward();
    }
    private void SuppressConsumedBackward()
    {
        if(!quickTurn.ConsumeBackward)return;
        Input.SetAction("backward",false);
        Input.AnalogMove=Input.AnalogMove.WithX(MathF.Max(0,Input.AnalogMove.x));
    }
    private void ApplyMappings()
    {
        if(!Open && Input.UsingController)return;
        blockedUntilRelease.RemoveWhere(key=>!Input.Keyboard.Down(key));
        foreach(var pair in Bindings)
        {
            var down=!Open && !blockedUntilRelease.Contains(pair.Value) && Input.Keyboard.Down(pair.Value);
            if(pair.Key=="duck" && !Open && Profile?.Components.Get<CoverController>()?.FreeCrouched==true)down=true;
            Input.SetAction(pair.Key,down);
            Input.SetLastAction(pair.Key,down && !Input.Keyboard.Pressed(pair.Value));
        }
        Input.AnalogMove=Open ? Vector3.Zero :
            Vector3.Forward*((Input.Down("forward") ? 1 : 0)-(Input.Down("backward") ? 1 : 0))+
            Vector3.Left*((Input.Down("left") ? 1 : 0)-(Input.Down("right") ? 1 : 0));
        if(Open)Input.AnalogLook=Angles.Zero;
        SuppressConsumedBackward();
    }
    protected override void OnDestroy()
    {
        updateHook?.Dispose();fixedHook?.Dispose();
        if(Open)Scene.TimeScale=previousScale;
    }
    public static string Display(Scene scene,string action)=>scene.GetAllComponents<PaintballControls>().FirstOrDefault()?.Label(action) ?? Input.GetButtonOrigin(action);
}
