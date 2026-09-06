using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    public class LevelFlowManager : MonoBehaviour
    {
        [SerializeField] private List<SourceNode> sources;
        [SerializeField] private List<SwitchNode> allSwitches;
        [SerializeField] private List<BlockNode> allBlocks;
        [SerializeField] private List<ChannelNode> allChannels;
        [SerializeField] private MoveCounter moveCounter;

        private const int MaxIterations = 5000;

        public System.Action OnLevelWin;
        public System.Action OnLevelLose;

        private readonly HashSet<BlockNode> _subscribedBlocks =
            new HashSet<BlockNode>();

        private bool _hasStarted;

        private void Start()
        {
            _hasStarted = true;

            SubscribeToBlocks();

            if (moveCounter != null)
                moveCounter.OnMovesExhausted += HandleMovesExhausted;

            RecalculateFlows();
        }

        public void ConfigureGeneratedGraph(
            List<SourceNode> generatedSources,
            List<SwitchNode> generatedSwitches,
            List<BlockNode> generatedBlocks,
            List<ChannelNode> generatedChannels)
        {
            UnsubscribeFromBlocks();

            sources = generatedSources ?? new List<SourceNode>();
            allSwitches = generatedSwitches ?? new List<SwitchNode>();
            allBlocks = generatedBlocks ?? new List<BlockNode>();
            allChannels = generatedChannels ?? new List<ChannelNode>();

            SubscribeToBlocks();

            if (_hasStarted)
                RecalculateFlows();
        }

        private void SubscribeToBlocks()
        {
            if (allBlocks == null)
                return;

            foreach (var block in allBlocks)
            {
                if (block == null)
                    continue;

                if (_subscribedBlocks.Add(block))
                    block.OnDestroyedByFlow += HandleBlockDestroyed;
            }
        }

        private void UnsubscribeFromBlocks()
        {
            foreach (var block in _subscribedBlocks)
            {
                if (block != null)
                    block.OnDestroyedByFlow -= HandleBlockDestroyed;
            }

            _subscribedBlocks.Clear();
        }

        public void OnPlayerToggledSwitch(SwitchNode sw)
        {
            if (sw == null || !sw.CanCycle)
                return;

            // Ходы закончились — ручное переключение запрещено.
            if (moveCounter != null && moveCounter.MovesLeft <= 0)
                return;

            // Одно ручное действие = один ход.
            if (moveCounter != null)
                moveCounter.SpendMove();

            sw.CyclePosition();

            RecalculateFlows();
        }

        public void OnPlayerSelectedSwitchPosition(
            SwitchNode sw,
            int positionIndex)
        {
            if (sw == null || !sw.CanCycle)
                return;

            // Если рычаг уже стоит в этом положении,
            // это не действие и ход не тратится.
            if (sw.CurrentPositionIndex == positionIndex)
                return;

            // Ходы закончились.
            if (moveCounter != null && moveCounter.MovesLeft <= 0)
                return;

            // Одно изменение положения = один ход.
            if (moveCounter != null)
                moveCounter.SpendMove();

            sw.SetPosition(positionIndex);

            RecalculateFlows();
        }

        public void RecalculateFlows()
        {
            // ---------------------------------------------------------
            // 1. Сбрасываем текущую краску каналов
            // ---------------------------------------------------------

            if (allChannels != null)
            {
                foreach (var channel in allChannels)
                {
                    if (channel != null)
                        channel.ResetFlow();
                }
            }

            // ---------------------------------------------------------
            // 2. Сбрасываем цвет переключателей
            // ---------------------------------------------------------

            if (allSwitches != null)
            {
                foreach (var sw in allSwitches)
                {
                    if (sw != null)
                        sw.ResetFlow();
                }
            }

            // ---------------------------------------------------------
            // 3. Создаём очередь распространения
            // ---------------------------------------------------------

            var queue = new Queue<(IFlowNode node, ColorType color)>();

            // Источники ВСЕГДА запускают поток.
            if (sources != null)
            {
                foreach (var source in sources)
                {
                    if (source == null)
                        continue;

                    queue.Enqueue(
                        (source, source.CurrentColor)
                    );
                }
            }

            // ---------------------------------------------------------
            // 4. Распространяем краску
            // ---------------------------------------------------------

            int iterations = 0;

            while (queue.Count > 0 &&
                   iterations++ < MaxIterations)
            {
                var current = queue.Dequeue();

                IFlowNode node = current.node;
                ColorType incomingColor = current.color;

                if (node == null)
                    continue;

                if (incomingColor.IsEmpty())
                    continue;

                ColorType previousColor = node.CurrentColor;

                ColorType resultColor =
                    node.ReceiveFlow(incomingColor);

                // Источник должен передавать поток всегда.
                //
                // Для остальных узлов отправляем поток дальше
                // только если их итоговый цвет изменился.
                bool shouldPropagate =
                    node is SourceNode ||
                    resultColor != previousColor;

                if (!shouldPropagate)
                    continue;

                foreach (var next in node.GetActiveOutputs())
                {
                    if (next == null)
                        continue;

                    queue.Enqueue(
                        (next, resultColor)
                    );
                }
            }

            // ---------------------------------------------------------
            // 5. Защита от случайных циклов
            // ---------------------------------------------------------

            if (iterations >= MaxIterations)
            {
                Debug.LogError(
                    "LevelFlowManager: достигнут MaxIterations. " +
                    "Возможно, в графе присутствует цикл.",
                    this
                );
            }
        }

        // -------------------------------------------------------------
        // Блок был уничтожен
        // -------------------------------------------------------------

        private void HandleBlockDestroyed()
        {
            AutoResetSwitches();

            RecalculateFlows();

            CheckWinCondition();
        }

        // -------------------------------------------------------------
        // После разрушения блока возвращаем переключатели
        // в исходное положение
        // -------------------------------------------------------------

        private void AutoResetSwitches()
        {
            if (allSwitches == null)
                return;

            foreach (var sw in allSwitches)
            {
                if (sw == null)
                    continue;

                if (sw.CurrentPositionIndex != 0)
                    sw.ResetPosition();
            }
        }

        // -------------------------------------------------------------
        // Проверка победы
        // -------------------------------------------------------------

        private void CheckWinCondition()
        {
            if (allBlocks == null)
                return;

            foreach (var block in allBlocks)
            {
                if (block == null)
                    continue;

                if (!block.IsDestroyed)
                    return;
            }

            OnLevelWin?.Invoke();
        }

        // -------------------------------------------------------------
        // Закончились ходы
        // -------------------------------------------------------------

        private void HandleMovesExhausted()
        {
            if (allBlocks == null)
                return;

            foreach (var block in allBlocks)
            {
                if (block == null)
                    continue;

                if (!block.IsDestroyed)
                {
                    OnLevelLose?.Invoke();
                    return;
                }
            }
        }
    }
}
