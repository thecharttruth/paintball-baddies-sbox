using Sandbox;

namespace PaintballBaddies;

/// <summary>Training roster selection; match rules will restrict swaps to the lobby.</summary>
public sealed class RosterSelection : Component
{
    public static readonly string[] Names = { "Viper", "Roxie", "Imani", "Mei", "Freya", "Leilani" };
    public int Selected { get; private set; }
    [Property] public bool UseCitizenCharacters { get; set; }
    private CitizenPlayerPresentation ownedPresentation;
    public string CharacterName => Names[Selected];
    public int AvailableCharacters => Names.Length;
    // Retargeted hip proportions relative to the motion source; keeps gait speed tied to stride length.
    private static readonly float[] MotionScales = { .933228f, .944392f, 1.031877f, .912818f, 1.038501f, .997422f };
    public float MotionScale => MotionScales[Selected];
    public static float GetMotionScale(int index) => MotionScales[index];
    public static string GetModelPath(int index)
    {
        var slug = Names[index].ToLowerInvariant();
        return $"models/characters/{slug}_protected_morph/{slug}_protected.vmdl";
    }
    private PlayerController player;

    protected override void OnStart()
    {
        player = Components.Get<PlayerController>();
        if (Components.Get<RosterHelmet>() is null) Components.Create<RosterHelmet>();
        SelectNetwork(Components.Get<NetworkPawn>()?.Seat ?? 0);
    }

    protected override void OnUpdate()
    {
        if (UseCitizenCharacters && !ownedPresentation.IsValid() && Components.Get<CitizenPlayerPresentation>() is null)
            ownedPresentation=Components.Create<CitizenPlayerPresentation>();
        else if (!UseCitizenCharacters && ownedPresentation.IsValid())
        {
            ownedPresentation.Destroy();
            ownedPresentation=null;
        }
        if(IsProxy || MultiplayerSession.Online)return;
        if ( player is null || (!player.UseInputControls && Components.Get<ArenaMatch>()?.Status == "TRAINING") ) return;
        for ( int i = 0; i < AvailableCharacters; i++ )
            if ( Input.Pressed( $"slot{i + 1}" ) ) Select( i );
    }

    protected override void OnDestroy()
    {
        if (ownedPresentation.IsValid()) ownedPresentation.Destroy();
    }

    public bool Select( int index )
    {
        if(MultiplayerSession.Online || Components.Get<ArenaMatch>()?.InRound==true)return false;
        return SelectNetwork(index);
    }
    internal bool SelectNetwork(int index)
    {
        if ( player?.Renderer is not { } renderer || index < 0 || index >= AvailableCharacters ) return false;
        renderer.Model = Model.Load( GetModelPath(index) );
        renderer.UseAnimGraph = false;
        renderer.Sequence.Name = "idle";
        renderer.Sequence.Looping = true;
        renderer.Sequence.Blending = true;
        Selected = index;
        return true;
    }
}
