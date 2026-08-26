using UnityEngine.SceneManagement;

// Where "Back" goes. SettingScene is reachable from two places — the title
// screen and a live round — and Back must return to whichever one the
// player came from, or Settings→Back from the title drops them into a
// running game.
//
// Deliberately NOT PlayerPrefs-backed. This is navigation state, not a
// preference: a value persisted from a previous run would route the next
// launch somewhere the player never was, and stale PlayerPrefs has already
// produced three false bugs in this project. A cold start always begins at
// TitleScene, so there is nothing worth restoring.
public static class SceneRouter
{
    public const string TitleScene = "TitleScene";
    public const string GameScene = "GameScene";
    public const string SettingScene = "SettingScene";

    // Set this immediately before loading SettingScene. Defaults to the
    // title so an unset route can never strand the player mid-round.
    public static string ReturnScene { get; set; } = TitleScene;

    // Load SettingScene, remembering where to come back to.
    public static void OpenSettings(string returnScene)
    {
        ReturnScene = string.IsNullOrEmpty(returnScene) ? TitleScene : returnScene;
        SceneManager.LoadScene(SettingScene);
    }
}
