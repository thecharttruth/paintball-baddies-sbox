using Sandbox;

namespace PaintballBaddies;

/// <summary>Identifies the covered replacement bodies and their built-in equipment.</summary>
public static class CoveredRosterAssets
{
    public static string ReloadTrack( string modelPath ) => modelPath?.Replace( "\\", "/" ) switch
    {
        "models/characters/imani_run_clearance/imani.vmdl" => "animations/imani_reload_presentation.json",
        "models/characters/imani_shuffle_pilot/imani_protected.vmdl" => "animations/imani_reload_presentation.json",
        "models/characters/freya_shuffle_pilot/freya_protected.vmdl" => "animations/freya_reload_presentation.json",
        "models/characters/freya_protected_morph/freya_protected.vmdl" => "animations/freya_reload_presentation.json",
        "models/characters/imani_protected_morph/imani_protected.vmdl" => "animations/imani_reload_presentation.json",
        "models/characters/viper_protected_morph/viper_protected.vmdl" => "animations/viper_reload_presentation.json",
        "models/characters/leilani_protected_morph/leilani_protected.vmdl" => "animations/leilani_reload_presentation.json",
        "models/characters/roxie_protected_morph/roxie_protected.vmdl" => "animations/roxie_reload_presentation.json",
        "models/characters/mei_planted_review/mei_protected.vmdl" => "animations/mei_reload_presentation.json",
        "models/characters/mei_protected_reference/mei_protected.vmdl" => "animations/mei_reload_presentation.json",
        "models/characters/mei_protected_morph/mei_protected.vmdl" => "animations/mei_reload_presentation.json",
        _ => null
    };
    /// <summary>Authored marker origin relative to RightHand, in body-space inches.</summary>
    public static bool TryMarkerOffset( string modelPath, out Vector3 offset )
    {
        offset = default;
        switch ( modelPath?.Replace( "\\", "/" ) )
        {
            case "models/characters/imani_run_clearance/imani.vmdl":
            case "models/characters/imani_protected_morph/imani_protected.vmdl":
                offset = new Vector3( 4.92126f, -.984252f, .590551f );
                return true;
            case "models/characters/imani_shuffle_pilot/imani_protected.vmdl":
            case "models/characters/imani_protected_two_arm/imani_protected.vmdl":
                offset = new Vector3( 4.92126f, -.984252f, -3.740157f );
                return true;
            case "models/characters/viper_protected_two_arm/viper_protected.vmdl":
            case "models/characters/viper_protected_morph/viper_protected.vmdl":
                offset = new Vector3( 3.464567f, 1.377953f, -3.740157f );
                return true;
            case "models/characters/freya_protected_two_arm/freya_protected.vmdl":
            case "models/characters/freya_shuffle_pilot/freya_protected.vmdl":
            case "models/characters/freya_protected_morph/freya_protected.vmdl":
            case "models/characters/roxie_protected_two_arm/roxie_protected.vmdl":
            case "models/characters/roxie_protected_morph/roxie_protected.vmdl":
            case "models/characters/leilani_protected_two_arm/leilani_protected.vmdl":
            case "models/characters/leilani_protected_morph/leilani_protected.vmdl":
                offset = new Vector3( 4.92126f, 1.377953f, .590551f );
                return true;
            case "models/characters/mei_protected_two_arm/mei_protected.vmdl":
            case "models/characters/mei_planted_review/mei_protected.vmdl":
            case "models/characters/mei_protected_reference/mei_protected.vmdl":
            case "models/characters/mei_protected_morph/mei_protected.vmdl":
                offset = new Vector3( 4.133858f, 1.377953f, .590551f );
                return true;
            default:
                return false;
        }
    }

    public static int CharacterIndex( string modelPath ) => modelPath?.Replace( "\\", "/" ) switch
    {
        "models/characters/imani_run_clearance/imani.vmdl" => 2,
        "models/characters/viper_protected/viper_protected.vmdl" => 0,
        "models/characters/viper_protected_two_arm/viper_protected.vmdl" => 0,
        "models/characters/viper_protected_morph/viper_protected.vmdl" => 0,
        "models/characters/roxie_protected/roxie_protected.vmdl" => 1,
        "models/characters/roxie_protected_grip/roxie_protected.vmdl" => 1,
        "models/characters/roxie_protected_two_arm/roxie_protected.vmdl" => 1,
        "models/characters/roxie_protected_morph/roxie_protected.vmdl" => 1,
        "models/characters/imani_protected/imani_protected.vmdl" => 2,
        "models/characters/imani_shuffle_pilot/imani_protected.vmdl" => 2,
        "models/characters/imani_protected_two_arm/imani_protected.vmdl" => 2,
        "models/characters/imani_protected_morph/imani_protected.vmdl" => 2,
        "models/characters/mei_protected/mei_protected.vmdl" => 3,
        "models/characters/mei_protected_two_arm/mei_protected.vmdl" => 3,
        "models/characters/mei_planted_review/mei_protected.vmdl" => 3,
        "models/characters/mei_protected_reference/mei_protected.vmdl" => 3,
        "models/characters/mei_protected_morph/mei_protected.vmdl" => 3,
        "models/characters/freya_protected/freya_protected.vmdl" => 4,
        "models/characters/freya_shuffle_pilot/freya_protected.vmdl" => 4,
        "models/characters/freya_protected_two_arm/freya_protected.vmdl" => 4,
        "models/characters/freya_protected_morph/freya_protected.vmdl" => 4,
        "models/characters/leilani_protected/leilani_protected.vmdl" => 5,
        "models/characters/leilani_protected_two_arm/leilani_protected.vmdl" => 5,
        "models/characters/leilani_protected_morph/leilani_protected.vmdl" => 5,
        _ => -1
    };
}
