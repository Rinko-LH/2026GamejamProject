using RoomDemo.Input;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 主角移动（占位）。输入统一走 InputManager（策划案 1.7），
    /// 本脚本不直接读 Input；对话/UI 期间由上下文栈自动锁住。
    ///
    /// 批 4 修正：Player 挂的是 Dynamic Rigidbody2D，原来用
    /// transform.position += 每帧瞬移，和物理解算互相打架——
    /// 贴到碰撞体附近会抖、卡、被弹（"行动轨迹受阻"的帮凶之一）。
    /// 改为 FixedUpdate 里 Rigidbody2D.MovePosition，交给物理引擎统一结算。
    /// </summary>
    public class Simple2DMovement : MonoBehaviour
    {
        public float moveSpeed = 5f;

        [Header("角色精灵（上 / 右 / 下）")]
        [Tooltip("向上移动时使用的角色精灵。")]
        public Sprite upSprite;
        [Tooltip("向右移动时使用的角色精灵；向左移动时水平翻转此精灵。")]
        public Sprite rightSprite;
        [Tooltip("向下移动时使用的角色精灵。停止移动后保留最后朝向。")]
        public Sprite downSprite;

        [Header("交互范围")]
        [Tooltip("玩家脚边的检测碰撞箱；留空时使用 Player 根物体上的 Collider2D")]
        public Collider2D interactionProbe;

        Rigidbody2D _rb;
        SpriteRenderer _spriteRenderer;
        public Vector2 FacingDirection { get; private set; } = Vector2.down;

        void Awake()
        {
            EnsureRb();
        }

        void EnsureRb()
        {
            if (_rb != null) return;
            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody2D>();
                _rb.gravityScale = 0f;
            }
            _rb.freezeRotation = true;
            EnsureSpriteRenderer();
            SetFacing(Vector2.down);
        }

        void EnsureSpriteRenderer()
        {
            if (_spriteRenderer != null) return;
            var body = transform.Find("Body");
            _spriteRenderer = body != null ? body.GetComponent<SpriteRenderer>() : null;
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        void SetFacing(Vector2 direction)
        {
            EnsureSpriteRenderer();
            if (_spriteRenderer == null) return;

            Sprite facingSprite;
            bool flipX = false;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                FacingDirection = direction.x < 0f ? Vector2.left : Vector2.right;
                facingSprite = rightSprite;
                flipX = direction.x < 0f;
            }
            else
            {
                FacingDirection = direction.y < 0f ? Vector2.down : Vector2.up;
                facingSprite = direction.y < 0f ? downSprite : upSprite;
            }

            if (facingSprite != null) _spriteRenderer.sprite = facingSprite;
            _spriteRenderer.flipX = flipX;
        }

        void FixedUpdate()
        {
            // 惰性兜底：播放中域重载后 Awake 不保证重跑，_rb 可能为 null
            EnsureRb();
            var move = InputManager.Instance != null ? InputManager.Instance.Move : Vector2.zero;
            var dir = new Vector3(move.x, move.y, 0f).normalized;
            if (move.sqrMagnitude > 0.0001f) SetFacing(move);
            // S8 低落表现：状态低落（任一 <20%）时移速 -20%，经契约层自查
            float speed = moveSpeed;
            var stats = RoomDemo.Contracts.Services.Stats;
            if (stats != null && stats.IsLowMood) speed *= 0.8f;
            _rb.MovePosition(_rb.position + (Vector2)(dir * speed * Time.fixedDeltaTime));
        }
    }
}
