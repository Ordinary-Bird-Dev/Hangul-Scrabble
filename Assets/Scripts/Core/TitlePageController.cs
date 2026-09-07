using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen: routes to gameplay, the how-to-play page, or settings.
public class TitlePageController : MonoBehaviour
{
    void Start()
    {
        // Hangul Basics is deliberately NOT here: it is reached from inside
        // How to Play, so the title keeps two real choices plus the gear.
        // InfoPageController owns that link.
        WireButton("PlayButton", () => SceneManager.LoadScene(SceneRouter.GameScene));
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