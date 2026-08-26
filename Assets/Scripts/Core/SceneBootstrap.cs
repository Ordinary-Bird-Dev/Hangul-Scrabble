using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runtime wiring: GameScene's controller stack normally comes from
// Resources/GameController.prefab, instantiated here at scene load (a
// controller authored into the scene in the editor is respected instead).
// Mode-specific components — WordJournal for Zen, ClassicModeController
// for Classic — are attached to that controller afterwards, on every load,
// so they follow the mode chosen in Settings regardless of where the
// controller came from. Result/Setting scenes get their controllers
// spawned here too; scene objects are resolved with GameObject.Find.
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
            case "TitleScene":
                EnsureController<TitlePageController>();
                break;
            case "HowToPlayScene":
            case "HangulBasicsScene":
                EnsureController<InfoPageController>();
                break;

        }
    }

    private static void SetupGameScene()
    {
        // Resolve the controller object first: found in the scene, or
        // instantiated from the prefab, or built bare as a last resort.
        GameManager existing = Object.FindAnyObjectByType<GameManager>();
        GameObject controller;

        if (existing != null)
        {
            controller = existing.gameObject;
        }
        else
        {
            GameObject prefab = Resources.Load<GameObject>("GameController");
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
        }

        // Mode components must attach even when the controller already
        // existed — a scene-authored controller once swallowed them and
        // silently disabled Zen's journal and Classic's guided puzzle entirely.
        if (GameSettings.Mode == GameMode.Zen && controller.GetComponent<WordJournal>() == null)
            controller.AddComponent<WordJournal>();
        else if (GameSettings.IsGuided && controller.GetComponent<ClassicModeController>() == null)
            controller.AddComponent<ClassicModeController>();

        // SyllableBuilderUI is deliberately not bundled with GameController:
        // it belongs on the scene's SyllableBuilder object with its
        // serialized ConfirmButton reference. This guard only covers a
        // scene that ships without one.
        EnsureController<SyllableBuilderUI>();

        WireButton("SettingsButton", () => {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveSessionState();
            }
            SceneRouter.OpenSettings(SceneRouter.GameScene);
        });
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
        if (button != null)
            button.onClick.AddListener(action);
        else
            Debug.LogWarning($"SceneBootstrap: {name} not found (or inactive) — button is not wired.");
    }
}
