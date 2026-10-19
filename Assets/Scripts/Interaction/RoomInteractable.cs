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

        [Tooltip("已保留用于旧场景兼容；交互范围由物体上的 Collider2D 决定")]
        public float interactRange = 1.4f;

        [Tooltip("主角。只用到位置，不要求特定控制器。")]
        public Transform player;

        [Header("交互范围")]
        [Tooltip("独立的 Is Trigger 碰撞箱；留空时从自身及子物体中查找第一个 Trigger")]
        public Collider2D interactionZone;

        /// <summary>是否还能交互。被拿走的物品要能主动失效。</summary>
        public bool canInteract = true;

        /// <summary>是否应显示交互描边。默认仅高亮当前输入选中的目标。</summary>
        protected virtual bool ShouldShowOutline => this == CurrentTarget;

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
                return IsPlayerOverlappingTarget(player, transform);
            }
        }

        /// <summary>玩家任一启用的 2D 碰撞体与目标任一启用碰撞体重叠时，视为进入交互范围。</summary>
        public static bool IsPlayerOverlappingTarget(Transform player, Transform target)
        {
            if (player == null || target == null) return false;
            var movement = player.GetComponent<Simple2DMovement>();
            var playerCollider = movement != null && movement.interactionProbe != null
                ? movement.interactionProbe
                : player.GetComponent<Collider2D>();
            var targetCollider = GetInteractionZone(target);
            if (playerCollider == null || targetCollider == null || !targetCollider.isTrigger) return false;
            if (!playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy
                || !targetCollider.enabled || !targetCollider.gameObject.activeInHierarchy) return false;
            return Physics2D.Distance(playerCollider, targetCollider).isOverlapped;
        }

        static Collider2D GetInteractionZone(Transform target)
        {
            if (target == null) return null;
            var roomTarget = target.GetComponent<RoomInteractable>();
            if (roomTarget != null && roomTarget.interactionZone != null) return roomTarget.interactionZone;
            var boardTarget = target.GetComponent<DrawingBoardInteract>();
            if (boardTarget != null && boardTarget.interactionZone != null) return boardTarget.interactionZone;
            foreach (var collider in target.GetComponentsInChildren<Collider2D>())
                if (collider != null && collider.isTrigger) return collider;
            return null;
        }

        static Collider2D GetPlayerCollider(Transform player)
        {
            if (player == null) return null;
            var movement = player.GetComponent<Simple2DMovement>();
            if (movement != null && movement.interactionProbe != null) return movement.interactionProbe;
            return player.GetComponent<Collider2D>();
        }

        /// <summary>计算玩家碰撞体到目标碰撞体/可见边界的二维间距。</summary>
        public static float InteractionDistance(Transform player, Transform target)
        {
            if (player == null || target == null) return float.PositiveInfinity;

            var playerCollider = GetPlayerCollider(player);
            var targetCollider = GetInteractionZone(target);
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
            float bestFacing = float.NegativeInfinity;
            var interactables = FindObjectsOfType<RoomInteractable>();
            foreach (var item in interactables)
            {
                if (item == null || !item.isActiveAndEnabled || !item.canInteract || item.player == null) continue;
                if (!IsPlayerOverlappingTarget(item.player, item.transform)) continue;
                float distance = InteractionDistance(item.player, item.transform);
                float facing = FacingAlignment(item.player, item.transform);
                if (facing <= 0.35f || facing < bestFacing
                    || (Mathf.Approximately(facing, bestFacing) && distance >= bestDistance)) continue;
                best = item;
                bestDistance = distance;
                bestFacing = facing;
            }

            var board = FindObjectOfType<DrawingBoardInteract>();
            if (board != null && board.isActiveAndEnabled && board.player != null
                && IsPlayerOverlappingTarget(board.player, board.transform))
            {
                float distance = InteractionDistance(board.player, board.transform);
                float facing = FacingAlignment(board.player, board.transform);
                if (facing > 0.35f && (facing > bestFacing
                    || (Mathf.Approximately(facing, bestFacing) && distance < bestDistance)))
                {
                    best = board;
                    bestDistance = distance;
                    bestFacing = facing;
                }
            }

            CurrentTarget = CloseupActive ? null : best;
            foreach (var item in interactables)
                if (item != null) SetOutline(item, item.ShouldShowOutline);
            if (board != null) SetOutline(board, board == CurrentTarget);
        }

        static float FacingAlignment(Transform player, Transform target)
        {
            var movement = player != null ? player.GetComponent<Simple2DMovement>() : null;
            if (target == null) return float.NegativeInfinity;
            if (movement == null) return 1f;

            // Use the interaction box center. Its closest point is the player itself
            // while overlapping, which would incorrectly make every box count as facing.
            var collider = GetInteractionZone(target);
            Vector2 targetPoint = collider != null ? (Vector2)collider.bounds.center : (Vector2)target.position;
            Vector2 toTarget = targetPoint - (Vector2)player.position;
            return toTarget.sqrMagnitude <= 0.0001f ? 1f
                : Vector2.Dot(movement.FacingDirection, toTarget.normalized);
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
