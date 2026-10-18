using RoomDemo.Input;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 可互动的场景物品基类。只需要"走近 + 按键 → 触发"。
    ///
    /// 注意：Unity 要求 MonoBehaviour 所在文件名与类名一致，
    /// 所以这个基类单独放在 RoomInteractable.cs 里
    ///（原来三个类挤在 RoomInteractables.cs，导致 MonoScript 资产
    /// 映射不到具体类，AddComponent 出来的组件序列化后会变成丢失脚本）。
    /// </summary>
    public abstract class RoomInteractable : MonoBehaviour
    {
        /// <summary>The single room interaction that will receive the next E press.</summary>
        public static MonoBehaviour CurrentTarget { get; private set; }
        static int _selectionFrame = -1;

        [Tooltip("交互距离（只看水平方向，主角走到物品所在的列附近即可）")]
        public float interactRange = 1.4f;

        [Tooltip("主角。只用到位置，不要求特定控制器。")]
        public Transform player;

        /// <summary>是否还能交互。被拿走的物品要能主动失效。</summary>
        public bool canInteract = true;

        /// <summary>
        /// 是否有任意特写（画板/收拾）正在进行中。进行中时所有房间内互动一律不响应，
        /// 避免与特写自己的输入处理抢同一个 E 事件（双重触发）。由各自特写管理器置位。
        /// </summary>
        public static bool CloseupActive { get; set; }

        /// <summary>
        /// 主角是否在交互范围内。
        ///
        /// 关键：只比较<b>水平 X 距离</b>，不比较 Y。
        /// 因为透视手绘场景里，桌面上的家具 Y 值明显高于角色地面 Y，
        /// 这是固定存在的纵向差，用二维距离判定的话无论怎么走近都够不到，
        /// E 永远没反应。
        /// </summary>
        public bool PlayerInRange
        {
            get
            {
                if (!canInteract || player == null) return false;
                return HorizontalDistance(player.position, transform.position) < interactRange;
            }
        }

        /// <summary>只算 X 方向的距离（该视角下 X 就是房间左右）。</summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x);
        }

        bool _inputSubscribed;

        void EnsureOutline()
        {
            if (GetComponent<InteractionOutline>() == null)
                gameObject.AddComponent<InteractionOutline>();
        }

        /// <summary>Choose the nearest valid room object or drawing board using the same X-range used by E.</summary>
        public static void RefreshSelectionForInput()
        {
            if (_selectionFrame == Time.frameCount) return;
            _selectionFrame = Time.frameCount;

            MonoBehaviour best = null;
            float bestDistance = float.PositiveInfinity;
            var interactables = FindObjectsOfType<RoomInteractable>();
            foreach (var item in interactables)
            {
                if (item == null || !item.isActiveAndEnabled || !item.canInteract || item.player == null) continue;
                float distance = HorizontalDistance(item.player.position, item.transform.position);
                if (distance >= item.interactRange || distance >= bestDistance) continue;
                best = item;
                bestDistance = distance;
            }

            var board = FindObjectOfType<DrawingBoardInteract>();
            if (board != null && board.isActiveAndEnabled && board.player != null)
            {
                float distance = HorizontalDistance(board.player.position, board.transform.position);
                if (distance < board.interactRange && distance < bestDistance)
                    best = board;
            }

            CurrentTarget = CloseupActive ? null : best;
            foreach (var item in interactables)
                if (item != null) SetOutline(item, item == CurrentTarget);
            if (board != null) SetOutline(board, board == CurrentTarget);
        }

        static void SetOutline(MonoBehaviour target, bool enabled)
        {
            if (target == null) return;
            var outline = target.GetComponent<InteractionOutline>();
            if (outline == null) outline = target.gameObject.AddComponent<InteractionOutline>();
            outline.SetOutlined(enabled);
        }

        /// <summary>
        /// 惰性订阅 InputManager 的 Interact 事件（策划案 1.7）。
        /// 场景加载时 InputManager 的 Awake 可能还没跑、Instance 为 null，
        /// 每帧重试——与 GameManager.Subscribe 同一套时序兜底。
        /// </summary>
        void EnsureInputSubscribed()
        {
            if (_inputSubscribed) return;
            var im = InputManager.Instance;
            if (im == null) return;
            im.Interact -= HandleInteract;
            im.Interact += HandleInteract;
            _inputSubscribed = true;
        }

        void HandleInteract()
        {
            RefreshSelectionForInput();
            if (!CloseupActive && CurrentTarget == this) Interact();
        }

        protected virtual void Update()
        {
            EnsureOutline();
            EnsureInputSubscribed();
            RefreshSelectionForInput();
        }

        void OnDestroy()
        {
            var im = InputManager.Instance;
            if (im != null) im.Interact -= HandleInteract;
            _inputSubscribed = false;
        }

        /// <summary>执行互动。</summary>
        public abstract void Interact();
    }
}
