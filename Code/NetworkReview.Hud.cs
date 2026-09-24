using Sandbox;
using Sandbox.UI;
namespace PaintballBaddies;

// Reads the actual rendered panels on each peer, only while the review fixture exists.
public sealed partial class NetworkReview
{
    private object HudReport()
    {
        var hud=Scene.GetAllComponents<TrainingHud>().FirstOrDefault();
        var panels=hud?.Panel?.Descendants.ToArray() ?? System.Array.Empty<Panel>();
        var labels=panels.OfType<Label>().ToArray();
        var controls=Scene.GetAllComponents<PaintballControls>().FirstOrDefault();
        return new {
            Heist=HeistReview.Snapshot(Scene),
            controls?.ShowGameplayText,controls?.ShowScoreboard,
            VoiceEnabled=PlayerVoiceChat.ChatEnabled,VoiceKey=controls?.Label("voice"),VaultKey=controls?.Label("vault"),
            HoldToTalk=PlayerVoiceChat.HoldToTalk,MicOpen=MultiplayerSession.LocalPlayer(Scene)?.Components.Get<PlayerVoiceChat>()?.MicOpen,
            VoiceIndicator=panels.FirstOrDefault(x=>x.HasClass("voice-indicator")) is not null,
            Voices=Scene.GetAllComponents<Voice>().Select(x=>new {x.GameObject.Name,x.IsProxy,Mode=x.Mode.ToString(),
                x.PushToTalkInput,x.Volume,x.WorldspacePlayback,x.Loopback,x.IsListening,x.IsRecording,x.Amplitude,
                SincePlayed=(float)x.LastPlayed,Seat=x.Components.Get<NetworkPawn>()?.Seat}).ToArray(),
            HudRect=hud?.Panel?.Box.Rect.ToString(),
            CrosshairRect=panels.FirstOrDefault(x=>x.HasClass("crosshair"))?.Box.Rect.ToString(),
            IncomingRect=panels.FirstOrDefault(x=>x.HasClass("incoming-direction"))?.Box.Rect.ToString(),
            Types=labels.Where(x=>x.HasClass("board-type")).Select(x=>x.Text).ToArray(),
            Recovery=labels.FirstOrDefault(x=>x.HasClass("recovery-instruction"))?.Text,
            Countdown=panels.Any(x=>x.HasClass("final-countdown")),
            Awards=panels.Where(x=>x.HasClass("award-card")).Select(x=>x.Descendants.OfType<Label>().Select(l=>l.Text).ToArray()).ToArray(),
            PersonalRecords=hud?.PersonalRecords,
            PersonalBest=labels.FirstOrDefault(x=>x.HasClass("personal-best"))?.Text,
            Result=labels.FirstOrDefault(x=>x.HasClass("result-title"))?.Text,
            Restart=labels.FirstOrDefault(x=>x.HasClass("retry"))?.Text,
            FilledPips=panels.Count(x=>x.HasClass("recovery-pip") && x.HasClass("filled"))
        };
    }
}
