using Sandbox;
using System;
namespace PaintballBaddies;

/// <summary>Millimetre-scale suit follow-through phased behind the current gait.</summary>
public static class CoveredSuitMotion
{
    public static void Apply(SkinnedModelRenderer body)
    {
        if (!body.IsValid() || !body.Enabled || !body.Model.IsValid() || body.Model.Morphs.GetIndex("SuitFollowThroughLUp")<0) return;
        var clip=body.Sequence.Name;
        var amplitude=clip switch { "run"=>1f, "walk" or "walk_backward"=>.48f,
            "crouch_walk" or "crouch_left" or "crouch_right"=>.32f, "idle"=>.2f, "crouch"=>.16f, _=>0f };
        var frequency=clip=="idle" || clip=="crouch" ? 1 : 2;
        var value=MathF.Sin(body.Sequence.TimeNormalized*MathF.Tau*frequency-.6f)*amplitude;
        var up=MathF.Max(0,value);var down=MathF.Max(0,-value);
        body.Morphs.Set("SuitFollowThroughLUp",up,0);
        body.Morphs.Set("SuitFollowThroughLDown",down,0);
        body.Morphs.Set("SuitFollowThroughRUp",up,0);
        body.Morphs.Set("SuitFollowThroughRDown",down,0);
    }
}
