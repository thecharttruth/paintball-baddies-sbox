using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Local preferences around the engine's owner-controlled voice transmitter.</summary>
public sealed class PlayerVoiceChat : Component
{
    public const string PreferenceKey="paintball-voice-chat-enabled";
    public static bool ChatEnabled => Game.Cookies.Get(PreferenceKey,true);
    public static void Toggle() => Game.Cookies.Set(PreferenceKey,!ChatEnabled);
    public static bool HoldToTalk => Game.Cookies.Get("paintball-voice-hold-to-talk",false);
    public static void ToggleMode() => Game.Cookies.Set("paintball-voice-hold-to-talk",!HoldToTalk);
    public bool MicOpen {get;private set;}
    private bool previousHoldMode;
    private IDisposable inputHook;
    public Voice Transmitter => Components.Get<Voice>();
    public bool Talking => Transmitter is {} voice && (IsProxy
        ? voice.LastPlayed<.3f && voice.Amplitude>.001f
        : voice.IsListening);

    protected override void OnUpdate()
    {
        var voice=Transmitter;
        if(!voice.IsValid())return;
        // The native Voice component handles ownership, transport and the Voice mixer.
        // Lobby-wide playback keeps conversation intelligible across the entire arena.
        voice.Volume=ChatEnabled ? .85f : 0;
    }
    protected override void OnStart()
    {
        previousHoldMode=HoldToTalk;
        inputHook=Scene.AddHook(GameObjectSystem.Stage.StartUpdate,-20,UpdateMicrophone,
            nameof(PlayerVoiceChat),"Apply microphone gate before native voice recording");
    }
    private void UpdateMicrophone()
    {
        if(IsProxy)return;
        var voice=Transmitter;if(!voice.IsValid())return;
        bool allowed=MultiplayerSession.Online && ChatEnabled
            && Scene.GetAllComponents<PaintballControls>().FirstOrDefault()?.Open!=true;
        if(!allowed || previousHoldMode!=HoldToTalk)MicOpen=false;
        previousHoldMode=HoldToTalk;
        if(allowed && !HoldToTalk && Input.Pressed("voice"))MicOpen=!MicOpen;
        bool transmit=allowed && (HoldToTalk ? Input.Down("voice") : MicOpen);
        // Gate both native preference paths: global PTT reads its named action,
        // while global open-mic uses the Manual listening flag.
        voice.Mode=Voice.ActivateMode.Manual;
        voice.PushToTalkInput="voicetransmit";
        Input.SetAction("voicetransmit",transmit);
        voice.IsListening=transmit;
    }
    protected override void OnDestroy()
    {
        inputHook?.Dispose();
        if(IsProxy)return;
        Input.SetAction("voicetransmit",false);
        if(Transmitter.IsValid())Transmitter.IsListening=false;
    }
}
