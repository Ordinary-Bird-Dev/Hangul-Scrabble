using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Builds the cutout rig's animation clips from the recipes in the rig guide.
//
// Why a script rather than hand-authored .anim files: a clip's runtime
// bindings are hashed paths and hashed property names, not the strings you
// see in the Animation window. Writing the YAML by hand means guessing those
// hashes, and a clip that binds in the editor but not at runtime is worse
// than no clip at all. Going through AnimationUtility lets Unity compute
// them, so the output is correct by construction.
//
// Idempotent: re-running overwrites the four clips it owns and nothing else.
// Tune the numbers here, press the menu item again, and the clips update —
// which is a faster loop than dragging keys, at least until the timing is
// close and you want to nudge by eye.
//
// The clips are written under new names so the sprite-based originals and
// the old MascotImage prefab stay intact as a fallback. Point the Animator
// states at these when you are ready; see the console output.
public static class MascotRigClipBuilder
{
    private const string Dir = "Assets/Animations/";

    // Paths are relative to the GameObject carrying the Animator — the
    // mascot root. Renaming or reparenting any of these silently breaks
    // every curve that names it, so they live in one place.
    private const string Rig     = "Rig";
    private const string Body    = "Rig/Body";
    private const string Head    = "Rig/Body/Head";
    private const string Tail    = "Rig/Tail";
    private const string ArmFar  = "Rig/ArmFar";
    private const string ArmNear = "Rig/ArmNear";
    private const string Face    = "Rig/Body/Head/Face";

    // The legs are siblings of Body, not children of it, because a UI child
    // always draws in front of its parent and these have to sit behind the
    // torso. The cost is that they do not inherit Body's breathing squash —
    // acceptable, since Body pivots at its base and the hips barely move.
    //
    // Positive Z is counter-clockwise, so on both legs a positive angle
    // swings the foot toward screen-right: same sign swings them together,
    // opposite signs splay or tuck.
    private const string LegFar  = "Rig/LegFar";
    private const string LegNear = "Rig/LegNear";

    // Two-bone arms. ArmFar/ArmNear are now empty bones that carry the
    // shoulder rotation; the drawing lives on their two children. The upper
    // arm is the LAST child so it paints over the forearm, which is how the
    // source art overlaps — a UI child always draws in front of its parent,
    // so the forearm could not simply be parented to the upper arm.
    //
    // ForearmFar/ForearmNear pivot on the elbow and inherit the shoulder, so
    // a curve here is a pure elbow bend. Keep it within +/-15 degrees: past
    // that the forearm's rounded cap clears the upper arm's cut edge and the
    // joint shows as a bump. 12 is the working limit used below.
    private const string ForearmFar  = "Rig/ArmFar/ForearmFar";
    private const string ForearmNear = "Rig/ArmNear/ForearmNear";

    // Expressions. The head itself is now blank; everything that changes
    // between moods lives on the Face child, which is a ~200px patch rather
    // than a whole head.
    private const string FaceDir     = "Assets/Sprites/monkey rig/";
    private const string FaceNeutral = "face_neutral";
    private const string FaceAngry   = "face_angry";
    private const string FaceAsleep  = "face_asleep";
    private const string FaceHappy   = "face_happy";

    // Unity's serialized names. Rotation is NOT "m_LocalRotation" — the
    // Animation window writes euler angles under this name so that a 370°
    // turn stays distinguishable from a 10° one.
    private const string RotZ    = "localEulerAnglesRaw.z";
    private const string ScaleX  = "m_LocalScale.x";
    private const string ScaleY  = "m_LocalScale.y";
    private const string PosY    = "m_AnchoredPosition.y";
    private const string PosX    = "m_AnchoredPosition.x";

