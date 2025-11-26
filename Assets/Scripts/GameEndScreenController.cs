using UnityEngine;

public class GameEndScreenController : MonoBehaviour
{
    [SerializeField] private GameObject winScreen;
    [SerializeField] private GameObject loseScreen;

    public static GameEndScreenController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        HideAll();
    }

    public void ShowWin()
    {
        if (winScreen != null) winScreen.SetActive(true);
        if (loseScreen != null) loseScreen.SetActive(false);
    }

    public void ShowLose()
    {
        if (loseScreen != null) loseScreen.SetActive(true);
        if (winScreen != null) winScreen.SetActive(false);
    }

    public void HideAll()
    {
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
    }
}
