using UnityEngine;
using UnityEngine.SceneManagement;

// OPTIONAL: Attach to an object in EndPanel of Crypt_Intro, then wire a button's OnClick.
public class MourneveilParkSceneLink : MonoBehaviour
{
    [SerializeField] private string parkSceneName = "Park";

    public void OpenPark()
    {
        if (!Application.CanStreamedLevelBeLoaded(parkSceneName))
        {
            Debug.LogError("[Mourneveil] Park scene missing from Build Profiles > Scene List: " + parkSceneName);
            return;
        }
        SceneManager.LoadScene(parkSceneName);
    }
}