    [MenuItem("Tools/Hangulnoree/Build Mascot Rig Clips")]
    public static void BuildAll()
    {
        BuildIdle();
        BuildTileSelect();
        BuildWrong();
        BuildCelebrate();
        BuildAsleep();
        BuildWakeUp();
        BuildWin();
        BuildLose();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Mascot rig clips built: Rig_Idle, Rig_TileSelect, Rig_Wrong, Rig_Celebrate, " +
            "Rig_Asleep, Rig_WakeUp, Rig_Win, Rig_Lose.\n" +
            "To use them, open MascotAnimator and set each state's Motion to the matching Rig_ clip. " +
            "The sprite-based originals are untouched.\n" +
            "Note: nothing in the project fires the Win, Lose or TileSelect triggers yet, and there " +
            "is no TileSelect state at all — see the comments on BuildWin and BuildTileSelect.");
    }

    // ---- the recipes ------------------------------------------------------

    // Breathing loop. The tail runs a quarter-period behind the head on
    // purpose: nothing alive moves in sync, and that offset is the cheapest
    // trick there is for making a rig stop looking like paper.
    private static void BuildIdle()
    {
        AnimationClip clip = Prepare("Rig_Idle", loop: true);

        Curve(clip, Body, ScaleY, (0f, 1f), (1.2f, 1.015f), (2.4f, 1f));

        Curve(clip, Head, RotZ, (0f, 0f), (0.6f, 1.5f), (1.2f, 0f), (1.8f, -1.5f), (2.4f, 0f));

        // Same period, shifted a quarter cycle. First and last values match
        // so the loop does not pop.
        Curve(clip, Tail, RotZ, (0f, 6f), (0.6f, 0f), (1.2f, -6f), (1.8f, 0f), (2.4f, 6f));

        // Barely there, and opposed so the two legs never move as one lump.
        // Ends where it starts or the loop pops.
        Curve(clip, LegFar,  RotZ, (0f, 0f), (1.2f, -1.2f), (2.4f, 0f));
        Curve(clip, LegNear, RotZ, (0f, 0f), (1.2f,  1.2f), (2.4f, 0f));
    }

    // The nod, on tile select. Fast down, slower back — an even in-and-out
    // reads as a wobble rather than an acknowledgement.
    // Clapping overhead, fired on every tile tap.
    //
    // The arms cannot reach above the head by rotating alone: shoulder to
    // hand is 244px and the point above the skull is 371px away, so the bones
    // are moved as well as turned. That is what the PosX/PosY curves below
    // are — they lift each shoulder up behind the head, where the upper arm's
    // unfinished cut edge is covered by the ears.
    //
    // The rotations (100 open, 114 shut) are past vertical, which puts each
    // arm's shaded underside on the OUTER face of the V. That reads as
    // ordinary shading on a tube-shaped arm, and it is the only way to get
    // the hands inboard: the two arms' reach circles intersect only on the
    // centre line, so a clap anywhere else is geometrically impossible.
    //
    // Do not nudge the rotations without re-checking the shoulders. At 120
    // with the hands at this height the cut edge clears the ears and ~240
    // pixels of raw brush end appear beside the head. 100/114 measured clean.
    private static void BuildTileSelect()
    {
        AnimationClip clip = Prepare("Rig_TileSelect", loop: false);

        // Up, clap, clap, down. Returning to zero matters: the state exits on
        // exit time, and WriteDefaultValues would otherwise snap the arms.
        Curve(clip, ArmFar,  RotZ,
            (0f, 0f), (0.16f, -100f), (0.28f, -114f), (0.40f, -100f),
            (0.52f, -114f), (0.64f, -100f), (0.85f, 0f));
        Curve(clip, ArmNear, RotZ,
            (0f, 0f), (0.16f,  100f), (0.28f,  114f), (0.40f,  100f),
            (0.52f,  114f), (0.64f,  100f), (0.85f, 0f));

        // Shoulder travel. Solved from the pose, not eyeballed: each pair
        // puts the hand on the centre line just above the skull.
        Curve(clip, ArmFar,  PosX,
            (0f, 0f), (0.16f, -17.6f), (0.28f, -3.5f), (0.40f, -17.6f),
            (0.52f, -3.5f), (0.64f, -17.6f), (0.85f, 0f));
        Curve(clip, ArmNear, PosX,
            (0f, 0f), (0.16f,  16.4f), (0.28f,  2.0f), (0.40f,  16.4f),
            (0.52f,  2.0f), (0.64f,  16.4f), (0.85f, 0f));
        Curve(clip, ArmFar,  PosY,
            (0f, 0f), (0.16f, 199.5f), (0.28f, 221.8f), (0.40f, 199.5f),
            (0.52f, 221.8f), (0.64f, 199.5f), (0.85f, 0f));
        Curve(clip, ArmNear, PosY,
            (0f, 0f), (0.16f, 201.7f), (0.28f, 224.1f), (0.40f, 201.7f),
            (0.52f, 224.1f), (0.64f, 201.7f), (0.85f, 0f));

        // Elbows straighten as the arms go up — a bent elbow at the top of a
        // clap reads as a shrug.
        Curve(clip, ForearmFar,  RotZ, (0f, 0f), (0.16f, -6f), (0.64f, -6f), (0.85f, 0f));
        Curve(clip, ForearmNear, RotZ, (0f, 0f), (0.16f,  6f), (0.64f,  6f), (0.85f, 0f));

        // The old tap-tick, kept: the head still dips on contact, now twice,
        // in time with the hands meeting.
        Curve(clip, Head, RotZ,
            (0f, 0f), (0.28f, -5f), (0.34f, 0f), (0.52f, -5f), (0.58f, 0f), (0.85f, 0f));

        // Body rides the claps.
        Curve(clip, Rig, PosY,
            (0f, 0f), (0.16f, 10f), (0.28f, 0f), (0.40f, 8f), (0.52f, 0f),
            (0.64f, 6f), (0.85f, 0f));

        FaceCurve(clip, (0f, FaceHappy), (0.72f, FaceNeutral));
    }

    // Wrong answer: a decaying shake on the whole rig, plus the head
    // dropping. Returns to zero at the end rather than holding, so whatever
    // state follows starts from a clean pose.
    private static void BuildWrong()
    {
        AnimationClip clip = Prepare("Rig_Wrong", loop: false);

        Curve(clip, Rig, RotZ,
            (0f, 0f), (0.08f, 5f), (0.16f, -5f), (0.24f, 3f), (0.32f, -2f), (0.4f, 0f));

        Curve(clip, Head, RotZ, (0f, 0f), (0.12f, -8f), (0.32f, -8f), (0.4f, 0f));

        // A brief tuck, as if bracing. Small, because the Rig shake already
        // carries the legs along with everything else.
        Curve(clip, LegFar,  RotZ, (0f, 0f), (0.12f,  6f), (0.4f, 0f));
        Curve(clip, LegNear, RotZ, (0f, 0f), (0.12f, -6f), (0.4f, 0f));

        // Arms whip along with the shake instead of riding it rigidly.
        //
        // These are the ONLY arm curves in the rig where the two sides share
        // a sign rather than mirroring. Everywhere else the arms make a
        // symmetric gesture, so Far and Near take opposite values; here the
        // whole body is tipping one way at a time, and a lagging arm has to
        // counter-rotate in the same direction on both sides to stay put in
        // world space. Mirroring them would read as flapping, not shaking.
        //
        // Derived rather than eyeballed: local = body(t - 0.06) - body(t),
        // i.e. the arm holds its world angle about half a beat behind the
        // torso, then catches up. That is why the values run opposite in
        // phase to the Rig curve above and decay on the same envelope.
        Curve(clip, ArmFar,  RotZ,
            (0f, 0f), (0.08f, -4f), (0.16f, 7f), (0.24f, -6f), (0.32f, 4f), (0.4f, 0f));
        Curve(clip, ArmNear, RotZ,
            (0f, 0f), (0.08f, -4f), (0.16f, 7f), (0.24f, -6f), (0.32f, 4f), (0.4f, 0f));

        // The elbows lag the shoulders by another beat, so the whip travels
        // out to the hands. Same derivation at tau = 0.10, trimmed to stay
        // inside the +/-15 degree limit where the joint stays hidden.
        Curve(clip, ForearmFar,  RotZ,
            (0f, 0f), (0.12f, -5f), (0.2f, 9f), (0.28f, -6f), (0.36f, 3f), (0.4f, 0f));
        Curve(clip, ForearmNear, RotZ,
            (0f, 0f), (0.12f, -5f), (0.2f, 9f), (0.28f, -6f), (0.36f, 3f), (0.4f, 0f));

        // Cross for the duration, back to neutral as it recovers.
        FaceCurve(clip, (0f, FaceAngry), (0.34f, FaceNeutral));
    }

    // Hop with squash and stretch. Stretches thin on the way up, squashes
    // wide on landing, settles. The Rig pivot sits at the hips (0.5, 0.1),
    // which is what makes the squash read as weight on the feet instead of
    // the whole monkey deflating.
    private static void BuildCelebrate()
    {
        AnimationClip clip = Prepare("Rig_Celebrate", loop: false);

        Curve(clip, Rig, PosY,   (0f, 0f), (0.18f, 40f), (0.4f, 0f), (0.48f, 8f), (0.6f, 0f));
        Curve(clip, Rig, ScaleX, (0f, 1f), (0.18f, 0.92f), (0.4f, 1.08f), (0.6f, 1f));
        Curve(clip, Rig, ScaleY, (0f, 1f), (0.18f, 1.08f), (0.4f, 0.92f), (0.6f, 1f));

        // Signs differ because the arms point opposite ways: positive Z is
        // counter-clockwise, so it lifts the right arm and drops the left.
        Curve(clip, ArmFar,  RotZ, (0f, 0f), (0.18f, -40f), (0.6f, 0f));
        Curve(clip, ArmNear, RotZ, (0f, 0f), (0.18f,  40f), (0.6f, 0f));

        // Legs tuck at the top of the hop and drop again on the way down —
        // a jump where the feet stay planted reads as the body being yanked
        // upward rather than pushing off. Peaks with Rig PosY at 0.18s.
        Curve(clip, LegFar,  RotZ, (0f, 0f), (0.18f,  15f), (0.4f, -4f), (0.6f, 0f));
        Curve(clip, LegNear, RotZ, (0f, 0f), (0.18f, -15f), (0.4f,  4f), (0.6f, 0f));

        // Elbows snap up a beat after the shoulders, so the arms unfurl
        // rather than swinging as two straight planks.
        Curve(clip, ForearmFar,  RotZ, (0f, 0f), (0.24f, -12f), (0.6f, 0f));
        Curve(clip, ForearmNear, RotZ, (0f, 0f), (0.24f,  12f), (0.6f, 0f));

        // Grins on the hop, back to neutral before the clip ends. Rig_Idle
        // deliberately carries no face curve — MascotController owns the face
        // there — so whatever expression a clip finishes on simply stays, and
        // a Celebrate that ended happy would leave the monkey beaming through
        // the next wrong answer.
        FaceCurve(clip, (0f, FaceHappy), (0.5f, FaceNeutral));
    }

    // Sleeping. Fired by MascotSleepController after 20 seconds without a
    // tile tap — and until now it has been switching the mascot to a clip
    // that animates an Image the rig does not have, i.e. freezing it.
    //
    // The head holds its loll for the whole loop rather than returning to
    // neutral: a sleeping character that keeps straightening up reads as
    // nodding off over and over instead of being asleep.
    private static void BuildAsleep()
    {
        AnimationClip clip = Prepare("Rig_Asleep", loop: true);

        // Slower and deeper than the idle breath — that contrast is most of
        // what sells the state change.
        Curve(clip, Body, ScaleY, (0f, 1f), (1.5f, 1.03f), (3f, 1f));

        // Tied to the breath, so the head rides the body rather than
        // drifting on its own schedule.
        Curve(clip, Head, RotZ, (0f, 12f), (1.5f, 13.5f), (3f, 12f));

        Curve(clip, Tail, RotZ, (0f, 2f), (1.5f, -2f), (3f, 2f));

        // Splayed and slack, drifting with the breath. WakeUp starts from
        // these same values, so changing one means changing both.
        Curve(clip, LegFar,  RotZ, (0f, -6f), (1.5f, -7.5f), (3f, -6f));
        Curve(clip, LegNear, RotZ, (0f,  6f), (1.5f,  7.5f), (3f,  6f));

        // Elbows fold in slightly and stay folded. WakeUp starts from these.
        Curve(clip, ForearmFar,  RotZ, (0f,  7f), (1.5f,  8f), (3f,  7f));
        Curve(clip, ForearmNear, RotZ, (0f, -7f), (1.5f, -8f), (3f, -7f));

        // Eyes shut for the whole loop.
        FaceCurve(clip, (0f, FaceAsleep));
    }

    // Waking. Starts from the asleep pose on purpose — the Animator blends
    // into this clip, so beginning at neutral would snap the head upright
    // before the animation has played.
    private static void BuildWakeUp()
    {
        AnimationClip clip = Prepare("Rig_WakeUp", loop: false);

        // Overshoots past neutral and settles. A head that travels straight
        // from 12 to 0 reads as a lift; one that overshoots reads as a
        // startle, which is what waking up is.
        Curve(clip, Head, RotZ,
            (0f, 12f), (0.2f, 12f), (0.35f, -7f), (0.5f, 4f), (0.65f, -2f), (0.8f, 0f));

        Curve(clip, Body, ScaleY, (0f, 1.03f), (0.25f, 1.03f), (0.45f, 0.97f), (0.8f, 1f));

        Curve(clip, Tail, RotZ, (0f, 0f), (0.3f, 10f), (0.8f, 0f));

        // Picks up the asleep splay (-6 / +6) and pulls in through neutral
        // with a small tuck, the leg half of the startle.
        Curve(clip, LegFar,  RotZ, (0f, -6f), (0.2f, -6f), (0.4f,  7f), (0.8f, 0f));
        Curve(clip, LegNear, RotZ, (0f,  6f), (0.2f,  6f), (0.4f, -7f), (0.8f, 0f));

        // Unfold from the asleep fold (+7 / -7) and settle straight.
        Curve(clip, ForearmFar,  RotZ, (0f,  7f), (0.2f,  7f), (0.45f, -5f), (0.8f, 0f));
        Curve(clip, ForearmNear, RotZ, (0f, -7f), (0.2f, -7f), (0.45f,  5f), (0.8f, 0f));

        // Eyes open on the startle, not at the start — the head snaps up
        // first and the face follows a frame later, which reads as waking
        // rather than as having been awake all along.
        FaceCurve(clip, (0f, FaceAsleep), (0.3f, FaceNeutral));
    }

    // Round won. Fired from ResultSceneController, which sets Win or Lose
    // depending on the outcome — reachable, despite what an earlier version of
    // this comment claimed.
    //
    // Loops, unlike Celebrate: this plays on a screen the player sits and
    // reads, so a single burst would freeze on its last frame.
    private static void BuildWin()
    {
        AnimationClip clip = Prepare("Rig_Win", loop: true);

        // Two hops of decreasing height, then a rest before the loop comes
        // round — continuous bouncing gets tiring to sit next to.
        Curve(clip, Rig, PosY,
            (0f, 0f), (0.2f, 34f), (0.45f, 0f), (0.65f, 24f), (0.85f, 0f), (1.6f, 0f));
        Curve(clip, Rig, ScaleX, (0f, 1f), (0.2f, 0.94f), (0.45f, 1.06f), (0.6f, 1f), (1.6f, 1f));
        Curve(clip, Rig, ScaleY, (0f, 1f), (0.2f, 1.06f), (0.45f, 0.94f), (0.6f, 1f), (1.6f, 1f));

        // Arms stay up throughout and wave gently, rather than pumping with
        // each hop — held arms are what make it read as sustained delight.
        Curve(clip, ArmFar,  RotZ, (0f, -35f), (0.8f, -45f), (1.6f, -35f));
        Curve(clip, ArmNear, RotZ, (0f,  35f), (0.8f,  45f), (1.6f,  35f));

        // Faster than the idle sway. Every value matches at 0 and 1.6 so the
        // loop does not pop.
        Curve(clip, Tail, RotZ,
            (0f, 8f), (0.4f, -8f), (0.8f, 8f), (1.2f, -8f), (1.6f, 8f));

        // One tuck per hop, matching Rig PosY's peaks at 0.2 and 0.65, then
        // still for the rest of the loop like everything else.
        Curve(clip, LegFar,  RotZ,
            (0f, 0f), (0.2f,  13f), (0.45f, 0f), (0.65f,  9f), (0.85f, 0f), (1.6f, 0f));
        Curve(clip, LegNear, RotZ,
            (0f, 0f), (0.2f, -13f), (0.45f, 0f), (0.65f, -9f), (0.85f, 0f), (1.6f, 0f));

        // The wave itself. Twice the shoulder's rate, because a wave is an
        // elbow action — the shoulder only carries it.
        Curve(clip, ForearmFar,  RotZ,
            (0f, 0f), (0.4f, -12f), (0.8f, 0f), (1.2f, -12f), (1.6f, 0f));
        Curve(clip, ForearmNear, RotZ,
            (0f, 0f), (0.4f,  12f), (0.8f, 0f), (1.2f,  12f), (1.6f, 0f));

        // Held, unlike Celebrate's. Nothing follows it on the result screen,
        // and a single key is enough — the curve is stepped, so one frame at
        // t=0 covers every loop.
        FaceCurve(clip, (0f, FaceHappy));
    }

    // Round lost. Fired from the same place as Win.
    //
    // Holds its final pose instead of returning to neutral, and does not
    // loop: the slump IS the animation. Anything cyclical here would read as
    // the monkey repeatedly remembering it lost.
    private static void BuildLose()
    {
        AnimationClip clip = Prepare("Rig_Lose", loop: false);

        Curve(clip, Head, RotZ, (0f, 0f), (0.35f, -14f), (0.7f, -14f));

        // Settles down and spreads slightly — weight going out of the pose.
        Curve(clip, Rig, ScaleY, (0f, 1f), (0.4f, 0.96f), (0.7f, 0.96f));
        Curve(clip, Rig, ScaleX, (0f, 1f), (0.4f, 1.03f), (0.7f, 1.03f));

        // Signs mirrored, same as Celebrate but downward: positive Z drops
        // the left arm and lifts the right.
        Curve(clip, ArmFar,  RotZ, (0f, 0f), (0.5f,  8f), (0.7f,  8f));
        Curve(clip, ArmNear, RotZ, (0f, 0f), (0.5f, -8f), (0.7f, -8f));

        Curve(clip, Tail, RotZ, (0f, 0f), (0.6f, -12f), (0.7f, -12f));

        // Splay outward and hold: the legs giving way is the same gesture as
        // the torso spreading, so they settle on the same beat.
        Curve(clip, LegFar,  RotZ, (0f, 0f), (0.5f, -9f), (0.7f, -9f));
        Curve(clip, LegNear, RotZ, (0f, 0f), (0.5f,  9f), (0.7f,  9f));

        // Elbows buckle inward on the same beat and hold with everything else.
        Curve(clip, ForearmFar,  RotZ, (0f, 0f), (0.5f,  10f), (0.7f,  10f));
        Curve(clip, ForearmNear, RotZ, (0f, 0f), (0.5f, -10f), (0.7f, -10f));

        // Holds, like the rest of the pose.
        FaceCurve(clip, (0f, FaceAngry));
    }

    // ---- plumbing ---------------------------------------------------------

    private static AnimationClip Prepare(string name, bool loop)
    {
        string path = Dir + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            // Rebuild from scratch so re-running cannot leave a curve behind
            // from an earlier version of the recipe.
            clip.ClearCurves();
        }

        clip.frameRate = 60f;

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        return clip;
    }

    private static void Curve(AnimationClip clip, string path, string property,
        params (float time, float value)[] keys)
    {
        var curve = new AnimationCurve();
        foreach ((float time, float value) k in keys)
            curve.AddKey(k.time, k.value);

        // Auto tangents on every key. The motion here is all easing rather
        // than snapping, so smooth is the right default; if a clip ever wants
        // a hard cut, set that key's tangents to Constant by hand afterwards.
        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0f);

        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(RectTransform), property),
            curve);
    }

    // Sprite swaps on a part, as an object-reference curve. Unity keeps
    // these in a different list from the float curves (m_PPtrCurves rather
    // than m_EulerCurves), but one clip can hold both — which is the whole
    // point here: the body moves by transform while the face cuts between
    // drawings.
    //
    // Sprite keys are stepped, not interpolated; a sprite at t=0.2 simply
    // replaces the previous one at that frame.
    private static void FaceCurve(AnimationClip clip, params (float time, string sprite)[] keys)
    {
        var frames = new ObjectReferenceKeyframe[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            string path = FaceDir + keys[i].sprite + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"MascotRigClipBuilder: sprite not found at '{path}' — " +
                                 $"'{clip.name}' will have no face curve.");
                return;
            }
            frames[i] = new ObjectReferenceKeyframe { time = keys[i].time, value = sprite };
        }

        AnimationUtility.SetObjectReferenceCurve(
            clip,
            EditorCurveBinding.PPtrCurve(Face, typeof(Image), "m_Sprite"),
            frames);
    }

    // ---- optional cleanup -------------------------------------------------

    // The sprite-based clips animate Image.Sprite on the mascot root, which
    // the rig no longer has — that is the "Missing!" row in the Animation
    // window. This clears it from every clip in the folder.
    //
    // Deliberately a SEPARATE menu item, and deliberately not run by
    // BuildAll: it makes those clips useless to the old MascotImage prefab,
    // which is still sitting in GameScene as your fallback. Only run it once
    // you are confident the rig has replaced it. Everything is in git either
    // way.
    [MenuItem("Tools/Hangulnoree/Clear Missing Sprite Curves (destructive)")]
    public static void ClearMissingSpriteCurves()
    {
        if (!EditorUtility.DisplayDialog(
                "Clear sprite curves?",
                "This removes the Image.Sprite curves from every clip in Assets/Animations.\n\n" +
                "The old MascotImage prefab's animations will stop working. Continue?",
                "Clear", "Cancel"))
            return;

        int cleared = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Animations" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;

            foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                AnimationUtility.SetObjectReferenceCurve(clip, b, null);
                cleared++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Cleared {cleared} sprite curve(s) from Assets/Animations.");
    }
}
