using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen: routes to gameplay, the how-to-play page, or settings.
public class TitlePageController : MonoBehaviour
{
    void Start()
    {
        // Neither Hangul Basics nor the Tutorial is here. Both hang off How
        // to Play, which is the single learning entry point, so the title
        // stays at two real choices plus the gear. InfoPageController owns
        // those two links.
        //
        // Play goes through StartGame, not a bare LoadScene, because that is
        // what clears the tutorial flag. Without it, leaving the tutorial and
        // then pressing Play would start a second tutorial.
        WireButton("PlayButton", SceneRouter.StartGame);
        WireButton("HowToPlayButton", () => SceneManager.LoadScene(SceneRouter.HowToPlayScene));
        WireButton("SettingsButton", () => SceneRouter.OpenSettings(SceneRouter.TitleScene));
    }

    private static void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject go = GameObject.Find(name);
        Button button = go != null ? go.GetComponent<Button>() : null;
        if (button != null)
            button.onClick.AddListener(action);
        else
            Debug.LogWarning($"TitlePageController: '{name}' not found or has no Button — that route is dead. (Note: GameObject.Find cannot see inactive objects.)");
    }
}