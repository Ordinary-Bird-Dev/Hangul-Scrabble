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
    public const string HowToPlayScene = "HowToPlayScene";
    public const string HangulBasicsScene = "HangulBasicsScene";

    // Set this immediately before loading SettingScene. Defaults to the
    // title so an unset route can never strand the player mid-round.
    public static string ReturnScene { get; set; } = TitleScene;

    // Load SettingScene, remembering where to come back to.
    public static void OpenSettings(string returnScene)
    {
        ReturnScene = string.IsNullOrEmpty(returnScene) ? TitleScene : returnScene;
        SceneManager.LoadScene(SettingScene);
    }

    // True when GameScene should run the tutorial instead of a real round.
    //
    // Not a fourth GameMode and not PlayerPrefs, for the same reason
    // ReturnScene is neither. GameSettings.Mode is the player's CHOSEN
    // mode, shown by three rows in Settings; writing Tutorial into it would
    // overwrite that choice and leave those rows describing something they
    // never picked. This is where they are going next, not what they prefer.
    public static bool TutorialRequested { get; private set; }

    // The two ways into GameScene. Both set the flag explicitly rather than
    // only one setting it true — a value left over from a previous visit
    // would turn an ordinary round into a tutorial.
    public static void StartTutorial()
    {
        TutorialRequested = true;
        SceneManager.LoadScene(GameScene);
    }

    public static void StartGame()
    {
        TutorialRequested = false;
        SceneManager.LoadScene(GameScene);
    }

    // Abandoning the tutorial via Skip. Goes to the title rather than into a
    // round: a player who bailed out has not been taught anything yet, and
    // dropping them straight into a timed game would be the opposite of what
    // pressing Skip asked for. Finishing all the lessons goes to GameScene
    // instead — see TutorialController.Finish, which calls StartGame.
    public static void ExitTutorial()
    {
        TutorialRequested = false;
        SceneManager.LoadScene(TitleScene);
    }
}
