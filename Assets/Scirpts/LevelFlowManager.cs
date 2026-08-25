using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEngine;
using static Unity.VisualScripting.Dependencies.Sqlite.SQLite3;

namespace ColorMelt.Core
{
    /// <summary>
    /// Центральный узел логики уровня:
    /// - пересчитывает потоки по графу при любом изменении рычага (BFS от источников);
    /// - следит за разрушением блоков;
    /// - выполняет автосброс рычагов в исходное положение без траты хода;
    /// - проверяет победу/поражение через MoveCounter.
    ///
    /// Подключите сюда все узлы уровня через инспектор — это единственный
    /// компонент, который знает про структуру уровня целиком.
    /// </summary>
    public class LevelFlowManager : MonoBehaviour
    {
        [SerializeField] private List<SourceNode> sources;
        [SerializeField] private List<SwitchNode> allSwitches;
        [SerializeField] private List<BlockNode> allBlocks;
        [SerializeField] private MoveCounter moveCounter;

        // Защита от случайных циклов в графе уровня (например, ошибка левел-дизайнера)
        private const int MaxIterations = 5000;

        public System.Action OnLevelWin;
        public System.Action OnLevelLose;

        private void Start()
        {
            foreach (var block in allBlocks)
                block.OnDestroyedByFlow += HandleBlockDestroyed;

            if (moveCounter != null)
                moveCounter.OnMovesExhausted += HandleMovesExhausted;

            Simulate();
        }

        /// <summary>Вызывайте это из обработчика клика/тапа по рычагу.</summary>
        public void OnPlayerToggledSwitch(SwitchNode sw)
        {
            sw.CyclePosition();
            moveCounter?.SpendMove();
            Simulate();
        }

        /// <summary>Полный пересчёт всех потоков от источников по графу.</summary>
        private void Simulate()
        {
            // Очищаем текущее состояние каналов/рычагов перед пересчётом.
            // Блоки не трогаем — разрушенные должны оставаться разрушенными.
            foreach (var sw in allSwitches) sw.ResetFlow();

            var queue = new Queue<(IFlowNode node, ColorType color)>();
            foreach (var source in sources)
                queue.Enqueue((source, source.CurrentColor));

            int iterations = 0;
            while (queue.Count > 0 && iterations++ < MaxIterations)
            {
                var (node, incomingColor) = queue.Dequeue();
                var resultColor = node.ReceiveFlow(incomingColor);

                foreach (var next in node.GetActiveOutputs())
                    queue.Enqueue((next, resultColor));
            }
        }

        private void HandleBlockDestroyed()
        {
            AutoResetSwitches(); // ход не тратится — SpendMove() здесь намеренно не вызывается
            Simulate();
            CheckWinCondition();
        }

        /// <summary>Автосброс всех рычагов в исходное положение после разрушения блока.</summary>
        private void AutoResetSwitches()
        {
            foreach (var sw in allSwitches)
                sw.ResetPosition();
        }

        private void CheckWinCondition()
        {
            foreach (var block in allBlocks)
                if (!block.IsDestroyed)
                    return;

            OnLevelWin?.Invoke();
        }

        private void HandleMovesExhausted()
        {
            foreach (var block in allBlocks)
                if (!block.IsDestroyed)
                {
                    OnLevelLose?.Invoke();
                    return;
                }
        }
    }
}

