using UnityEngine;

// The integers are persisted in PlayerPrefs and must stay put. The names
// denote the CURRENT player-facing modes: Classic is the guided puzzle
// (meaning clue + exact tile deal), Word Hunt is free-form building from
// a random tray. These two labels were swapped in August 2026 — ask
// GameSettings.IsGuided rather than comparing against a member.
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

    // Which game loop the next GameScene load runs: Classic (timed, guided
    // by a meaning clue), Zen (no timer, word journal), or Word Hunt
    // (timed, free-form building from a random tray).
    public static GameMode Mode
    {
        get => (GameMode)PlayerPrefs.GetInt(GameModeKey, 0);
        set { PlayerPrefs.SetInt(GameModeKey, (int)value); PlayerPrefs.Save(); }
    }

    // The single definition of "guided mode": clue banner, hint button,
    // exact tile deal, refill disabled, re-deal on a wrong answer. This is
    // the ONLY place a mode is compared against a GameMode member — call
    // sites ask this instead, so they cannot drift apart or be silently
    // inverted the next time the player-facing labels move.
    public static bool IsGuided => Mode == GameMode.Classic;
}
