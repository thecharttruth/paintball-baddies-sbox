using Sandbox;
using System;
namespace PaintballBaddies;

public sealed partial class PaintballCombatant
{
    [Sync(SyncFlags.FromHost)] public int CarriedTokens {get;internal set;}
    [Sync(SyncFlags.FromHost)] public int BankedTokens {get;internal set;}
    [Sync(SyncFlags.FromHost)] public float BankProgress {get;internal set;}
    [Sync(SyncFlags.FromHost)] public int TokenSerial {get;private set;}
    [Sync(SyncFlags.FromHost)] public string TokenEvent {get;private set;}="";
    [Sync(SyncFlags.FromHost)] public int TokenAmount {get;private set;}
    public string TokenNotice {get;private set;}="";
    public float TokenNoticeRemaining {get;private set;}
    public int TokenSoundsPlayed {get;private set;}
    public string LastTokenSound {get;private set;}="";
    public bool LastTokenSoundStarted {get;private set;}
    private int seenTokenSerial;
    private float tokenAudioCooldown;
    internal void HeistFeedback(string kind,int amount)
    {
        if(!MultiplayerSession.Authority)return;
        TokenEvent=kind;TokenAmount=amount;TokenSerial=unchecked(TokenSerial+1);
    }
    private void UpdateHeistFeedback()
    {
        tokenAudioCooldown=MathF.Max(0,tokenAudioCooldown-Time.Delta);
        TokenNoticeRemaining=MathF.Max(0,TokenNoticeRemaining-Time.Delta);
        if(TokenSerial==seenTokenSerial)return;
        seenTokenSerial=TokenSerial;
        var local=MultiplayerSession.LocalPlayer(Scene);
        if(!local.IsValid() || local.GameObject!=GameObject)return;
        TokenNotice=TokenEvent switch {
            "bank"=>$"+{TokenAmount} BANKED — SAFE",
            "spill"=>$"-{TokenAmount} DROPPED — BANKED TOKENS ARE SAFE",
            "collect"=>$"+{TokenAmount} COLLECTED",
            "earn"=>"+1 CARRYING — BANK IT TO SCORE",
            _=>""};
        TokenNoticeRemaining=TokenEvent=="bank" ? 2.5f : 1.6f;
        // Hit confirmation already acknowledges earning. Keep it distinct from walking over a chip.
        var sound=HeistAudio.Get(TokenEvent);
        if(sound is not null && (tokenAudioCooldown<=0 || TokenEvent=="bank"))
        {
            var handle=Sound.Play(sound,Scene.Camera?.WorldPosition ?? WorldPosition);
            LastTokenSound=sound.Sounds[0].ResourcePath;LastTokenSoundStarted=handle?.IsPlaying==true;if(LastTokenSoundStarted)TokenSoundsPlayed++;
            tokenAudioCooldown=.16f;
        }
    }
    private void ResetHeistWallet(){CarriedTokens=BankedTokens=0;BankProgress=0;TokenEvent="";TokenNotice="";TokenNoticeRemaining=0;TokenSerial++;}
}
