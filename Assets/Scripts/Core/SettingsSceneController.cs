using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Binds the SettingScene toggles to GameSettings and wires BackButton.
// Note: AutoConfirmToggle is labelled "Require syllable confirm", so the
// toggle is the INVERSE of GameSettings.AutoConfirm.
public class SettingsSceneController : MonoBehaviour
{
    void Start()
    {
        Toggle sound = FindToggle("SoundToggle");
        if (sound != null)
        {
            sound.isOn = GameSettings.SoundOn;
            sound.onValueChanged.AddListener(v => GameSettings.SoundOn = v);
        }

        Toggle requireConfirm = FindToggle("AutoConfirmToggle");
        if (requireConfirm != null)
        {
            requireConfirm.isOn = !GameSettings.AutoConfirm;
            requireConfirm.onValueChanged.AddListener(v => GameSettings.AutoConfirm = !v);
        }

        GameObject back = GameObject.Find("BackButton");
        Button backButton = back != null ? back.GetComponent<Button>() : null;
        if (backButton != null)
            backButton.onClick.AddListener(() => SceneManager.LoadScene("GameScene"));
    }

    private static Toggle FindToggle(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<Toggle>() : null;
    }
}
