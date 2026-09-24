using Sandbox;
namespace PaintballBaddies;

/// <summary>Preserves corresponding foot phase across covered walk/run clips.</summary>
public static class CoveredStrideTransition
{
    public static void SetSequence(SkinnedModelRenderer body, string clip)
    {
        var previousClip=body.Sequence.Name;
        if(previousClip==clip)return;
        // Only query sequence time when a known locomotion clip has a phase to preserve.
        var preservePhase=CoveredRosterAssets.CharacterIndex(body.Model?.Name)>=0
            && ((previousClip=="walk" && clip=="run") || (previousClip=="run" && clip=="walk"));
        var previousPhase=preservePhase ? body.Sequence.TimeNormalized : 0;
        body.Sequence.Name=clip;
        // Measured source foot trajectories align run one eighth cycle before walk.
        if(preservePhase)
            body.Sequence.TimeNormalized=(previousPhase+(clip=="run"?.875f:.125f))%1f;
    }
}
