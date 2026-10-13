using UnityEngine;

namespace Iso3DBillboard
{
    /// <summary>
    /// billboard 版角色控制器。
    ///
    /// 与 3D 版（Iso3DPlayer）的关键差异：
    ///   × 不再绕 Y 轴旋转身体 —— 立绘由 YBillboard 保持朝向相机，
    ///     所以"角色朝哪"由美术提供的朝向帧决定，而不是模型旋转。
    ///   √ 只切换朝向贴图，并略微左右偏移脸来表达方向。
    ///
    /// 其余部分与 3D 版一致：CharacterController 自动碰撞，
    /// 不需要手写范围夹取，也没有任何 sortingOrder。
    /// </summary>
    public class BillboardPlayer : MonoBehaviour
    {
        public float moveSpeed = 4.5f;

        public enum Facing4 { Up, Down, Left, Right }
        public Facing4 Facing { get; private set; } = Facing4.Down;

        CharacterController _cc;
        SpriteRenderer _faceSr;
        Transform _face;
        Vector3 _faceRest;
        SpriteRenderer _artSr;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            var art = transform.Find("Art");
            if (art != null) _artSr = art.GetComponent<SpriteRenderer>();
            _face = transform.Find("Face");
            if (_face != null)
            {
                _faceSr = _face.GetComponent<SpriteRenderer>();
                _faceRest = _face.localPosition;
            }
        }

        void Update()
        {
            Vector2 dir = ReadDirection();

            // 地面是平的，把 Y 归零防止浮点漂移。
            // 注意：这里必须用 cc.Move 而不是直接赋值 transform.position，
            // 否则会绕过碰撞检测导致穿模。
            var p = transform.position;
            if (p.y != 0f) _cc.Move(new Vector3(0f, -p.y, 0f));

            // 碰撞交给 CharacterController，不做任何手写判断
            Vector3 worldDir = new Vector3(dir.x, 0f, dir.y);
            _cc.Move(worldDir * (moveSpeed * Time.deltaTime));

            if (dir != Vector2.zero) Facing = ToFacing(dir);

            ApplyFacing();
            ClampToRoom();
        }

        Vector2 ReadDirection()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) < 0.15f && Mathf.Abs(v) < 0.15f) return Vector2.zero;

            if (Mathf.Abs(h) >= Mathf.Abs(v)) return new Vector2(Mathf.Sign(h), 0f);
            return new Vector2(0f, Mathf.Sign(v));
        }

        static Facing4 ToFacing(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                return dir.x > 0f ? Facing4.Right : Facing4.Left;
            return dir.y > 0f ? Facing4.Up : Facing4.Down;
        }

        /// <summary>
        /// 朝向表现。
        ///
        /// 正式项目里这里会切换美术提供的 4 向帧图（Down/Left/Right/Up）。
        /// 现在只有一张正面图，所以用"脸的位置 + 是否显示"来示意方向：
        /// 左右时脸横移，背对时藏起来 —— 效果和 2D 版一致。
        /// </summary>
        void ApplyFacing()
        {
            if (_faceSr == null) return;

            _face.localPosition = _faceRest;

            switch (Facing)
            {
                case Facing4.Right:
                    _face.localPosition += new Vector3(0.11f, 0f, -0.02f);
                    break;
                case Facing4.Left:
                    _face.localPosition += new Vector3(-0.11f, 0f, -0.02f);
                    break;
                case Facing4.Down:
                    _face.localPosition += new Vector3(0f, -0.03f, -0.02f);
                    break;
                case Facing4.Up:
                    // 背对镜头：脸藏起来
                    _faceSr.gameObject.SetActive(false);
                    // 将来这里会换成美术的"背面"帧图
                    return;
            }
            _faceSr.gameObject.SetActive(true);
        }

        /// <summary>
        /// 房间边界限制。
        ///
        /// 关键：必须用 cc.Move 实现，不能直接赋值 transform.position。
        /// 直接赋值会把物体"传送"过去，完全绕过 CharacterController 的
        /// 碰撞检测 —— 实测会直接穿进家具内部。
        ///
        /// 更好的做法是给四面墙挂上 BoxCollider，让物理引擎接管，
        /// 这段就可以整个删掉。
        /// </summary>
        void ClampToRoom()
        {
            float h = BillboardSceneBuilder.RoomSize * 0.5f - 0.5f;
            Vector3 p = transform.position;

            float clampedX = Mathf.Clamp(p.x, -h, h);
            float clampedZ = Mathf.Clamp(p.z, -h, h);

            // 用 Move 而非赋值，保证碰撞检测仍然生效
            _cc.Move(new Vector3(clampedX - p.x, 0f, clampedZ - p.z));
        }
    }
}