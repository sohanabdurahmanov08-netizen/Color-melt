using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorMelt.Core
{
    public class NextLevelButton : MonoBehaviour
    {
        public void LoadNextLevel()
        {
            int currentIndex = SceneManager.GetActiveScene().buildIndex;
            int nextIndex = currentIndex + 1;

            if (nextIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.Log("Это последний уровень!");
                return;
            }

            SceneManager.LoadScene(nextIndex);
        }
    }
}