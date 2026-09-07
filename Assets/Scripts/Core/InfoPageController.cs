using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shared by HowToPlayScene and HangulBasicsScene — both are static info
// pages whose main control is a Back button returning to the title.
//
// How to Play carries one extra control: a link on to Hangul Basics.
// That link is the ONLY route to HangulBasicsScene now that the title
// screen is down to Play + How to Play + the gear, so if it stops
// resolving the alphabet primer becomes unreachable rather than merely
// inconvenient — hence the loud warning below.
public class InfoPageController : MonoBehaviour
{
    private const string BackButtonName = "BackButton";
    private const string BasicsButtonName = "HangulBasicsButton";

    void Start()
    {
        WireButton(BackButtonName, SceneRouter.TitleScene, required: true);

        // Expected on How to Play and nowhere else: Hangul Basics does not
        // link to itself. Checking the scene keeps the "missing name is a
        // fault" rule intact without warning on the page where its absence
        // is correct.
        if (SceneManager.GetActiveScene().name == SceneRouter.HowToPlayScene)
            WireButton(BasicsButtonName, SceneRouter.HangulBasicsScene, required: true);
    }

    private static void WireButton(string name, string targetScene, bool required)
    {
        GameObject go = FindIncludingInactive(name);
        Button button = go != null ? go.GetComponent<Button>() : null;

        if (button != null)
        {
            button.onClick.AddListener(() => SceneManager.LoadScene(targetScene));
            return;
        }

        if (required)
            Debug.LogWarning($"InfoPageController: '{name}' not found or has no Button — the route to {targetScene} is dead.");
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
