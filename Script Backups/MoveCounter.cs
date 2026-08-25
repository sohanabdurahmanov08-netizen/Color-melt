using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Считает только ручные действия игрока (переключения рычагов).
    /// Автосброс рычагов после разрушения блока ходов не тратит —
    /// для этого просто не вызывайте SpendMove() в LevelFlowManager.AutoResetSwitches().
    /// </summary>
    public class MoveCounter : MonoBehaviour
    {
        [SerializeField] private int maxMoves = 10;

        public int MovesLeft { get; private set; }
        public int MaxMoves => maxMoves;

        public System.Action<int> OnMovesChanged;
        public System.Action OnMovesExhausted;

        private void Awake() => MovesLeft = maxMoves;

        /// <summary>Вызывать при каждом ручном переключении рычага игроком.</summary>
        public void SpendMove()
        {
            if (MovesLeft <= 0) return;

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
