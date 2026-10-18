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
        const float ShadowSelectionRadius = 1.5f;
        /// <summary>The single room interaction that will receive the next E press.</summary>
        public static MonoBehaviour CurrentTarget { get; private set; }
        static int _selectionFrame = -1;

        [Tooltip("玩家与物品边界之间允许的最大交互距离")]
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
        /// 主角是否足够接近物品。距离按物体碰撞体或可见图像边缘计算，
        /// 避免用家具中心点导致大型物品不易触发。
        /// </summary>
        public bool PlayerInRange
        {
            get
            {
                if (!canInteract || player == null) return false;
                return InteractionDistance(player, transform) < interactRange;
            }
        }

        /// <summary>计算玩家碰撞体到目标碰撞体/可见边界的二维间距。</summary>
        public static float InteractionDistance(Transform player, Transform target)
        {
            if (player == null || target == null) return float.PositiveInfinity;

            var playerCollider = player.GetComponent<Collider2D>() ?? player.GetComponentInChildren<Collider2D>();
            var targetCollider = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
            Vector2 playerPosition = player.position;
            Vector2 targetPoint;

            if (targetCollider != null)
            {
                targetPoint = targetCollider.ClosestPoint(playerPosition);
            }
            else
            {
                var renderers = target.GetComponentsInChildren<SpriteRenderer>();
                bool hasBounds = false;
                Bounds bounds = default;
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else bounds.Encapsulate(renderer.bounds);
                }

                if (!hasBounds) targetPoint = target.position;
                else
                {
                    targetPoint = bounds.ClosestPoint(playerPosition);
                }
            }

            Vector2 playerPoint = playerCollider != null
                ? playerCollider.ClosestPoint(targetPoint)
                : playerPosition;
            return Vector2.Distance(playerPoint, targetPoint);
        }

        bool _inputSubscribed;

        void EnsureOutline()
        {
            if (GetComponent<InteractionOutline>() == null)
                gameObject.AddComponent<InteractionOutline>();
        }

        /// <summary>选择玩家当前朝向前方、且在交互范围内最近的物品或画板。</summary>
        public static void RefreshSelectionForInput()
        {
            if (_selectionFrame == Time.frameCount) return;
            _selectionFrame = Time.frameCount;

            MonoBehaviour best = null;
            float bestDistance = float.PositiveInfinity;
            var interactables = FindObjectsOfType<RoomInteractable>();
            var shadow = GameObject.Find("Shadow");
            Vector2 shadowPosition = shadow != null ? (Vector2)shadow.transform.position : Vector2.zero;
            foreach (var item in interactables)
            {
                if (item == null || !item.isActiveAndEnabled || !item.canInteract || item.player == null) continue;
                if (shadow == null || !IsWithinShadowRange(shadowPosition, item.transform)) continue;
                float distance = InteractionDistance(item.player, item.transform);
                if (distance >= item.interactRange || distance >= bestDistance || !IsInFront(item.player, item.transform)) continue;
                best = item;
                bestDistance = distance;
            }

            var board = FindObjectOfType<DrawingBoardInteract>();
            if (shadow != null && board != null && board.isActiveAndEnabled && board.player != null
                && IsWithinShadowRange(shadowPosition, board.transform))
            {
                float distance = InteractionDistance(board.player, board.transform);
                if (distance < board.interactRange && distance < bestDistance && IsInFront(board.player, board.transform))
                    best = board;
            }

            CurrentTarget = CloseupActive ? null : best;
            foreach (var item in interactables)
                if (item != null) SetOutline(item, item == CurrentTarget);
            if (board != null) SetOutline(board, board == CurrentTarget);
        }

        static bool IsWithinShadowRange(Vector2 shadowPosition, Transform target)
        {
            return target != null
                && ((Vector2)target.position - shadowPosition).sqrMagnitude <= ShadowSelectionRadius * ShadowSelectionRadius;
        }

        static bool IsInFront(Transform player, Transform target)
        {
            var movement = player != null ? player.GetComponent<Simple2DMovement>() : null;
            if (movement == null || target == null) return true;

            // Test facing against the nearest point on the target, not its pivot.
            // Large furniture can have a pivot far from the side the player is standing at,
            // which otherwise makes a nearby object appear behind a farther target.
            Vector2 playerPosition = player.position;
            var collider = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
            Vector2 targetPoint;
            if (collider != null)
            {
                targetPoint = collider.ClosestPoint(playerPosition);
            }
            else
            {
                var renderers = target.GetComponentsInChildren<SpriteRenderer>();
                bool hasBounds = false;
                Bounds bounds = default;
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                targetPoint = hasBounds ? bounds.ClosestPoint(playerPosition) : (Vector2)target.position;
            }

            Vector2 toTarget = targetPoint - playerPosition;
            // Allow a modest forward cone while rejecting targets that are merely
            // somewhere in the front half-plane.
            return toTarget.sqrMagnitude <= 0.0001f
                || Vector2.Dot(movement.FacingDirection, toTarget.normalized) > 0.35f;
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
