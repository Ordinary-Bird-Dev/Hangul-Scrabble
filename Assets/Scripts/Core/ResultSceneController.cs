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

        Apply(GameManager.LastFinalScore, GameManager.LastWordsCompleted, successPanel, failPanel, scoreText);
        FillMeaningCard(GameManager.LastWordEntry);

        WireButton("PlayAgainButton");
        WireButton("TryAgainButton");
    }

    // Pure panel/score logic, separated for unit testing.
    public static void Apply(int score, int wordsCompleted,
        GameObject successPanel, GameObject failPanel, TMP_Text scoreText)
    {
        if (scoreText != null) scoreText.text = $"Score: {score:N0}";

        bool success = wordsCompleted > 0;
        if (successPanel != null) successPanel.SetActive(success);
        if (failPanel != null) failPanel.SetActive(!success);
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
}
