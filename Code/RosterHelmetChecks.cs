using Sandbox;
namespace PaintballBaddies;

/// <summary>Opt-in checks for swaps between integrated and separate protective equipment.</summary>
public sealed class RosterHelmetChecks : Component
{
    private readonly int[] order = { 0, 1, 2, 3, 5, 0, 4 };
    private float elapsed;
    private int phase;
    private RosterHelmet gear;
    private void CheckEquipment(RosterSelection roster)
    {
        var body=roster.Components.Get<PlayerController>().Renderer;
        var path=RosterSelection.GetModelPath(roster.Selected);
        var integrated=CoveredRosterAssets.CharacterIndex(path)>=0;
        var name=roster.CharacterName;
        Check($"{name} correct body",body.Model?.Name==path);
        var helmets=Scene.GetAllComponents<ModelRenderer>().Count(x=>x.GameObject.Name.EndsWith(" fitted helmet"));
        var seals=Scene.GetAllComponents<SkinnedModelRenderer>().Count(x=>x.GameObject.Name.EndsWith(" fitted neck seal"));
        Check($"{name} no duplicate protective gear",roster.Components.Get<RosterHelmet>()?.Equipped==!integrated
            && helmets==(integrated ? 0 : 1) && seals==(integrated ? 0 : 1));
        var expected=CoveredRosterAssets.ReloadTrack(path);
        var track=roster.Components.Get<ReloadPresentationTrack>();
        Check($"{name} correct belt gear",expected is null ? !track.IsValid() : track.IsValid() && track.TrackPath==expected && track.Docked);
    }
    protected override void OnUpdate()
    {
        var roster=Scene.GetAllComponents<RosterSelection>().FirstOrDefault();
        if (!roster.IsValid()) return;
        elapsed+=Time.Delta;
        if (elapsed<.5f) return;
        elapsed=0;
        if (!gear.IsValid()) gear=roster.Components.Get<RosterHelmet>();
        if (phase==0) roster.Select(order[0]);
        else if (phase<=order.Length)
        {
            CheckEquipment(roster);
            if (phase<order.Length) roster.Select(order[phase]);
            else
            {
                var player=roster.Components.Get<PlayerController>();
                Check("normal controls retained",player.UseInputControls && player.UseCameraControls && player.UseLookControls);
                gear.Enabled=false;
            }
        }
        else if (phase==order.Length+1)
        {
            Check("disabled separate equipment clears",!gear.Equipped
                && !Scene.GetAllComponents<ModelRenderer>().Any(x=>x.GameObject.Name.EndsWith(" fitted helmet"))
                && !Scene.GetAllComponents<SkinnedModelRenderer>().Any(x=>x.GameObject.Name.EndsWith(" fitted neck seal")));
            gear.Enabled=true;
        }
        else
        {
            CheckEquipment(roster);roster.Select(0);
            Log.Info("ROSTER_GEAR COMPLETE");Destroy();
        }
        phase++;
    }
    private static void Check(string name,bool passed)=>Log.Info($"ROSTER_GEAR {(passed ? "PASS" : "FAIL")} {name}");
}
