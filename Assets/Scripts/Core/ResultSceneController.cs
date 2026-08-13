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

        WireButton("PlayAgainButton");
        WireButton("TryAgainButton");
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

    private static void WireButton(string name)
    {
        GameObject go = FindAnywhere(name);
        Button button = go != null ? go.GetComponent<Button>() : null;
        if (button != null)
            button.onClick.AddListener(() => SceneManager.LoadScene("GameScene"));
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
