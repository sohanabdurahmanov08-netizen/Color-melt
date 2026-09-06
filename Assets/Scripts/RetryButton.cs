using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorMelt.Core
{
    public class RetryButton : MonoBehaviour
    {
        public void Retry()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.buildIndex);
        }
    }
}