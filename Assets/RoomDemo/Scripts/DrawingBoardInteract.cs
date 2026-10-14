using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 画板玩法的交互控制：把"在房间里点画板"和"在特写里画画"串起来。
    ///
    /// 按用户选择：
    /// - 进入画板是<b>桌面特写俯视</b>视角（不是第一人称平视）；
    /// - 完成判定是<b>着墨量达标</b>；
    /// - <b>需要先拿到画笔</b>才能画；
    /// - 三幅画<b>各自独立</b>，画完清空。
    /// </summary>
    public class DrawingBoardInteract : MonoBehaviour
    {
        [Header("引用")]
        public RoomPlayer player;
        public DrawingCanvas canvas;
        public SpriteRenderer canvasRenderer;
        public Camera mainCamera;

        [Header("特写设置")]
        [Tooltip("特写相机的正交尺寸，越大画面越宽")]
        public float closeupOrthoSize = 2.6f;
        public Vector3 closeupCameraOffset = new Vector3(0f, 0.35f, -10f);
        [Tooltip("画布在世界里的宽度（高度按画布像素比例自动算）")]
        public float canvasWorldWidth = 4.4f;

        [Header("找画笔")]
        public bool hasBrush;

        [Tooltip("交互距离（只看水平方向）")]
        public float interactRange = 1.6f;

        [Header("交互提示")]
        public InteractionHint hint;

        [Header("状态影响")]
        [Tooltip("状态越差，完成一幅画需要的时间越长")]
        public bool scaleTimeByCondition = true;

        Camera _roomCamera;
        Vector3 _roomCamPos;
        float _roomCamSize;
        bool _inCloseup;

        // 记录进入特写前的房间状态，退出时还原
        bool _preRoomPlayerActive;

        /// <summary>进入画板特写时触发。</summary>
        public event System.Action EnteredPaintingMode;
        /// <summary>退出特写时触发。</summary>
        public event System.Action ExitedPaintingMode;
        /// <summary>完成一幅画时触发，参数为画作序号（1~3）。</summary>
        public event System.Action<int> PaintingCompleted;

        PlayerStats _stats;
        DayTimer _timer;
        float _minutesThisPainting;

        public bool IsPaintingMode => _inCloseup;

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
            _timer = FindObjectOfType<DayTimer>();
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

            if (mainCamera != null) _roomCamera = mainCamera;
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

            if (canvas != null && canvasRenderer != null) canvas.Bind(canvasRenderer);
        }

        void Update()
        {
            if (!_inCloseup)
            {
                // 房间内：走近画板按 E 进入
                if (player != null && IsNearBoard() && Input.GetKeyDown(KeyCode.E))
                    TryEnterPainting();
                return;
            }

            HandlePaintingInput();
        }

        /// <summary>
        /// 判断主角是否走到画板附近。
        /// 和 <see cref="RoomInteractable.PlayerInRange"/> 同理，只比 X：
        /// 画板挂在桌面上方，Y 比角色可达上限高出 1.3，二维距离永远够不到。
        /// </summary>
        bool IsNearBoard()
        {
            if (player == null) return false;
            return Mathf.Abs(player.transform.position.x - transform.position.x) < interactRange;
        }

        /// <summary>
        /// 尝试进入画画。手上没画笔时给出反馈但不进入。
        /// </summary>
        public bool TryEnterPainting()
        {
            if (_inCloseup) return true;

            if (!hasBrush)
            {
                Debug.Log("[画板] 「奇怪，画笔怎么不见了，应该是落在房间某处了吧」");
                return false;
            }

            EnterPaintingMode();
            return true;
        }

        public void EnterPaintingMode()
        {
            if (_inCloseup) return;
            _inCloseup = true;
            _minutesThisPainting = 0f;

            // 隐藏房间内容，只留画板
            if (player != null)
            {
                _preRoomPlayerActive = player.gameObject.activeSelf;
                player.gameObject.SetActive(false);
            }
            SetRoomVisible(false);
            SetCanvasVisible(true);

            // 切换到特写相机
if (_roomCamera != null)
            {
    // 兜底：历史上可能被 SetActive(false) 过，这里强制激活
     if (canvasRenderer != null && !canvasRenderer.gameObject.activeSelf)
       canvasRenderer.gameObject.SetActive(true);

         // 画布颜色固定为白，别让交互提示的高亮色染到画布上
    if (canvasRenderer != null) canvasRenderer.color = Color.white;

            // 先把画布搬到根节点，脱离父级缩放
         DetachCanvas();
                FitCanvasToCamera();

                // 相机正对画布中心，让画面居中
                Vector3 center = canvasRenderer != null
                    ? canvasRenderer.bounds.center
                    : transform.position;
                _roomCamera.orthographicSize = closeupOrthoSize;
                _roomCamera.transform.position =
                    new Vector3(center.x, center.y, closeupCameraOffset.z);
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
            SetCanvasVisible(false);
            AttachCanvasBack();

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
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                ExitPaintingMode();
                return;
            }

            Vector2 canvasPos;
            if (!TryScreenToCanvas(Input.mousePosition, out canvasPos)) return;

            if (Input.GetMouseButton(0))
            {
                // 状态越差，完成同样的着墨量要花更多时间
                if (scaleTimeByCondition && _stats != null)
                {
                    float cond = _stats.ConditionFactor;
                    _minutesThisPainting += Time.deltaTime * TimeCostPerSecond(cond);
                }

                canvas.DrawAt(canvasPos);
            }
            else
            {
                canvas.LiftPen();
            }

            if (canvas.IsComplete)
                CompletePainting();
        }

        /// <summary>
        /// 画画时的实际耗时换算。
        /// 让"完成一幅画的总耗时"稳定等于 RequiredMinutes，而不是随着帧率漂移。
        /// </summary>
        float TimeCostPerSecond(float conditionFactor)
        {
            float targetMinutes = canvas.RequiredMinutes(conditionFactor);
            // 以达标所需的着墨量为分母，把实时着墨增量换算成等比例的时间
            float perPercentNeeded = targetMinutes / (canvas.requiredInkRatio * 100f);
            float deltaPercent = Time.deltaTime * 0.02f; // 每秒按画布 2% 的速度推进进度
            return perPercentNeeded * deltaPercent;
        }

        void CompletePainting()
        {
            int finishedIndex = canvas.CurrentPaintingIndex;

            if (_timer != null && scaleTimeByCondition)
                _timer.SpendMinutes(canvas.RequiredMinutes(_stats != null ? _stats.ConditionFactor : 1f));

            PaintingCompleted?.Invoke(finishedIndex);
            Debug.Log($"[画板] 完成第 {finishedIndex} 幅画");

            bool hasNext = canvas.AdvanceToNextPainting();
            if (!hasNext)
            {
                Debug.Log("[画板] 三幅画全部完成，进入结算（结局逻辑后续再做）");
                ExitPaintingMode();
            }
        }

        /// <summary>
        /// 把屏幕坐标转成画布内的像素坐标。超出画布时返回 false。
        ///
        /// 用 Camera.WorldToScreenPoint 把画布包围盒的四个角投到屏幕，
        /// 反推出包围盒在屏幕上的矩形，再做归一化映射。
        /// 这样完全绕开"猜 z 距离"那一步——正交相机下深度不影响 x/y，
        /// 但如果以后换成透视相机，这个写法也不会崩。
        /// </summary>
        bool TryScreenToCanvas(Vector3 screenPos, out Vector2 canvasPos)
        {
            canvasPos = Vector2.zero;
            if (canvas == null || canvasRenderer == null || _roomCamera == null) return false;

            Bounds b = canvasRenderer.bounds;
            Vector3 center = b.center;
            Vector3 ext = b.extents;

            // 把包围盒中心投到屏幕，再算半宽半高的屏幕跨度
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

        /// <summary>
        /// 把画布精灵缩放到刚好填满特写视野。
        ///
        /// 画布在进入特写时会被<b>临时挂到场景根节点</b>，这样它就不会继承
        /// DrawingBoard 自身的缩放，世界尺寸完全由这里说了算。
        /// </summary>
        void FitCanvasToCamera()
        {
            if (canvasRenderer == null || canvas == null || _roomCamera == null) return;

            float aspect = (float)canvas.canvasWidth / Mathf.Max(1, canvas.canvasHeight);

            var ls = canvasRenderer.transform.localScale;
            ls.x = canvasWorldWidth;
            ls.y = canvasWorldWidth / aspect;
            canvasRenderer.transform.localScale = ls;

            // 画布要压在书桌之上，否则会被桌面色块盖住看不见
            canvasRenderer.sortingOrder = RoomView.LayerPropsMax + 100;
        }

        /// <summary>
        /// 特写期间把画布搬到场景根节点，避免父级缩放干扰。
        /// 退出时再挂回 DrawingBoard。
        /// </summary>
        void DetachCanvas()
        {
            if (canvasRenderer == null) return;
            if (canvasRenderer.transform.parent == null) return;
            canvasRenderer.transform.SetParent(null, true);
        }

        void AttachCanvasBack()
        {
            if (canvasRenderer == null || transform == null) return;
            canvasRenderer.transform.SetParent(transform, false);
        }

        /// <summary>
        /// 控制画布可见性。
        ///
        /// 重要：画布精灵的父物体就是本物体自己，所以这里<b>只能</b>操作
        /// renderer.enabled，绝对不能 SetActive 整个物体——
        /// 否则一进特写就把画布自己关掉了，鼠标怎么拖都没反应。
        /// </summary>
        void SetCanvasVisible(bool visible)
        {
            if (canvasRenderer == null) return;
            // 两个都要设。只改 enabled 不够——物体一旦被 SetActive(false) 过，
            // activeInHierarchy 会一直是 false，渲染器 enabled 也救不回来，
            // 结果就是画布永远不显示。这里对 Canvas 自身 SetActive 是安全的。
            canvasRenderer.enabled = visible;
            canvasRenderer.gameObject.SetActive(visible);
        }

        void SetRoomVisible(bool visible)
        {
            var room = GameObject.Find("RoomWorld");
            if (room == null) return;
            // 画板自己所在的这条链要留着
            var shell = room.transform.Find("Shell");
            if (shell != null) shell.gameObject.SetActive(visible);
            var props = room.transform.Find("Props");
            if (props != null) props.gameObject.SetActive(visible);
        }

        /// <summary>给玩家发画笔（由找画笔流程调用）。</summary>
        public void GiveBrush()
        {
            hasBrush = true;
        }
    }
}