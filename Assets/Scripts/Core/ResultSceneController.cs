using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fills ResultScene with the finished round: final score, success/fail
// panel, and the meaning card of the last completed word.
public class ResultSceneController : MonoBehaviour
{

    void Start()
    {
        GameObject successPanel = FindAnywhere("SuccessPanel");
        GameObject failPanel = FindAnywhere("FailPanel");
        TMP_Text scoreText = FindText("ScoreText");
        TMP_Text resultText = FindText("ResultText");

        bool success = GameManager.LastWordsCompleted > 0;
        Apply(GameManager.LastFinalScore, GameManager.LastWordsCompleted, successPanel, failPanel, scoreText, resultText);

        // Both outcomes animate. Firing only on success left the mascot
        // sitting in Idle behind a "FAIL!" panel, which read as the screen
        // being half-finished rather than as a loss.
        TriggerMascot(success ? MascotWinTrigger : MascotLoseTrigger);

        // The two exits are split by outcome: after a successful round
        // Play Again starts another one, and after a failed round Try Again
        // returns to the title. These are the ONLY routes out of this
        // scene — there is no separate main-menu button — so if either
        // stops resolving the player is stranded here.
        WireButton("PlayAgainButton", SceneRouter.GameScene);
        WireButton("TryAgainButton", SceneRouter.TitleScene);
    }

    // Pure panel/score logic, separated for unit testing.
    public static void Apply(int score, int wordsCompleted,
        GameObject successPanel, GameObject failPanel, TMP_Text scoreText, TMP_Text resultText)
    {
        if (scoreText != null) scoreText.text = $"Score: {score:N0}";

        bool success = wordsCompleted > 0;
        if (successPanel != null) successPanel.SetActive(success);
        if (failPanel != null) failPanel.SetActive(!success);
        if (resultText != null) resultText.text = success ? "SUCCESS!" : "FAIL!";
    }



    

    private static TMP_Text FindText(string name)
    {
        GameObject go = FindAnywhere(name);
        return go != null ? go.GetComponent<TMP_Text>() : null;
    }

    private static void WireButton(string name, string targetScene)
    {
        GameObject go = FindAnywhere(name);
        Button button = go != null ? go.GetComponent<Button>() : null;
        if (button != null)
            button.onClick.AddListener(() => SceneManager.LoadScene(targetScene));
        else
            Debug.LogWarning($"ResultSceneController: '{name}' not found or has no Button — that route is dead.");
    }

    // GameObject.Find skips inactive objects (FailPanel starts inactive),
    // so search every transform in the active scene instead.
    private static GameObject FindAnywhere(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    private const string MascotWinTrigger = "Win";
    private const string MascotLoseTrigger = "Lose";

    // The mascot reacts in place via its Animator. MascotAnimator has an
    // AnyState transition for each of these triggers, so the state it is
    // currently sitting in does not matter.
    //
    // It used to also be driven across the screen by a SwingMascot coroutine
    // that overwrote anchoredPosition and localRotation every frame; that was
    // removed deliberately. Anything that needs the mascot to move belongs
    // in the animation clip, not in a coroutine fighting the RectTransform.
    private void TriggerMascot(string trigger)
    {
        // FindAnywhere, not GameObject.Find: the latter skips inactive
        // objects, and the mascot sits inside a panel that one outcome hides.
        GameObject mascot = FindAnywhere("MascotImage");
        if (mascot == null)
        {
            Debug.LogWarning($"ResultSceneController: MascotImage not found — the '{trigger}' animation is disabled.");
            return;
        }

        Animator animator = mascot.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"ResultSceneController: MascotImage has no Animator Controller — the '{trigger}' animation is disabled.");
            return;
        }

        animator.SetTrigger(trigger);
    }
}
