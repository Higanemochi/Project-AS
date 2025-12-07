using UnityEngine;

public class GameManager : MonoBehaviour
{
    public void LoadScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("In-Game");
    }
}
