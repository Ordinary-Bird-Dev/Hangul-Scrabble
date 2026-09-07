using UnityEngine;

// The one place the game's colours live.
//
// Before this existed there were nine hardcoded `new Color(...)` calls
// spread across six controllers, and none of them agreed: two different
// greys for the same "unselected chip" idea, three unrelated oranges, a
// pure-red rejection flash. Anything built at runtime now reads from here,
// so a palette change is one file rather than a scavenger hunt.
//
// This does NOT cover scene-authored objects — panels, buttons and labels
// placed in a .unity file carry their own serialized colours and are edited
// in the Inspector. Keep the two in step by hand; these values are the
// reference.
//
// Naming: `On<Surface>` is the colour of content drawn ON that surface.
// OnAction is the label colour for an Action-coloured button, and so on.
public static class Palette
{
    // ---- Action -------------------------------------------------------
    // Exactly one accent, and only ever for things you can press: Confirm,
    // the enter arrow, the gear, the time gauge. Anything decorative that
    // takes this colour weakens every real button on the screen.
    public static readonly Color Action = Hex("FD984D");
    public static readonly Color ActionEdge = Hex("D97B34");   // the solid drop edge
    public static readonly Color ActionMuted = Hex("D97B34", 0.45f);
    public static readonly Color OnAction = Hex("323232");

    // ---- Surfaces -----------------------------------------------------
    // One white material for every panel: clue banner, word bar, the
    // How to Play sheet, settings rows.
    public static readonly Color Surface = Hex("FFFFFF");
    public static readonly Color SurfaceMuted = Hex("FFFFFF", 0.90f);
    public static readonly Color SurfaceSunken = Hex("F4F2EC");  // insets on a Surface
    public static readonly Color SurfaceEdge = Hex("D8D4C8");    // drop edge for a Surface

    // ---- Text ---------------------------------------------------------
    public static readonly Color Ink = Hex("323232");
    public static readonly Color InkSoft = Hex("6E6A62");
    public static readonly Color InkFaint = Hex("8A867B");
    public static readonly Color OnJungle = Hex("FFFFFF");       // text straight on the background

    // ---- Tiles --------------------------------------------------------
    // Pulled back from the neon #8BE04F / #5BD6F5 so that nothing on the
    // board competes with Action. The consonant/vowel split still reads.
    // NOTE: the live values are serialized on the Tile prefab — changing
    // these constants does not move the prefab. Update it in the Inspector.
    public static readonly Color Consonant = Hex("7CBF4F");
    public static readonly Color ConsonantEdge = Hex("5E9B37");
    public static readonly Color OnConsonant = Hex("22371A");
    public static readonly Color Vowel = Hex("56AEC9");
    public static readonly Color VowelEdge = Hex("3E8CA5");
    public static readonly Color OnVowel = Hex("12313A");
    public static readonly Color TileEmpty = Hex("FFFFFF", 0.16f);
    public static readonly Color TileConsumed = Hex("FFFFFF", 0.16f);

    // ---- Feedback -----------------------------------------------------
    // Muted against the art rather than pure RGB primaries, which vibrate.
    public static readonly Color Success = Hex("4E9B34");
    public static readonly Color Reject = Hex("D9614A");

    // ---- Hint chip ----------------------------------------------------
    public static readonly Color HintChip = Hex("FFF1E4");
    public static readonly Color HintChipEdge = Hex("FBD3AE");
    public static readonly Color OnHintChip = Hex("D97B34");

    // ---- Scrim --------------------------------------------------------
    // Laid over the jungle so white text and panels keep their contrast.
    public static readonly Color Scrim = Hex("102614", 0.34f);
    public static readonly Color ScrimLight = Hex("102614", 0.22f);

    // Parses "RRGGBB". A typo fails loudly and paints magenta rather than
    // silently shipping a black UI — the same convention as this project's
    // name-lookup warnings.
    private static Color Hex(string rrggbb, float alpha = 1f)
    {
        if (!ColorUtility.TryParseHtmlString("#" + rrggbb, out Color color))
        {
            Debug.LogError($"Palette: '{rrggbb}' is not a valid hex colour — using magenta so it is obvious on screen.");
            return Color.magenta;
        }
        color.a = alpha;
        return color;
    }
}
