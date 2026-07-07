using UnityEngine;

public enum GameMode
{
    Classic = 0,
    Zen = 1,
    WordHunt = 2
}

// PlayerPrefs-backed settings, toggled in SettingScene.
public static class GameSettings
{
    private const string SoundKey = "sound_on";
    private const string AutoConfirmKey = "auto_confirm";
    private const string GameModeKey = "game_mode";

    public static bool SoundOn
    {
        get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
        set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Off by default: a completed syllable waits for the WordConfirmButton
    // instead of auto-advancing to the word bar.
    public static bool AutoConfirm
    {
        get => PlayerPrefs.GetInt(AutoConfirmKey, 0) == 1;
        set { PlayerPrefs.SetInt(AutoConfirmKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Which game loop the next GameScene load runs: Classic (timed),
    // Zen (no timer, word journal), or Word Hunt (meaning clues).
    public static GameMode Mode
    {
        get => (GameMode)PlayerPrefs.GetInt(GameModeKey, 0);
        set { PlayerPrefs.SetInt(GameModeKey, (int)value); PlayerPrefs.Save(); }
    }
}
