using UnityEngine;

// PlayerPrefs-backed settings, toggled in SettingScene.
public static class GameSettings
{
    private const string SoundKey = "sound_on";
    private const string AutoConfirmKey = "auto_confirm";

    public static bool SoundOn
    {
        get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
        set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Off by default: a completed syllable waits for the ConfirmButton
    // instead of auto-advancing to the word bar.
    public static bool AutoConfirm
    {
        get => PlayerPrefs.GetInt(AutoConfirmKey, 0) == 1;
        set { PlayerPrefs.SetInt(AutoConfirmKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
