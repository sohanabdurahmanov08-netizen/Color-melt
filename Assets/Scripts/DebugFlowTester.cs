using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Временный отладочный скрипт для проверки MVP-цепочки источник → канал → блок.
    /// Повесь на любой объект на сцене, перетащи в поле Block ссылку на BlockNode.
    /// Удали этот файл, когда подключишь нормальный визуал разрушения блока.
    /// </summary>
    public class DebugFlowTester : MonoBehaviour
    {
        [SerializeField] private BlockNode block;

        private ColorType _lastLoggedColor = ColorType.None;

        private void Start()
        {
            if (block == null)
            {
                // This component is harmless on a reusable route prefab where
                // a block is optional. Avoid logging a warning for every clone.
                return;
            }

            block.OnDestroyedByFlow += () => Debug.Log($"[DebugFlowTester] Блок '{block.name}' разрушен!");
        }

        private void Update()
        {
            if (block == null || block.CurrentColor == _lastLoggedColor) return;

            _lastLoggedColor = block.CurrentColor;
            Debug.Log($"[DebugFlowTester] Цвет в блоке изменился: {block.CurrentColor}");
        }
    }
}
