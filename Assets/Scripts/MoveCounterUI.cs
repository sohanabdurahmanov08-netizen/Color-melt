using TMPro;
using UnityEngine;

namespace ColorMelt.Core
{
    public class MoveCounterUI : MonoBehaviour
    {
        [SerializeField] private MoveCounter moveCounter;
        [SerializeField] private TMP_Text movesText;

        [Header("Text")]
        [SerializeField] private string prefix = "MOVES: ";

        private void OnEnable()
        {
            if (moveCounter != null)
                moveCounter.OnMovesChanged += UpdateText;
        }

        private void Start()
        {
            if (moveCounter != null)
                UpdateText(moveCounter.MovesLeft);
        }

        private void OnDisable()
        {
            if (moveCounter != null)
                moveCounter.OnMovesChanged -= UpdateText;
        }

        private void UpdateText(int movesLeft)
        {
            if (movesText == null)
                return;

            movesText.text = prefix + movesLeft;
        }
    }
}