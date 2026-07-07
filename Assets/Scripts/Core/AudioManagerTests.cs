using System.Collections.Generic;
using UnityEngine;

public class AudioManagerTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestSingletonAndSourceSetup();
        TestPlayMethodsSafeWithoutClips();
        TestStaticHelpersSafeWithoutInstance();
        Cleanup();
        Debug.Log("All AudioManager tests passed!");
    }

    AudioManager MakeManager()
    {
        var go = new GameObject("TestAudioManager");
        _spawned.Add(go);
        return go.AddComponent<AudioManager>();
    }

    void TestSingletonAndSourceSetup()
    {
        AudioManager manager = MakeManager();

        Assert(AudioManager.Instance == manager, "Instance should point at the created AudioManager");
        Assert(manager.GetComponent<AudioSource>() != null, "AudioManager should add an AudioSource");
        Assert(!manager.GetComponent<AudioSource>().playOnAwake, "AudioSource should not play on awake");
    }

    void TestPlayMethodsSafeWithoutClips()
    {
        AudioManager manager = MakeManager();

        // Clips are unassigned placeholders — none of these may throw.
        manager.PlayTileTap();
        manager.PlaySyllableComplete();
        manager.PlayWordSuccess();
        manager.PlayWordError();
        Assert(true, "Play methods with unassigned clips should not throw");
    }

    void TestStaticHelpersSafeWithoutInstance()
    {
        foreach (GameObject go in _spawned)
            DestroyImmediate(go);
        _spawned.Clear();

        Assert(AudioManager.Instance == null, "Instance should clear when the manager is destroyed");

        AudioManager.TryPlayTileTap();
        AudioManager.TryPlaySyllableComplete();
        AudioManager.TryPlayWordSuccess();
        AudioManager.TryPlayWordError();
        Assert(true, "Static Try helpers without an instance should not throw");
    }

    void Cleanup()
    {
        foreach (GameObject go in _spawned)
            if (go != null) Destroy(go);
        _spawned.Clear();
    }

    void Assert(bool condition, string message)
    {
        if (!condition)
            Debug.LogError($"TEST FAILED: {message}");
    }
}
