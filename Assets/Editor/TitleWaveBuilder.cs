using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

// Builds the title screen's waving clip and its controller.
//
// Menu: Tools/Hangulnoree/Build Title Wave
//
// Same reason as MascotRigClipBuilder for going through AnimationUtility
// rather than writing the .anim by hand: a clip's runtime bindings are hashed
// paths and property names, and a clip that binds in the editor but not at
// runtime is worse than no clip. The controller is made here too, so the clip
// asset exists before anything references it.
//
// What it reproduces: the old title animation was three sprite frames on a
// 1.02s loop -- two idle, then one with the arms out and the mouth open. This
// does the same beat, but the arms swing on a hinge and get to wave twice
// while they are up, which three frames could not do.
public static class TitleWaveBuilder
{
    private const string Dir = "Assets/Animations/";
    private const string ClipPath = Dir + "TitleWave.anim";
    private const string CtrlPath = Dir + "TitleMascotRig.controller";

    // Paths relative to the GameObject carrying the Animator.
    private const string Rig      = "Rig";
    private const string ArmFar   = "Rig/ArmFar";
    private const string ArmNear  = "Rig/ArmNear";
    private const string Body     = "Rig/Body";
    private const string HeadOpen = "Rig/HeadOpen";

    private const string RotZ   = "localEulerAnglesRaw.z";
    private const string PosY   = "m_AnchoredPosition.y";
    private const string ScaleY = "m_LocalScale.y";
    private const string Alpha  = "m_Color.a";

    // Arms stay under 50 degrees. Past that the shoulder end swings out from
    // behind the torso and the unfinished cut shows -- the same limit the
    // gameplay rig has, measured the same way.
    private const float Up   = 42f;
    private const float Wave = 52f;

    [MenuItem("Tools/Hangulnoree/Build Title Wave")]
    public static void Build()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        else
        {
            clip.ClearCurves();
        }

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Signs mirror: negative lifts the left arm, positive the right, the
        // same convention as the gameplay rig so the two read alike.
        Curve(clip, ArmFar, RotZ,
            (0f, 0f), (0.30f, -Up), (0.55f, -Wave), (0.80f, -Up),
            (1.05f, -Wave), (1.30f, -Up), (1.70f, 0f), (2.20f, 0f));
        Curve(clip, ArmNear, RotZ,
            (0f, 0f), (0.30f, Up), (0.55f, Wave), (0.80f, Up),
            (1.05f, Wave), (1.30f, Up), (1.70f, 0f), (2.20f, 0f));

        // The mouth opens with the arms and closes as they come down. Stepped
        // hard rather than faded: crossfading two drawings of the same head
        // reads as a double exposure.
        Step(clip, HeadOpen, Alpha, (0f, 0f), (0.26f, 1f), (1.52f, 0f), (2.20f, 0f));

        // A small lift on the beat the arms reach the top, so the wave carries
        // through the body instead of only the shoulders.
        Curve(clip, Rig, PosY,
            (0f, 0f), (0.30f, 7f), (0.55f, 2f), (0.80f, 7f),
            (1.05f, 2f), (1.30f, 6f), (1.70f, 0f), (2.20f, 0f));
        Curve(clip, Body, ScaleY,
            (0f, 1f), (0.30f, 1.012f), (0.80f, 1.004f), (1.30f, 1.012f),
            (1.70f, 1f), (2.20f, 1f));

        EditorUtility.SetDirty(clip);

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        var sm = ctrl.layers[0].stateMachine;
        AnimatorState state = null;
        foreach (var cs in sm.states)
            if (cs.state.name == "Wave") state = cs.state;
        if (state == null) state = sm.AddState("Wave");
        state.motion = clip;
        sm.defaultState = state;
        EditorUtility.SetDirty(ctrl);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Title wave built: " + ClipPath + " + " + CtrlPath +
                  "  (2.2s loop; assign the controller to TitleMascotRig's Animator)");
    }

    private static void Curve(AnimationClip clip, string path, string prop,
                              params (float t, float v)[] keys)
    {
        var c = new AnimationCurve();
        foreach (var k in keys) c.AddKey(k.t, k.v);
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(RectTransform), prop), c);
    }

    // Constant tangents: the mouth must cut, not slide.
    private static void Step(AnimationClip clip, string path, string prop,
                             params (float t, float v)[] keys)
    {
        var c = new AnimationCurve();
        foreach (var k in keys) c.AddKey(k.t, k.v);
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Constant);
        }
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(Image), prop), c);
    }
}
