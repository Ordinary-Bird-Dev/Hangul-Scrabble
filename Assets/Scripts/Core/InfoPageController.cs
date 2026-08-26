using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shared by HowToPlayScene and HangulBasicsScene — both are static
// info pages whose only control is a Back button returning to the title.
public class InfoPageController : MonoBehaviour
{
    void Start()
    {
        GameObject go = GameObject.Find("BackButton");
        Button button = go != null ? go.GetComponent<Button>() : null;

        if (button != null)
            button.onClick.AddListener(() => SceneManager.LoadScene("TitleScene"));
        else
            Debug.LogWarning("InfoPageController: 'BackButton' not found or has no Button — that route is dead. (Note: GameObject.Find cannot see inactive objects.)");
    }
}