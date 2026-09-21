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
