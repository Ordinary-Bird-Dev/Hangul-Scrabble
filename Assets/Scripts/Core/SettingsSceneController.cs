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
        // Back returns to whoever opened Settings — the title screen or a
        // live round — via SceneRouter, never unconditionally to GameScene.
        if (backButton != null)
            backButton.onClick.AddListener(() => SceneManager.LoadScene(SceneRouter.ReturnScene));
        else
            Debug.LogWarning("SettingsSceneController: BackButton not found (or inactive) — back navigation is disabled.");

        ResolveModeSelect();
        ResolveLevelSelect();
    }

    // Row states. Selected = orange border plate + solid white face + a
    // filled dot; unselected = no border, slightly translucent face, no dot.
    private static readonly Color RowBorderOn   = Palette.Action;
    private static readonly Color RowBorderOff  = new Color(1f, 1f, 1f, 0f);
    private static readonly Color RowFaceOn     = Palette.Surface;
    private static readonly Color RowFaceOff    = Palette.SurfaceMuted;
    private static readonly Color RowDotOn      = Palette.Action;
    private static readonly Color RowDotOff     = new Color(1f, 1f, 1f, 0f);

    // Fallback-only colours: the runtime row is a single flat chip with no
    // border plate, so it cannot express the scene-authored look.
    private static readonly Color ModeNormal = Palette.SurfaceMuted;
    private static readonly Color ModeSelected = Palette.Action;

    private struct ModeRow
    {
        public GameMode Mode;
        public Image Border;   // outer plate; null on the runtime fallback
        public Image Face;     // the white body
        public Image Dot;      // selection dot; null on the runtime fallback
    }

    private readonly List<ModeRow> _modeRows = new List<ModeRow>();

    // Scene-authored rows win. GameScene's clue banner works the same way:
    // if the scene provides the objects the Inspector owns their look, and
    // the runtime build below is only the fallback for a scene that ships
    // without them.
    private void ResolveModeSelect()
    {
        bool allFound = true;
        foreach (GameMode mode in new[] { GameMode.Classic, GameMode.WordHunt, GameMode.Zen })
        {
            GameObject row = FindIncludingInactive($"Mode_{mode}");
            if (row == null) { allFound = false; break; }

            Button button = row.GetComponent<Button>();
            Image border = row.GetComponent<Image>();
            Transform inner = row.transform.Find("Inner");
            Image face = inner != null ? inner.GetComponent<Image>() : null;
            Transform dotT = inner != null ? inner.Find("Dot") : null;
            Image dot = dotT != null ? dotT.GetComponent<Image>() : null;

            if (button == null || face == null) { allFound = false; break; }

            GameMode captured = mode;
            button.onClick.AddListener(() =>
            {
                GameSettings.Mode = captured;
                RefreshModeButtons();
            });
            _modeRows.Add(new ModeRow { Mode = mode, Border = border, Face = face, Dot = dot });
        }

        if (!allFound)
        {
            _modeRows.Clear();
            BuildModeSelect();
            return;
        }

        RefreshModeButtons();
    }

    // Runtime fallback: a flat row of chips, used only when the scene has no
    // Mode_* objects of its own.
    private void BuildModeSelect()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        var rowGo = new GameObject("ModeSelect", typeof(RectTransform));
        rowGo.transform.SetParent(canvas.transform, false);

        var rowRect = (RectTransform)rowGo.transform;
        rowRect.anchorMin = new Vector2(0.5f, 1f);
        rowRect.anchorMax = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -700f);
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
        text.color = Palette.Ink;
        text.fontSize = 34f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (font != null) text.font = font;

        _modeRows.Add(new ModeRow { Mode = mode, Border = null, Face = bg, Dot = null });
    }

    private void RefreshModeButtons()
    {
        GameMode current = GameSettings.Mode;
        foreach (ModeRow row in _modeRows)
        {
            bool on = row.Mode == current;

            // The fallback chip has no border plate, so its single Image is
            // the thing that has to change colour. A scene-authored row keeps
            // its face white and shows selection on the border and the dot.
            if (row.Border == null)
            {
                if (row.Face != null) row.Face.color = on ? ModeSelected : ModeNormal;
                continue;
            }

            row.Border.color = on ? RowBorderOn : RowBorderOff;
            if (row.Face != null) row.Face.color = on ? RowFaceOn : RowFaceOff;
            if (row.Dot != null) row.Dot.color = on ? RowDotOn : RowDotOff;
        }
    }

    private struct LevelRow
    {
        public int Level;
        public Image Border;
        public Image Face;
        public Image Dot;
    }

    private readonly List<LevelRow> _levelRows = new List<LevelRow>();

    // Level rows pick which vocabulary Classic draws its clues from:
    // Level_1 -> topik1, Level_2 -> vocabB, Level_3 -> vocabC.
    //
    // Unlike the mode rows there is no runtime fallback. A scene without
    // Level_* objects simply has no level picker and the game keeps using
    // whatever GameSettings.Level already holds — which is a working game,
    // just without the choice. Building these in code instead would mean a
    // second copy of the row styling that nothing keeps in step with the
    // scene's.
    //
    // Driven by WordValidator.LevelCount rather than a literal 3, so adding
    // a fourth word set and a fourth row needs no change here.
    private void ResolveLevelSelect()
    {
        for (int level = GameSettings.MinLevel; level <= WordValidator.LevelCount; level++)
        {
            GameObject row = FindIncludingInactive($"Level_{level}");
            if (row == null) continue;

            Button button = row.GetComponent<Button>();
            Transform inner = row.transform.Find("Inner");
            Image face = inner != null ? inner.GetComponent<Image>() : null;
            if (button == null || face == null)
            {
                Debug.LogWarning($"SettingsSceneController: 'Level_{level}' is missing its Button or its Inner image — that row will not be selectable.");
                continue;
            }

            Transform dotT = inner.Find("Dot");

            int captured = level;
            button.onClick.AddListener(() =>
            {
                GameSettings.Level = captured;
                RefreshLevelButtons();
            });

            _levelRows.Add(new LevelRow
            {
                Level = captured,
                Border = row.GetComponent<Image>(),
                Face = face,
                Dot = dotT != null ? dotT.GetComponent<Image>() : null,
            });
        }

        RefreshLevelButtons();
    }

    private void RefreshLevelButtons()
    {
        int current = GameSettings.Level;
        foreach (LevelRow row in _levelRows)
        {
            bool on = row.Level == current;
            if (row.Border != null) row.Border.color = on ? RowBorderOn : RowBorderOff;
            if (row.Face != null) row.Face.color = on ? RowFaceOn : RowFaceOff;
            if (row.Dot != null) row.Dot.color = on ? RowDotOn : RowDotOff;
        }
    }

    // GameObject.Find cannot see inactive objects, and a mode row may well
    // start inactive while a screen is being laid out.
    private static GameObject FindIncludingInactive(string name)
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    private static Toggle FindToggle(string name)
    {
        GameObject go = GameObject.Find(name);
        Toggle toggle = go != null ? go.GetComponent<Toggle>() : null;
        if (toggle == null)
            Debug.LogWarning($"SettingsSceneController: {name} not found (or inactive) — setting toggle is not wired.");
        return toggle;
    }
}
