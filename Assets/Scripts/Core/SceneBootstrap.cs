using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runtime wiring for Classic Mode: creates the controller objects each
// scene needs so the scenes stay plain layout wireframes. If a scene
// already contains a controller (added in the editor), nothing is added.
public static class SceneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case "GameScene":
                SetupGameScene();
                break;
            case "ResultScene":
                EnsureController<ResultSceneController>();
                break;
            case "SettingScene":
            case "SettingsScene":
                EnsureController<SettingsSceneController>();
                break;
        }
    }

    private static void SetupGameScene()
    {
        if (Object.FindAnyObjectByType<GameManager>() == null)
        {
            var controller = new GameObject("GameController");
            controller.AddComponent<SyllableBuilderUI>();
            controller.AddComponent<TileManager>();
            controller.AddComponent<WordBuilder>();
            controller.AddComponent<GameManager>();
            controller.AddComponent<AudioManager>();

            if (GameSettings.Mode == GameMode.Zen)
                controller.AddComponent<WordJournal>();
        }

        WireButton("SettingsButton", () => SceneManager.LoadScene("SettingScene"));
    }

    private static void EnsureController<T>() where T : Component
    {
        if (Object.FindAnyObjectByType<T>() != null) return;
        new GameObject(typeof(T).Name).AddComponent<T>();
    }

    private static void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject go = GameObject.Find(name);
        Button button = go != null ? go.GetComponent<Button>() : null;
        if (button != null) button.onClick.AddListener(action);
    }
}
