using System.Collections.Generic;
using UnityEngine;

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
        [SerializeField] private List<ChannelNode> allChannels;
        [SerializeField] private MoveCounter moveCounter;

        // Защита от случайных циклов в графе уровня (например, ошибка левел-дизайнера)
        private const int MaxIterations = 5000;

        public System.Action OnLevelWin;
        public System.Action OnLevelLose;

        private readonly HashSet<BlockNode> _subscribedBlocks = new HashSet<BlockNode>();
        private bool _hasStarted;

        private void Start()
        {
            _hasStarted = true;
            SubscribeToBlocks();

            if (moveCounter != null)
                moveCounter.OnMovesExhausted += HandleMovesExhausted;

            Simulate();
        }

        /// <summary>
        /// Replaces the graph with nodes made by a procedural level generator.
        /// The method is also safe before Start, which is how Level 1 prepares
        /// its generated graph in Awake.
        /// </summary>
        public void ConfigureGeneratedGraph(List<SourceNode> generatedSources,
            List<SwitchNode> generatedSwitches, List<BlockNode> generatedBlocks,
            List<ChannelNode> generatedChannels)
        {
            UnsubscribeFromBlocks();
            sources = generatedSources ?? new List<SourceNode>();
            allSwitches = generatedSwitches ?? new List<SwitchNode>();
            allBlocks = generatedBlocks ?? new List<BlockNode>();
            allChannels = generatedChannels ?? new List<ChannelNode>();
            SubscribeToBlocks();

            if (_hasStarted)
                Simulate();
        }

        private void SubscribeToBlocks()
        {
            if (allBlocks == null) return;

            foreach (var block in allBlocks)
            {
                if (block == null || !_subscribedBlocks.Add(block)) continue;
                block.OnDestroyedByFlow += HandleBlockDestroyed;
            }
        }

        private void UnsubscribeFromBlocks()
        {
            foreach (var block in _subscribedBlocks)
                if (block != null)
                    block.OnDestroyedByFlow -= HandleBlockDestroyed;

            _subscribedBlocks.Clear();
        }

        /// <summary>Вызывайте это из обработчика клика/тапа по рычагу.</summary>
        public void OnPlayerToggledSwitch(SwitchNode sw)
        {
            if (sw == null || !sw.CanCycle) return;
            sw.CyclePosition();
            moveCounter?.SpendMove();
            Simulate();
        }

        /// <summary>Полный пересчёт всех потоков от источников по графу.</summary>
        private void Simulate()
        {
            // Each click is a new state of the puzzle. Clear channels first so
            // colour is mixed only from flows valid for the current lever setup.
            // Блоки не трогаем — разрушенные должны оставаться разрушенными.
            if (allChannels != null)
                foreach (var channel in allChannels)
                    if (channel != null)
                        channel.ResetFlow();

            if (allSwitches != null)
                foreach (var sw in allSwitches) sw.ResetFlow();

            var queue = new Queue<(IFlowNode node, ColorType color)>();
            if (sources != null)
                foreach (var source in sources)
                    if (source != null)
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
            if (allSwitches != null)
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

