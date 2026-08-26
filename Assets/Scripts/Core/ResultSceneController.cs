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
        FillMeaningCard(GameManager.LastWordEntry);

        if (GameManager.LastWordsCompleted > 0) TriggerMascotWin();

        // Play Again / Try Again start a fresh round. Returning to the
        // title is a separate, always-visible route.
        WireButton("PlayAgainButton", SceneRouter.GameScene);
        WireButton("TryAgainButton", SceneRouter.GameScene);
        WireMenuButton();
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

    private void FillMeaningCard(WordEntry entry)
    {
        if (entry == null) return;

        SetText("WordText", entry.word);
        SetText("MeaningText", entry.english);
        SetText("RomanizationText", entry.romanization);
        SetText("ExampleText", entry.example);
    }

    private static void SetText(string name, string value)
    {
        TMP_Text text = FindText(name);
        if (text != null && !string.IsNullOrEmpty(value)) text.text = value;
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

    private const string MenuButtonName = "MenuButton";
    private static readonly Color MenuButtonColor = new Color(0.25f, 0.25f, 0.32f, 1f);

    // PlayAgainButton lives in SuccessPanel and TryAgainButton in FailPanel,
    // so exactly one of them is ever visible. The route back to the title
    // must show in both states, so it is parented to the Canvas root rather
    // than to either panel.
    //
    // An object named "MenuButton" authored in the scene wins; the runtime
    // build below is only the fallback for a ResultScene that ships without
    // one, and its placement is a plain bottom-centre guess worth tuning in
    // the editor.
    private void WireMenuButton()
    {
        GameObject existing = FindAnywhere(MenuButtonName);
        Button existingButton = existing != null ? existing.GetComponent<Button>() : null;
        if (existingButton != null)
        {
            existingButton.onClick.AddListener(() => SceneManager.LoadScene(SceneRouter.TitleScene));
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("ResultSceneController: no Canvas in ResultScene — the main-menu route could not be built.");
            return;
        }

        var buttonGo = new GameObject(MenuButtonName, typeof(RectTransform));
        buttonGo.transform.SetParent(canvas.transform, false);

        var rect = (RectTransform)buttonGo.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 60f);
        rect.sizeDelta = new Vector2(360f, 96f);

        Image background = buttonGo.AddComponent<Image>();
        background.color = MenuButtonColor;

        Button button = buttonGo.AddComponent<Button>();
        button.onClick.AddListener(() => SceneManager.LoadScene(SceneRouter.TitleScene));

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(buttonGo.transform, false);
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "Main Menu";
        label.fontSize = 34f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        TMP_Text donor = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        if (donor != null && donor.font != null) label.font = donor.font;
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
    private const float SwingRangeX = 300f;   // pixels each direction
    private const float SwingSpeed = 0.6f;    // cycles per second
    private const float SwingTiltDegrees = 12f;

    private void TriggerMascotWin()
    {
        GameObject mascot = GameObject.Find("MascotImage");
        if (mascot == null)
            Debug.LogWarning("ResultSceneController: MascotImage not found (or inactive) — win animation is disabled.");
        Animator animator = mascot != null ? mascot.GetComponent<Animator>() : null;
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(MascotWinTrigger);

        RectTransform rect = mascot != null ? mascot.GetComponent<RectTransform>() : null;
        if (rect != null) StartCoroutine(SwingMascot(rect));
    }

    private System.Collections.IEnumerator SwingMascot(RectTransform rect)
    {
        Vector2 basePos = rect.anchoredPosition;
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * SwingSpeed;
            float wave = Mathf.Sin(t * Mathf.PI * 2f);
            rect.anchoredPosition = basePos + new Vector2(wave * SwingRangeX, 0f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -wave * SwingTiltDegrees);
            yield return null;
        }
    }
}
