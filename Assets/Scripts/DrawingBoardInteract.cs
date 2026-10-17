using RoomDemo.Contracts;
using RoomDemo.Input;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 画板玩法的交互控制：把"在房间里点画板"和"在特写里画画"串起来。
    ///
    /// 按用户确认的设计：
    /// - 进入画板是<b>桌面特写俯视</b>视角（不是第一人称平视）；
    /// - <b>需要先拿到画笔</b>才能画；
    /// - 特写里<b>三块画板并排</b>，每块画一幅，各画各的；
    /// - 完成判定是<b>计时</b>：每块画板落笔起 60 秒，画满即达标（数值重规划案）；
    /// - 达标<b>只是标记</b>（外框变绿），不锁画、不强制切换，
    ///   玩家可以继续改，也可以用鼠标直接换到另一块画板上画。
    /// </summary>
    public class DrawingBoardInteract : MonoBehaviour, IResettable
    {
        [Header("引用")]
        [Tooltip("主角。只用到它的位置和显隐，不要求特定控制器。")]
        public Transform player;
        [Tooltip("三块画板，数组顺序即画作序号 1~3")]
        public PaintingBoard[] boards;
        [Tooltip("房间里桌上那块画板色块，进特写时隐藏")]
        public SpriteRenderer boardMarker;
        public Camera mainCamera;

        [Header("特写设置")]
        [Tooltip("特写相机的正交尺寸，越大画面越宽")]
        public float closeupOrthoSize = 2.6f;
        [Tooltip("相机相对画板组中心的垂直偏移")]
        public Vector3 closeupCameraOffset = new Vector3(0f, 0.2f, -10f);
        [Tooltip("三块画板合计占视野宽度的比例")]
        public float boardsWidthRatio = 0.92f;
        [Tooltip("画板之间的间隙（世界单位）")]
        public float boardGap = 0.22f;

        [Header("找画笔")]
        public bool hasBrush;

        [Tooltip("交互距离（只看水平方向）")]
        public float interactRange = 1.6f;

        [Header("房间显隐")]
        [Tooltip("房间手绘图根节点。进画板特写时整体隐藏、退出时恢复。留空则回退按名字查找 RoomWorld（旧装配兼容）")]
        public Transform roomRoot;

        [Header("交互提示")]
        public InteractionHint hint;

        /// <summary>进入画板特写时触发。</summary>
        public event System.Action EnteredPaintingMode;
        /// <summary>退出特写时触发。</summary>
        public event System.Action ExitedPaintingMode;
        /// <summary>某一块画板达标时触发，参数为画作序号（1~3）。</summary>
        public event System.Action<int> PaintingCompleted;

        Camera _roomCamera;
        Vector3 _roomCamPos;
        float _roomCamSize;
        bool _inCloseup;
        PlayerStats _stats;
        float _paintingSeconds;

        // 记录进入特写前的房间状态，退出时还原
        bool _preRoomPlayerActive;

        static GUIStyle _labelStyle;

        public bool IsPaintingMode => _inCloseup;

        /// <summary>总幅数。</summary>
        public int TotalPaintings => boards != null ? boards.Length : 0;

        /// <summary>
        /// 本局实际下笔累计秒数（IGameFlow.PaintingSeconds 的数据源）。
        /// 口径：只累计真正落笔的 dt，低落倍率不进来（S8 的倍率在
        /// DrawingCanvas.Tick 内部消化），退出特写自动暂停。
        /// </summary>
        public float PaintingSeconds => _paintingSeconds;

        /// <summary>已达标幅数。</summary>
        public int CompletedCount
        {
            get
            {
                int n = 0;
                if (boards != null)
                    foreach (var b in boards)
                        if (b != null && b.IsDone) n++;
                return n;
            }
        }

        /// <summary>三幅是否全部达标。</summary>
        public bool AllComplete => TotalPaintings > 0 && CompletedCount >= TotalPaintings;

        void Awake()
        {
            ResolveCamera();
            _roomCamera = mainCamera;
            if (_roomCamera != null)
            {
                _roomCamPos = _roomCamera.transform.position;
                _roomCamSize = _roomCamera.orthographicSize;
            }

            _stats = FindObjectOfType<PlayerStats>();
        }

        /// <summary>
        /// 找到用于特写的相机。
        /// 场景里的相机是 RoomSceneBuilder 运行时生成的，MainCamera 标签
        /// 在 Awake 阶段可能还没生效，所以找不到就退而求其次找正交相机。
        /// </summary>
        void ResolveCamera()
        {
            if (mainCamera != null) return;

            mainCamera = Camera.main;
            if (mainCamera != null) return;

            foreach (var c in FindObjectsOfType<Camera>())
            {
                if (c.orthographic) { mainCamera = c; break; }
            }
        }

        void Start()
        {
            // Awake 时相机可能还没就绪，这里再兜一次底
            if (_roomCamera == null)
            {
                ResolveCamera();
                _roomCamera = mainCamera;
                if (_roomCamera != null)
                {
                    _roomCamPos = _roomCamera.transform.position;
                    _roomCamSize = _roomCamera.orthographicSize;
                }
            }

            // 画板可能还没装配（漏跑菜单），这里兜底自愈一次
            if (boards != null)
            {
                foreach (var b in boards)
                    if (b != null) b.EnsureVisuals();
            }

            EnsureSubscriptions();
        }

        bool _subscribed;

        /// <summary>
        /// 订阅每块画板的达标事件。幂等，可在 Start / 进特写 / Update 里反复调用——
        /// 曾经出现过 Start 时 boards 还没就绪导致订阅丢失的情况，多入口兜底。
        /// </summary>
        void EnsureSubscriptions()
        {
            if (boards == null) return;
            foreach (var b in boards)
            {
                if (b == null || b.canvas == null) continue;
                b.canvas.Completed -= OnCanvasCompleted;   // 防重复订阅
                b.canvas.Completed += OnCanvasCompleted;
            }
            _subscribed = true;
        }

        void OnDestroy()
        {
            var im = InputManager.Instance;
            if (im != null)
            {
                im.Interact -= HandleInteract;
                im.CancelPressed -= HandleCancel;
            }
            _inputSubscribed = false;

            if (boards == null) return;
            foreach (var b in boards)
            {
                if (b == null || b.canvas == null) continue;
                b.canvas.Completed -= OnCanvasCompleted;
            }
        }

        void Update()
        {
            // 惰性兜底：万一 Start 时 boards 还没就绪，这里补上订阅
            if (!_subscribed && boards != null && boards.Length > 0) EnsureSubscriptions();
            EnsureInputSubscribed();

            if (_inCloseup) HandlePaintingInput();
        }

        bool _inputSubscribed;

        /// <summary>
        /// 惰性订阅 InputManager（策划案 1.7）：房间内 E 进特写、特写里 Esc/右键退出。
        /// Instance 未就绪时每帧重试，与 GameManager 同一套时序兜底。
        /// </summary>
        void EnsureInputSubscribed()
        {
            if (_inputSubscribed) return;
            var im = InputManager.Instance;
            if (im == null) return;
            im.Interact -= HandleInteract; im.Interact += HandleInteract;
            im.CancelPressed -= HandleCancel; im.CancelPressed += HandleCancel;
            _inputSubscribed = true;
        }

        /// <summary>房间内：走近画板按 E 进入特写（特写中不响应，画画走鼠标）。</summary>
        void HandleInteract()
        {
            if (_inCloseup) return;
            if (player != null && IsNearBoard()) TryEnterPainting();
        }

        /// <summary>特写中：Esc/右键退出。</summary>
        void HandleCancel()
        {
            if (_inCloseup) ExitPaintingMode();
        }

        /// <summary>
        /// 判断主角是否走到画板附近。
        /// 只比 X：画板挂在桌面上方，Y 比角色可达上限高出 1.3，二维距离永远够不到。
        /// </summary>
        bool IsNearBoard()
        {
            if (player == null) return false;
            return Mathf.Abs(player.position.x - transform.position.x) < interactRange;
        }

        /// <summary>
        /// 尝试进入画画。手上没画笔时给出反馈但不进入。
        /// </summary>
        public bool TryEnterPainting()
        {
            if (_inCloseup) return true;

            if (!hasBrush)
            {
                // 没画笔的反馈走对话系统（策划案 4.1：提示画笔位置）。
                // 正在播别的台词时不抢队列，避免把当前对白顶掉。
                if (Services.Dialogue == null || !Services.Dialogue.IsPlaying)
                {
                    var line = new DialogueLine
                    {
                        speaker = "",
                        text = "奇怪，画笔怎么不见了，应该是落在房间某处了吧",
                        autoAdvance = 3f
                    };
                    Services.Dialogue?.Play(new[] { line });
                }
                return false;
            }

            EnterPaintingMode();
            return true;
        }

        /// <summary>三块画板共同的父节点（装配时建立）。</summary>
        Transform BoardsContainer =>
            (boards != null && boards.Length > 0 && boards[0] != null) ? boards[0].transform.parent : null;

        public void EnterPaintingMode()
        {
            if (_inCloseup) return;

            if (boards == null || boards.Length == 0)
            {
                Debug.LogWarning("[画板] 尚未装配三块画板，请运行菜单 GameJam/SetupDrawingBoard");
                return;
            }

            EnsureSubscriptions();
            _inCloseup = true;

            // 隐藏房间内容，只留画板
            if (player != null)
            {
                _preRoomPlayerActive = player.gameObject.activeSelf;
                player.gameObject.SetActive(false);
            }
            SetRoomVisible(false);
            if (boardMarker != null) boardMarker.enabled = false;
            SetBoardsVisible(true);

            if (_roomCamera != null)
            {
                var container = BoardsContainer;
                if (container != null)
                {
                    // 先把画板组搬到场景根节点：DrawingBoard 自身带 1.6×1.1 缩放，
                    // 子物体会被叠乘，画布会直接顶出屏幕（这个坑踩过）。
                    container.SetParent(null, true);
                    container.localScale = Vector3.one;
                    container.localRotation = Quaternion.identity;
                }

                // 按相机视野重排三块画板
                FitBoardsToCamera();

                // 相机正对画板组中心，让画面居中
                Vector3 center = container != null ? container.position : transform.position;
                _roomCamera.orthographicSize = closeupOrthoSize;
                _roomCamera.transform.position =
                    new Vector3(center.x, center.y + closeupCameraOffset.y, closeupCameraOffset.z);
            }

            // 画画时状态掉得慢，且按策划案隐藏状态栏
            if (_stats != null) _stats.SetActivity(PlayerStats.Activity.Drawing);

            EnteredPaintingMode?.Invoke();
        }

        public void ExitPaintingMode()
        {
            if (!_inCloseup) return;
            _inCloseup = false;

            if (player != null) player.gameObject.SetActive(_preRoomPlayerActive);
            SetRoomVisible(true);
            SetBoardsVisible(false);
            if (boardMarker != null) boardMarker.enabled = true;

            // 清掉悬停高亮，回房间别再留金色框
            if (boards != null)
                foreach (var b in boards)
                    if (b != null) b.SetHover(false);

            // 画板组挂回 DrawingBoard（房间内不可见，缩放无所谓）
            var container = BoardsContainer;
            if (container != null)
            {
                container.SetParent(transform, false);
                container.localPosition = Vector3.zero;
            }

            if (_roomCamera != null)
            {
                _roomCamera.orthographicSize = _roomCamSize;
                _roomCamera.transform.position = _roomCamPos;
            }

            if (_stats != null) _stats.SetActivity(PlayerStats.Activity.Idle);

            ExitedPaintingMode?.Invoke();
        }

        void HandlePaintingInput()
        {
            // 输入统一走 InputManager（1.7）；退出特写改走 CancelPressed 事件订阅
            var im = InputManager.Instance;
            if (im == null) return;

            // 鼠标落在哪块画板上（不按键也能高亮）
            PaintingBoard target = FindBoardUnderMouse(im.PointerScreenPos, out Vector2 canvasPos);

            if (boards != null)
                foreach (var b in boards)
                    if (b != null) b.SetHover(b == target);

            if (im.PaintHeld)
            {
                if (target != null)
                {
                    target.canvas.DrawAt(canvasPos);
                    // 落笔才计时：60 秒是实打实的作画时间，
                    // 松开鼠标或换到别的画板，这块的计时就暂停。
                    target.canvas.Tick(Time.deltaTime);
                    // 同一口径累计到局计数（契约 IGameFlow.PaintingSeconds）
                    _paintingSeconds += Time.deltaTime;
                }
            }
            else
            {
                if (boards != null)
                    foreach (var b in boards)
                        if (b != null) b.canvas.LiftPen();
            }
        }

        void OnCanvasCompleted(DrawingCanvas c)
        {
            int index = 0;
            if (boards != null)
            {
                for (int i = 0; i < boards.Length; i++)
                {
                    if (boards[i] != null && boards[i].canvas == c)
                    {
                        index = boards[i].boardIndex;
                        break;
                    }
                }
            }

            PaintingCompleted?.Invoke(index);
            Debug.Log($"[画板] 第 {index} 幅达标（{CompletedCount}/{TotalPaintings}）");

            if (AllComplete)
                Debug.Log("[画板] 三幅画全部达标（结局逻辑后续再做）");
        }

        /// <summary>
        /// 按当前相机视野重排三块画板：以画板组中心为原点一字排开，
        /// 尺寸同时受视野宽度和高度约束，窄屏也不会挤出画面。
        /// </summary>
        void FitBoardsToCamera()
        {
            if (_roomCamera == null || boards == null || boards.Length == 0) return;

            var first = boards[0];
            if (first == null || first.canvas == null) return;

            float aspect = _roomCamera.aspect > 0.01f ? _roomCamera.aspect : 1.6f;
            float visH = closeupOrthoSize * 2f;
            float visW = visH * aspect;
            float canvasAspect = (float)first.canvas.canvasWidth / Mathf.Max(1, first.canvas.canvasHeight);

            // 宽度约束：三块 + 间隙 不超过视野宽度的 boardsWidthRatio
            float totalGap = boardGap * (boards.Length - 1);
            float boardW = (visW * boardsWidthRatio - totalGap) / boards.Length;

            // 高度约束：给计时条和顶部标签留出空间
            float maxH = visH * 0.72f;
            float boardH = boardW / canvasAspect;
            if (boardH > maxH)
            {
                boardH = maxH;
                boardW = boardH * canvasAspect;
            }

            float pitch = boardW + boardGap;
            // 以画板组中心为原点一字排开：第一块的中心在 -pitch*(n-1)/2，
            // 最后一块的右端刚好落在 +totalW/2（注意别用 -totalW/2 当起点，
            // 那样会把整组往左偏半个板宽）。
            float startX = -pitch * (boards.Length - 1) * 0.5f;

            for (int i = 0; i < boards.Length; i++)
            {
                var b = boards[i];
                if (b == null) continue;
                b.transform.localPosition = new Vector3(startX + pitch * i, 0f, 0f);
                b.Layout(boardW, boardH);
            }
        }

        /// <summary>找到鼠标位置下的画板，并给出画布内的像素坐标。</summary>
        PaintingBoard FindBoardUnderMouse(Vector3 screenPos, out Vector2 canvasPos)
        {
            canvasPos = Vector2.zero;
            if (boards == null) return null;

            foreach (var b in boards)
            {
                if (b == null || b.canvasRenderer == null || b.canvas == null) continue;
                if (TryScreenToRenderer(screenPos, b.canvasRenderer, b.canvas, out canvasPos))
                    return b;
            }
            return null;
        }

        /// <summary>
        /// 把屏幕坐标转成画布内的像素坐标。超出画布时返回 false。
        ///
        /// 用 Camera.WorldToScreenPoint 把画布包围盒的中心和两个轴向点投到屏幕，
        /// 反推出包围盒在屏幕上的矩形，再做归一化映射。
        /// 这样完全绕开"猜 z 距离"那一步——正交相机下深度不影响 x/y，
        /// 但如果以后换成透视相机，这个写法也不会崩。
        /// </summary>
        bool TryScreenToRenderer(Vector3 screenPos, SpriteRenderer renderer, DrawingCanvas canvas, out Vector2 canvasPos)
        {
            canvasPos = Vector2.zero;
            if (renderer == null || canvas == null || _roomCamera == null) return false;

            Bounds b = renderer.bounds;
            Vector3 center = b.center;
            Vector3 ext = b.extents;

            Vector3 c = _roomCamera.WorldToScreenPoint(center);
            Vector3 ex = _roomCamera.WorldToScreenPoint(center + new Vector3(ext.x, 0f, 0f));
            Vector3 ey = _roomCamera.WorldToScreenPoint(center + new Vector3(0f, ext.y, 0f));

            float halfW = Mathf.Abs(ex.x - c.x);
            float halfH = Mathf.Abs(ey.y - c.y);
            if (halfW < 0.001f || halfH < 0.001f) return false;

            float u = (screenPos.x - (c.x - halfW)) / (halfW * 2f);
            float v = (screenPos.y - (c.y - halfH)) / (halfH * 2f);
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

            canvasPos = new Vector2(u * canvas.canvasWidth, v * canvas.canvasHeight);
            return true;
        }

        /// <summary>控制三块画板的可见性（只动 renderer.enabled，不碰 SetActive）。</summary>
        void SetBoardsVisible(bool visible)
        {
            if (boards == null) return;
            foreach (var b in boards)
                if (b != null) b.SetVisible(visible);
        }

        /// <summary>
        /// 进特写时隐藏整个房间、退出时恢复。
        /// 手绘场景把房间图收在 roomRoot 下整体开关；留空时回退按名字找
        /// RoomWorld（兼容旧装配），再找不到就静默跳过。
        /// </summary>
        void SetRoomVisible(bool visible)
        {
            Transform room = roomRoot;
            if (room == null)
            {
                var go = GameObject.Find("RoomWorld");
                if (go == null) return;
                room = go.transform;
            }
            room.gameObject.SetActive(visible);
        }

        /// <summary>特写时在每块画板上方画一行状态文字（色块占位期的临时 UI）。</summary>
        void OnGUI()
        {
            if (!_inCloseup || boards == null || _roomCamera == null) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            foreach (var b in boards)
            {
                if (b == null || b.canvasRenderer == null) continue;

                // 画布顶边再往上一点，避开计时条
                Bounds bounds = b.canvasRenderer.bounds;
                Vector3 top = new Vector3(bounds.center.x, bounds.max.y + 0.42f, bounds.center.z);
                Vector3 sp = _roomCamera.WorldToScreenPoint(top);
                if (sp.z <= 0f) continue;

                string text;
                Color color;
                if (b.IsDone)
                {
                    text = $"第 {b.boardIndex} 幅 · 已达标（着墨 {b.InkRatio * 100f:F0}%）";
                    color = new Color32(0x9B, 0xE3, 0xA4, 0xFF);
                }
                else if (b.Started)
                {
                    text = $"第 {b.boardIndex} 幅 · {b.RemainingSeconds:F1}s（着墨 {b.InkRatio * 100f:F0}%）";
                    color = Color.white;
                }
                else
                {
                    text = $"第 {b.boardIndex} 幅 · 待动笔";
                    color = new Color32(0xDD, 0xD8, 0xCC, 0xFF);
                }

                var rect = new Rect(sp.x - 140f, Screen.height - sp.y - 14f, 280f, 28f);

                // 深色偏移当描边，白底房间下也看得清
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, _labelStyle);

                GUI.color = color;
                GUI.Label(rect, text, _labelStyle);
                GUI.color = Color.white;
            }
        }

        /// <summary>重置三块画板（开新局 / 调试重跑）。</summary>
        public void ResetBoards()
        {
            if (boards == null) return;
            foreach (var b in boards)
                if (b != null) b.ResetBoard();
        }

        /// <summary>R 重开时整体复位（IResettable）：退特写、收画笔、清画板、计时归零。</summary>
        public void ResetForNewRun()
        {
            if (_inCloseup) ExitPaintingMode();
            hasBrush = false;
            _paintingSeconds = 0f;
            ResetBoards();
        }

        /// <summary>给玩家发画笔（由找画笔流程调用）。</summary>
        public void GiveBrush()
        {
            hasBrush = true;
        }
    }
}
