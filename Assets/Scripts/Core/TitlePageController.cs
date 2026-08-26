using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen: routes to gameplay, the how-to-play page, or settings.
public class TitlePageController : MonoBehaviour
{
    void Start()
    {
        WireButton("PlayButton", () => SceneManager.LoadScene("GameScene"));
        WireButton("HangulBasicsButton", () => SceneManager.LoadScene("HangulBasicsScene"));
        WireButton("HowToPlayButton", () => SceneManager.LoadScene("HowToPlayScene"));
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