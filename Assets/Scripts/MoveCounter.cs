using UnityEngine;

namespace ColorMelt.Core
{
    public class MoveCounter : MonoBehaviour
    {
        [SerializeField] private int maxMoves = 10;

        public int MovesLeft { get; private set; }
        public int MaxMoves => maxMoves;

        public System.Action<int> OnMovesChanged;
        public System.Action OnMovesExhausted;

        private void Awake()
        {
            MovesLeft = maxMoves;
        }

        public void SpendMove()
        {
            if (MovesLeft <= 0)
                return;

            MovesLeft--;

            OnMovesChanged?.Invoke(MovesLeft);

            if (MovesLeft <= 0)
                OnMovesExhausted?.Invoke();
        }

        public void ResetMoves()
        {
            MovesLeft = maxMoves;
            OnMovesChanged?.Invoke(MovesLeft);
        }
    }
}