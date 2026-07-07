using System.Collections.Generic;
using TMPro;
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

        BuildModeSelect();
    }

    private static readonly Color ModeNormal = new Color(0.25f, 0.25f, 0.32f, 1f);
    private static readonly Color ModeSelected = new Color(0.2f, 0.55f, 0.35f, 1f);

    private readonly List<(GameMode mode, Image background)> _modeButtons =
        new List<(GameMode, Image)>();

    // Mode select: a row of Classic / Zen / Word Hunt buttons built at
    // runtime; the chosen mode applies on the next GameScene load.
    private void BuildModeSelect()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        var rowGo = new GameObject("ModeSelect", typeof(RectTransform));
        rowGo.transform.SetParent(canvas.transform, false);

        var rowRect = (RectTransform)rowGo.transform;
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, -320f);
        rowRect.sizeDelta = new Vector2(920f, 110f);

        var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        TMP_Text donor = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        TMP_FontAsset font = donor != null ? donor.font : null;

        AddModeButton(rowGo.transform, "Classic", GameMode.Classic, font);
        AddModeButton(rowGo.transform, "Zen", GameMode.Zen, font);
        AddModeButton(rowGo.transform, "Word Hunt", GameMode.WordHunt, font);
        RefreshModeButtons();
    }

    private void AddModeButton(Transform parent, string label, GameMode mode, TMP_FontAsset font)
    {
        var buttonGo = new GameObject($"Mode_{mode}", typeof(RectTransform));
        buttonGo.transform.SetParent(parent, false);

        Image bg = buttonGo.AddComponent<Image>();
        Button button = buttonGo.AddComponent<Button>();
        button.onClick.AddListener(() =>
        {
            GameSettings.Mode = mode;
            RefreshModeButtons();
        });

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(buttonGo.transform, false);
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var text = labelGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 34f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (font != null) text.font = font;

        _modeButtons.Add((mode, bg));
    }

    private void RefreshModeButtons()
    {
        GameMode current = GameSettings.Mode;
        foreach ((GameMode mode, Image background) in _modeButtons)
            if (background != null)
                background.color = mode == current ? ModeSelected : ModeNormal;
    }

    private static Toggle FindToggle(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<Toggle>() : null;
    }
}
