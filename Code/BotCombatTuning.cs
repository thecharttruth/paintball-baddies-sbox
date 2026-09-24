using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>One shared medium opponent profile for multiplayer practice.</summary>
public static class BotCombatTuning
{
    public const float ReactionSeconds=.26f;
    public const float BurstRestSeconds=.48f;
    public const float DecisionSeconds=.25f;
    public const float SeekCoverAfterSeconds=1.05f;
    public const float CoverRestSeconds=.85f;
    public const float HumanTargetShare=.60f;
    internal static bool PreferHuman(bool humanAvailable,bool botAvailable,float roll)
        => humanAvailable && (!botAvailable || roll<HumanTargetShare);
    public static Vector3 AimError(Vector3 muzzle,Vector3 target,int shot,int character,bool moving)
    {
        var direction=(target-muzzle).Normal;
        var side=Vector3.Cross(direction,Vector3.Up).Normal;
        float radius=Math.Clamp(7.5f+(target-muzzle).Length*.020f,7.5f,56)*(moving ? 1.2f : 1f);
        float phase=shot*2.399963f+character*1.37f;
        return side*(MathF.Sin(phase)*radius)+Vector3.Up*(MathF.Cos(phase*1.31f)*radius*.65f);
    }
}
