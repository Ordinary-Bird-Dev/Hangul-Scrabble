using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HowToPlaySceneController : MonoBehaviour
{
    void Start()
    {
        GameObject go = GameObject.Find("BackButton");
        Button button = go != null ? go.GetComponent<Button>() : null;
        if (button != null)
            button.onClick.AddListener(() => SceneManager.LoadScene("TitleScene"));
        else
            Debug.LogWarning("HowToPlaySceneController: 'BackButton' not found or has no Button.");
    }
}