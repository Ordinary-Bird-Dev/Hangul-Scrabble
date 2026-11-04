using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shared by HowToPlayScene and HangulBasicsScene.
//
// How to Play is the learning hub: it is the only entry the title screen
// offers besides Play and the gear, and everything else for a new player
// hangs off it —
//
//     Title ──"How to Play"──> HowToPlayScene ──> HangulBasicsScene
//                                     │                   │
//                                     │                   └──Back──┐
//                                     ├──"Try the Tutorial"──>     │
//                                     │      GameScene (tutorial)  │
//                                     └<───────────────────────────┘
//                                     Back ──> TitleScene
//
// So both links out of How to Play are the ONLY routes to their targets.
// If either stops resolving, that whole branch becomes unreachable rather
// than merely inconvenient — hence the loud warnings.
public class InfoPageController : MonoBehaviour
{
    private const string BackButtonName = "BackButton";
    private const string BasicsButtonName = "HangulBasicsButton";
    private const string TutorialButtonName = "TutorialButton";
    private const string VocabCreditName = "VocabCreditText";

    // Required attribution, not decoration. The vocabulary is krdict,
    // released under CC BY-SA 2.0 Korea, whose condition is that "the
    // copyright holder must be indicated when using the material".
    // Naming the work and linking the source is the conventional way to
    // satisfy that; the licence prescribes no exact format.
    //
    // Keep the holder, the work title, the licence and the source URL —
    // dropping any one of them puts the build out of compliance.
    public const string VocabCredit =
        "Vocabulary from 한국어기초사전 (Korean Basic Dictionary), " +
        "National Institute of Korean Language — krdict.korean.go.kr — " +
        "used under CC BY-SA 2.0 KR.";

    void Start()
    {
        bool onHowToPlay = SceneManager.GetActiveScene().name == SceneRouter.HowToPlayScene;

        // Back goes where the player came from, not to a fixed home.
        // Hangul Basics is reachable ONLY from How to Play, so sending its
        // Back to the title would strand them: they would have to go
        // Title -> How to Play again just to reach the tutorial.
        WireScene(BackButtonName,
            onHowToPlay ? SceneRouter.TitleScene : SceneRouter.HowToPlayScene,
            required: true);

        FillVocabCredit();

        // The two onward links live on How to Play and nowhere else —
        // Hangul Basics does not link to itself, and checking the scene
        // keeps "a missing name is a fault" true without warning on the
        // page where the absence is correct.
        if (!onHowToPlay) return;

        WireScene(BasicsButtonName, SceneRouter.HangulBasicsScene, required: true);

        // Not a plain scene load: StartTutorial sets the flag SceneBootstrap
        // reads to run GameScene as a lesson instead of a round.
        WireAction(TutorialButtonName, SceneRouter.StartTutorial, required: true);
    }

    // Fills the credit label on whichever info page carries it. Deliberately
    // not built at runtime the way the clue banner is: this text has to be
    // legible and placed, and a label this component invented would land on
    // top of whatever the scene already has there.
    //
    // The warning is loud because the consequence is a licence breach, not
    // a cosmetic gap. If it fires on both info scenes, the build is
    // shipping krdict vocabulary with no attribution anywhere.
    private void FillVocabCredit()
    {
        GameObject go = FindIncludingInactive(VocabCreditName);
        TMP_Text label = go != null ? go.GetComponent<TMP_Text>() : null;

        if (label == null)
        {
            Debug.LogWarning(
                $"InfoPageController: no '{VocabCreditName}' TMP_Text on " +
                $"{SceneManager.GetActiveScene().name} — the krdict attribution " +
                "is not being shown. CC BY-SA 2.0 KR requires it; add the label " +
                "to one of the info pages before shipping.");
            return;
        }

        label.text = VocabCredit;
    }

    private static void WireScene(string name, string targetScene, bool required)
    {
        WireAction(name, () => SceneManager.LoadScene(targetScene), required, targetScene);
    }

    private static void WireAction(string name, UnityEngine.Events.UnityAction action,
        bool required, string describeTarget = null)
    {
        GameObject go = FindIncludingInactive(name);
        Button button = go != null ? go.GetComponent<Button>() : null;

        if (button != null)
        {
            button.onClick.AddListener(action);
            return;
        }

        if (required)
        {
            string where = describeTarget ?? "its destination";
            Debug.LogWarning($"InfoPageController: '{name}' not found or has no Button — the route to {where} is dead.");
        }
    }

    // Not GameObject.Find: it cannot see inactive objects, which is how
    // WordConfirmButton stayed broken for months in this project. A link
    // that starts inactive is still a link.
    private static GameObject FindIncludingInactive(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }
}
