using Sandbox;
using System.Linq;
namespace PaintballBaddies;
public sealed class CurvedCoverChecks : Component
{
    private PlayerController player;
    private CoverController cover;
    private float elapsed;
    private int phase;
    private Vector3 initialNormal;
    protected override void OnStart()
    {
        player = Scene.GetAllComponents<PlayerController>().First();
        cover = player.Components.Get<CoverController>();
        player.UseInputControls = player.UseLookControls = false;
        player.Components.Get<PaintballMarker>().AcceptInput = false;
        player.WorldPosition = new Vector3(-480,-354.3307f,2);
        player.EyeAngles = new Angles(0,0,0);
        cover.TestInput = true;
    }
    protected override void OnFixedUpdate()
    {
        elapsed += Time.Delta;
        if (phase == 0 && elapsed > 1)
        {
            bool entered = cover.TryEnter(); initialNormal = cover.Normal; phase = 1;
            Log.Info($"CURVED_COVER {(entered ? "PASS" : "FAIL")} attach to rounded hull");
        }
        if (phase != 1) return;
        cover.TestMovement = Vector3.Cross(Vector3.Up,cover.Normal).Normal;
        if (elapsed < 2.5f) return;
        bool followed = cover.Attached && Vector3.Dot(initialNormal,cover.Normal) < .8f;
        Log.Info($"CURVED_COVER {(followed ? "PASS" : "FAIL")} follow changing face normal; attached={cover.Attached}, dot={Vector3.Dot(initialNormal,cover.Normal)}, position={player.WorldPosition}");
        cover.Leave(); cover.TestMovement = Vector3.Zero; cover.TestInput = false;
        player.UseInputControls = player.UseLookControls = true;
        player.Components.Get<PaintballMarker>().AcceptInput = true;
        phase = 2; Destroy();
    }
}
