namespace PaintballBaddies;

/// <summary>Reviewed suit candidates; shared gear and final body proportions remain in progress.</summary>
public static class CitizenRosterAssets
{
    public static string Helmet(int index) => index switch
    {
        1 => "models/gear/citizen_closed_helmet_review/roxie.vmdl",
        2 => "models/gear/citizen_closed_helmet_review/imani.vmdl",
        3 => "models/gear/citizen_closed_helmet_review/mei.vmdl",
        4 => "models/gear/citizen_closed_helmet_review/freya.vmdl",
        5 => "models/gear/citizen_closed_helmet_review/leilani.vmdl",
        _ => "models/gear/citizen_closed_helmet_review/helmet.vmdl"
    };
    public static string Suit(int index) => index switch
    {
        1 => "models/gear/roxie_suit_motion_review/sculpted_round_suit.vmdl",
        2 => "models/gear/imani_suit_motion_review/sculpted_round_suit.vmdl",
        3 => "models/gear/mei_suit_motion_review/sculpted_round_suit.vmdl",
        4 => "models/gear/freya_suit_motion_review/sculpted_round_suit.vmdl",
        5 => "models/gear/leilani_suit_motion_review/sculpted_round_suit.vmdl",
        _ => "models/gear/citizen_suit_motion_review/sculpted_round_suit.vmdl"
    };
    public static string StaticSuit(int index) => index switch
    {
        1 => "models/gear/roxie_citizen_suit_review/suit.vmdl",
        2 => "models/gear/imani_citizen_suit_review/suit.vmdl",
        3 => "models/gear/mei_citizen_suit_review/suit.vmdl",
        4 => "models/gear/freya_citizen_suit_review/suit.vmdl",
        5 => "models/gear/leilani_citizen_suit_review/suit.vmdl",
        _ => "models/gear/citizen_curvy_suit_review/suit.vmdl"
    };
}
