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

        Apply(GameManager.LastFinalScore, GameManager.LastWordsCompleted, successPanel, failPanel, scoreText, resultText);
        

        if (GameManager.LastWordsCompleted > 0) TriggerMascotWin();

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

    // The mascot celebrates in place via its Animator "Win" state. It used
    // to also be driven across the screen by a SwingMascot coroutine that
    // overwrote anchoredPosition and localRotation every frame; that was
    // removed deliberately. Anything that needs the mascot to move belongs
    // in the animation clip, not in a coroutine fighting the RectTransform.
    private void TriggerMascotWin()
    {
        GameObject mascot = GameObject.Find("MascotImage");
        if (mascot == null)
            Debug.LogWarning("ResultSceneController: MascotImage not found (or inactive) — win animation is disabled.");
        Animator animator = mascot != null ? mascot.GetComponent<Animator>() : null;
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(MascotWinTrigger);
    }
}
