using UnityEngine;

namespace RoomDemo
{
    /// <summary>四个朝向，对应画面里的上/下/左/右。</summary>
    public enum Facing4 { Up, Down, Left, Right }

    /// <summary>
    /// 主角：四方向移动 + 朝向表现 + 走路时轻微上下起伏。
    ///
    /// 这个视角下屏幕方向就是世界坐标（X 向右、Y 向上），
    /// 所以输入不需要任何斜向转换——这是该视角相比等轴测最大的好处。
    /// </summary>
    public class RoomPlayer : MonoBehaviour
    {
        [Header("移动")]
        public float moveSpeed = 4.5f;

        [Header("走路动画")]
        // 起伏要相对身高（约 2.5 单位）足够小，否则会看起来在"飘"
        public float bobHeight = 0.035f;
        public float bobFrequency = 9f;

        public Facing4 Facing { get; private set; } = Facing4.Down;
        public bool IsMoving { get; private set; }

        Transform _body;
        SpriteRenderer _torsoSr, _headSr, _faceSr, _legSr, _hairSr;
        SpriteRenderer _shadowSr;
        float _bobPhase;

        void Awake()
        {
            Cache();
        }

        void Cache()
        {
            _body = transform.Find("Body");
            if (_body != null)
            {
                _torsoSr = _body.Find("Torso")?.GetComponent<SpriteRenderer>();
                _headSr = _body.Find("Head")?.GetComponent<SpriteRenderer>();
                _faceSr = _body.Find("Face")?.GetComponent<SpriteRenderer>();
                _legSr = _body.Find("Legs")?.GetComponent<SpriteRenderer>();
                _hairSr = _body.Find("Hair")?.GetComponent<SpriteRenderer>();
            }
            _shadowSr = transform.Find("Shadow")?.GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            Vector2 dir = ReadDirection();
            IsMoving = dir != Vector2.zero;

            if (IsMoving)
            {
                Facing = ToFacing(dir);
                transform.position += new Vector3(dir.x, dir.y, 0f) * (moveSpeed * Time.deltaTime);
                _bobPhase += Time.deltaTime * bobFrequency;
            }

            ApplyFacing();
            ApplyBob();
            ClampToRoom();
            SyncOrder();
        }

        /// <summary>读取输入，吸附成上下左右四个方向。</summary>
        Vector2 ReadDirection()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) < 0.15f && Mathf.Abs(v) < 0.15f) return Vector2.zero;

            // 这个视角下屏幕方向 = 世界方向，取较大的那个分量即可
            if (Mathf.Abs(h) >= Mathf.Abs(v))
                return new Vector2(Mathf.Sign(h), 0f);
            return new Vector2(0f, Mathf.Sign(v));
        }

        static Facing4 ToFacing(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                return dir.x > 0f ? Facing4.Right : Facing4.Left;
            return dir.y > 0f ? Facing4.Up : Facing4.Down;
        }

        /// <summary>
        /// 朝向表现：脸的位置 + 是否显示。
        /// 纯色块也能一眼看出面朝哪边。
        /// </summary>
        void ApplyFacing()
        {
            if (_faceSr == null) return;

            // 这些偏移作用在 Body 的子节点上，必须和 RoomSceneBuilder 里摆的头/脸高度一致
            const float headY = 2.10f;
            Vector3 facePos = new Vector3(0f, headY - 0.06f, 0f);

            switch (Facing)
            {
                case Facing4.Right: facePos.x = 0.17f; break;
                case Facing4.Left:  facePos.x = -0.17f; break;
                case Facing4.Down:  break;                           // 面向镜头
                case Facing4.Up:    facePos.y = headY + 0.20f; break; // 背对镜头
            }

            _faceSr.transform.localPosition = facePos;
            // 背对镜头时把脸换成后脑勺
            _faceSr.gameObject.SetActive(Facing != Facing4.Up);

            // 左右朝向时头略微侧倾，强化方向感
            if (_headSr != null)
            {
                float tilt = Facing == Facing4.Left ? 4f : (Facing == Facing4.Right ? -4f : 0f);
                _headSr.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            }

            // 走路时腿前后摆动
            if (_legSr != null && IsMoving)
            {
                float swing = Mathf.Sin(_bobPhase * 2f) * 8f;
                _legSr.transform.localRotation = Quaternion.Euler(0f, 0f, swing);
            }
            else if (_legSr != null)
            {
                _legSr.transform.localRotation = Quaternion.identity;
            }
        }

        void ApplyBob()
        {
            if (_body == null) return;
            float bob = IsMoving ? Mathf.Sin(_bobPhase) * bobHeight : 0f;
            _body.localPosition = new Vector3(0f, bob, 0f);
        }

        void SyncOrder()
        {
            // transform.position.y 是"缩放后的屏幕高度"，不是纵深。
            // 必须除以当前缩放还原出 depth，才能和家具用同一套排序基准。
            float s = transform.lossyScale.y;
            float depth = s > 0.0001f ? transform.position.y / s : transform.position.y;

            // 按纵深排层：越靠里越先画，角色走到哪都能被正确遮挡
            int order = RoomView.Order(depth, 0);
            if (_shadowSr != null) _shadowSr.sortingOrder = order;
            if (_legSr != null) _legSr.sortingOrder = order + 1;
            if (_torsoSr != null) _torsoSr.sortingOrder = order + 2;
            if (_headSr != null) _headSr.sortingOrder = order + 3;
            // 头发要盖在头顶上，但脸的层次更高
            if (_hairSr != null) _hairSr.sortingOrder = order + 4;
            if (_faceSr != null) _faceSr.sortingOrder = order + 5;
        }

        /// <summary>限制在房间范围内，避免走出场景。</summary>
        void ClampToRoom()
        {
            float s = transform.lossyScale.y;
            if (s <= 0.0001f) return;

            // 位置是在父空间下的"缩放后屏幕高度"，先还原成纵深再夹取
            Vector3 p = transform.position;
            float depth = p.y / s;

            float halfW = RoomView.HalfWidth - 0.5f;
            // 上界要减去角色身高，避免走到后墙时身体穿进墙里
            float charTop = 2.6f * s;
            depth = Mathf.Clamp(depth, RoomView.FrontY + 0.15f,
                                RoomView.FloorY(RoomView.RoomDepth) - charTop * 0.35f);

            transform.position = new Vector3(
                Mathf.Clamp(p.x, -halfW * s, halfW * s),
                depth * s,
                0f);
        }

        /// <summary>把角色放到指定纵深，x 取房间中心。</summary>
        public void PlaceAt(float depth)
        {
            transform.position = new Vector3(0f, depth, 0f);
        }
    }
}