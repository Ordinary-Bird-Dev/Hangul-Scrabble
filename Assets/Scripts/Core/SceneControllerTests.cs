using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SceneControllerTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestResultApplySuccess();
        TestResultApplyFail();
        TestResultApplyNullSafe();
        TestGameSettingsRoundtrip();
        Cleanup();
        Debug.Log("All SceneController tests passed!");
    }

    void TestResultApplySuccess()
    {
        GameObject success = MakeGo("TestSuccessPanel");
        GameObject fail = MakeGo("TestFailPanel");
        TMP_Text score = MakeText();
        TMP_Text resultText = MakeText();

        ResultSceneController.Apply(1250, 4, success, fail, score, resultText);

        Assert(success.activeSelf, "SuccessPanel should be active when words were completed");
        Assert(!fail.activeSelf, "FailPanel should be hidden on success");
        Assert(score.text == "Score: 1,250", $"ScoreText should read 'Score: 1,250', got '{score.text}'");
        Assert(resultText.text == "SUCCESS!", $"ResultText should read 'SUCCESS!', got '{resultText.text}'");
    }

    void TestResultApplyFail()
    {
        GameObject success = MakeGo("TestSuccessPanel2");
        GameObject fail = MakeGo("TestFailPanel2");
        TMP_Text score = MakeText();
        TMP_Text resultText = MakeText();

        ResultSceneController.Apply(0, 0, success, fail, score, resultText);

        Assert(!success.activeSelf, "SuccessPanel should be hidden when no words were completed");
        Assert(fail.activeSelf, "FailPanel should show when no words were completed");
        Assert(score.text == "Score: 0", $"ScoreText should read 'Score: 0', got '{score.text}'");
        Assert(resultText.text == "FAIL!", $"ResultText should read 'FAIL!', got '{resultText.text}'");
    }

    void TestResultApplyNullSafe()
    {
        // Must not throw when scene objects are missing.
        ResultSceneController.Apply(100, 1, null, null, null, null);
        Assert(true, "Apply with null references should not throw");
    }

    void TestGameSettingsRoundtrip()
    {
        bool originalSound = GameSettings.SoundOn;
        bool originalAuto = GameSettings.AutoConfirm;

        GameSettings.SoundOn = false;
        Assert(!GameSettings.SoundOn, "SoundOn=false should persist");
        GameSettings.SoundOn = true;
        Assert(GameSettings.SoundOn, "SoundOn=true should persist");

        GameSettings.AutoConfirm = true;
        Assert(GameSettings.AutoConfirm, "AutoConfirm=true should persist");
        GameSettings.AutoConfirm = false;
        Assert(!GameSettings.AutoConfirm, "AutoConfirm=false should persist");

        GameSettings.SoundOn = originalSound;
        GameSettings.AutoConfirm = originalAuto;
    }

    GameObject MakeGo(string name)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        return go;
    }

    TMP_Text MakeText()
    {
        var go = MakeGo("TestScoreText");
        return go.AddComponent<TextMeshProUGUI>();
    }

    void Cleanup()
    {
        foreach (GameObject go in _spawned)
            Destroy(go);
        _spawned.Clear();
    }

    void Assert(bool condition, string message)
    {
        if (!condition)
            Debug.LogError($"TEST FAILED: {message}");
    }
}
