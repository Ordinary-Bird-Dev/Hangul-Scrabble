using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The tutorial. Runs inside GameScene rather than a scene of its own, and
// drives ClassicModeController through a fixed list of one-syllable words
// instead of the random pool.
//
// Sharing GameScene is deliberate. A duplicated tutorial scene would mean
// every layout fix has to be made twice, and this project already has the
// scar: ResultScene's mascot was a hand-built copy of MascotImage.prefab,
// drifted out of sync, and rendered stretched for months. One scene, one
// layout.
//
// What actually differs from Classic is small: the word order is scripted,
// each word carries a coaching line, the countdown is off, and the score
// and gauge are hidden. SceneBootstrap owns that chrome; this owns the
// script, the coaching, and the two ways out.
public class TutorialController : MonoBehaviour
{
    private struct Lesson
    {
        public string Word;
        public string Coach;
    }

    // Hand-picked, not filtered. Drawing at random from the 461 one-syllable
    // entries would serve up 각 "each" and 건 "matter" as someone's first
    // Korean; these are concrete nouns that can be pictured.
    //
    // The order teaches the tile grammar, not just vocabulary:
    //   1-2  consonant + vowel, and the vowel sits in two different places
    //   3    the final-consonant slot, with ONE jamo doing both jobs
    //   4-5  reinforce, then a compound vowel
    private static readonly Lesson[] Lessons =
    {
        new Lesson { Word = "개", Coach = "Tap the consonant, then the vowel." },
        new Lesson { Word = "코", Coach = "This vowel sits underneath instead." },
        new Lesson { Word = "눈", Coach = "Three tiles now — the last one goes in the bottom slot." },
        new Lesson { Word = "물", Coach = "Same shape again. Consonant, vowel, then the final." },
        new Lesson { Word = "별", Coach = "Last one. This vowel is two strokes joined." },
    };

    private ClassicModeController _classic;
    private bool _finished;

    void Awake()
    {
        // Awake, not Start, and this is the whole reason the wiring works:
        // ClassicModeController picks its first word in Start. Setting the
        // script there would be a race between two components on the same
        // object, which Unity does not order. Awake always precedes Start.
        _classic = GetComponent<ClassicModeController>()
                   ?? gameObject.AddComponent<ClassicModeController>();

        var words = new List<string>(Lessons.Length);
        foreach (Lesson lesson in Lessons) words.Add(lesson.Word);

        _classic.ScriptedWords = words;
        _classic.CoachLine = Lessons[0].Coach;
        _classic.ScriptCompleted += Finish;
    }

    void OnDestroy()
    {
        if (_classic != null) _classic.ScriptCompleted -= Finish;
    }

    void Start()
    {
        // Start, not Awake: SceneBootstrap activates SkipButton after it adds
        // this component, so in Awake the object is still switched off.
        // FindIncludingInactive anyway — GameObject.Find cannot see inactive
        // objects, and that has already been the cause of several dead
        // buttons in this project.
        GameObject skip = FindIncludingInactive("SkipButton");
        Button button = skip != null ? skip.GetComponent<Button>() : null;

        if (button != null)
            button.onClick.AddListener(SceneRouter.ExitTutorial);
        else
            Debug.LogWarning("TutorialController: SkipButton not found or has no Button — the tutorial cannot be left early.");
    }

    void Update()
    {
        // The coaching line follows whichever word ClassicModeController is
        // on. Polled rather than pushed because the controller advances the
        // word itself, inside its own WordCompleted handler, and adding a
        // second event for this would not buy anything a cheap index compare
        // does not already give.
        if (_classic == null || _finished) return;

        int i = _classic.ScriptIndex;
        if (i < 0 || i >= Lessons.Length) return;

        // The setter redraws the banner itself and ignores an unchanged
        // value, so assigning every frame costs nothing.
        _classic.CoachLine = Lessons[i].Coach;
    }

    // Every lesson done — straight into a real round, which is the whole
    // point of having taught them. StartGame rather than ExitTutorial: the
    // same flag clear, but it loads GameScene, and it respects whichever
    // mode Settings has selected rather than forcing Classic.
    //
    // Deliberately NOT ResultScene: that screen reports a score, and the
    // tutorial had no scoring and no round to report on.
    //
    // Skip goes somewhere different — to the title. See the SkipButton
    // wiring in Start, and SceneRouter.ExitTutorial for why.
    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        SceneRouter.StartGame();
    }

    private static GameObject FindIncludingInactive(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }
}
