using UnityEngine;

namespace Iso3DTest
{
    /// <summary>
    /// 3D 版角色控制器，用于和 2D 版做对比。
    ///
    /// 与 2D 方案（RoomPlayer.cs）对照，本文件里：
    ///   × 没有 sortingOrder        —— 深度缓冲自动处理遮挡
    ///   × 没有 lossyScale 换算    —— position.z 就是纵深
    ///   × 没有手写范围夹取        —— CharacterController 自动与家具碰撞
    ///   × 没有魔数（charTop 之类）—— 碰撞体是真实尺寸
    ///
    /// 只有一件事和 2D 版一样：四方向移动。
    /// </summary>
    public class Iso3DPlayer : MonoBehaviour
    {
        public float moveSpeed = 4.5f;
        public float rotateSpeed = 720f;

        public enum Facing4 { Up, Down, Left, Right }

        public Facing4 Facing { get; private set; } = Facing4.Down;

        CharacterController _cc;
        Transform _face;
        Vector3 _faceRestLocal;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            var f = transform.Find("Face");
            if (f != null)
            {
                _face = f;
                _faceRestLocal = f.localPosition;
            }
        }

        void Update()
        {
            Vector2 dir = ReadDirection();

            // 地面是平的，把 Y 归零防止浮点漂移。
            // 必须走 cc.Move，直接赋值会绕过碰撞检测导致穿模。
            var p = transform.position;
            if (p.y != 0f) _cc.Move(new Vector3(0f, -p.y, 0f));

            // CharacterController.Move 自动处理与家具 BoxCollider 的碰撞，
            // 不需要任何"是否越界/是否撞到家具"的手写判断。
            Vector3 worldDir = new Vector3(dir.x, 0f, dir.y);
            _cc.Move(worldDir * (moveSpeed * Time.deltaTime));

            if (dir != Vector2.zero)
            {
                Facing = ToFacing(dir);
                // 朝向：绕 Y 轴平滑转向，3D 里这是免费的
                Quaternion target = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, rotateSpeed * Time.deltaTime);
            }

            ApplyFacing();
            ClampToRoom();
        }

        Vector2 ReadDirection()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) < 0.15f && Mathf.Abs(v) < 0.15f) return Vector2.zero;

            // 这里可以直接把输入映射到 XZ 平面，不需要任何斜向数学
            if (Mathf.Abs(h) >= Mathf.Abs(v)) return new Vector2(Mathf.Sign(h), 0f);
            return new Vector2(0f, Mathf.Sign(v));
        }

        static Facing4 ToFacing(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                return dir.x > 0f ? Facing4.Right : Facing4.Left;
            return dir.y > 0f ? Facing4.Up : Facing4.Down;
        }

        /// <summary>用"脸"色块的位置表达朝向，纯色块也能看出来。</summary>
        void ApplyFacing()
        {
            if (_face == null) return;

            // 脸始终贴在角色朝向的那一面
            _face.localPosition = _faceRestLocal;

            // 背对镜头时把脸藏起来（用旋转后脸的位置判断朝向）
            Vector3 fwd = transform.forward;
            bool facingAway = Vector3.Dot(fwd, new Vector3(0f, 0f, -1f)) > 0.6f;
            _face.gameObject.SetActive(!facingAway);
        }

        /// <summary>
        /// 房间边界限制。必须用 cc.Move，直接赋值会绕过碰撞检测。
        /// 给墙挂上 BoxCollider 后，这段也能删掉交给物理引擎。
        /// </summary>
        void ClampToRoom()
        {
            float h = Iso3DTestBuilder.RoomSize * 0.5f - 0.5f;
            Vector3 p = transform.position;
            _cc.Move(new Vector3(
                Mathf.Clamp(p.x, -h, h) - p.x, 0f,
                Mathf.Clamp(p.z, -h, h) - p.z));
        }
    }
}