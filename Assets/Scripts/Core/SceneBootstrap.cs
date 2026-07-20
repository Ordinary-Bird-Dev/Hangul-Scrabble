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
            GameObject prefab = Resources.Load<GameObject>("GameController");
            GameObject controller;

            if (prefab != null)
            {
                controller = Object.Instantiate(prefab);
                controller.name = "GameController";
            }
            else
            {
                Debug.LogWarning("SceneBootstrap: GameController prefab not found in Resources — falling back to a runtime-built controller with no audio clips assigned.");
                controller = new GameObject("GameController");
                controller.AddComponent<TileManager>();
                controller.AddComponent<WordBuilder>();
                controller.AddComponent<GameManager>();
                controller.AddComponent<AudioManager>();
            }

            if (GameSettings.Mode == GameMode.Zen)
                controller.AddComponent<WordJournal>();
            else if (GameSettings.Mode == GameMode.WordHunt)
                controller.AddComponent<WordHuntController>();
        }

        // SyllableBuilderUI is deliberately not bundled with GameController:
        // it belongs on the scene's SyllableBuilder object with its
        // serialized ConfirmButton reference. This guard only covers a
        // scene that ships without one.
        EnsureController<SyllableBuilderUI>();

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
