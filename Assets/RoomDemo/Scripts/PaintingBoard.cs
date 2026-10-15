using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 单块画板：特写里三块并排中的一块。
    /// 组成：外框（Frame）+ 画布（Canvas）+ 计时条（BarBg / BarFill）。
    ///
    /// 三种状态：
    /// - 未动笔：木色外框，计时条隐藏；
    /// - 计时中：鼠标悬停时外框变金色高亮，计时条按进度填充；
    /// - 已达标：外框变绿、计时条填满。达标只是标记，不锁画。
    ///
    /// 所有可见性都通过 renderer.enabled 控制，绝不对子物体 SetActive(false)——
    /// 那会留下 activeInHierarchy=false 的脏状态，之后再设 enabled 也渲染不出来。
    /// </summary>
    public class PaintingBoard : MonoBehaviour
    {
        [Header("标识")]
        [Tooltip("第几幅画，1~3")]
        public int boardIndex = 1;

        [Header("引用")]
        public DrawingCanvas canvas;
        public SpriteRenderer canvasRenderer;
        public SpriteRenderer frame;
        public SpriteRenderer barBackground;
        public SpriteRenderer barFill;

        [Header("外框配色")]
        public Color normalColor = new Color32(0x8A, 0x62, 0x42, 0xFF);   // 木框
        public Color hoverColor = new Color32(0xE0, 0xA8, 0x3C, 0xFF);    // 悬停高亮
        public Color doneColor = new Color32(0x4F, 0x9E, 0x58, 0xFF);     // 达标

        [Header("计时条")]
        public Color barNormalColor = new Color32(0x5B, 0x7F, 0xB5, 0xFF);
        public float barHeight = 0.09f;
        [Tooltip("计时条中心距画板顶边还有多远")]
        public float barGapAbove = 0.18f;

        bool _hover;
        bool _lastDone;
        bool _visible;
        float _barFullWidth = 1f;

        /// <summary>这块画是否已达标（画满 15 秒）。</summary>
        public bool IsDone => canvas != null && canvas.IsComplete;

        /// <summary>是否已落笔（计时是否已启动）。</summary>
        public bool Started => canvas != null && canvas.TimerStarted;

        /// <summary>剩余秒数，未开始时为满值。</summary>
        public float RemainingSeconds => canvas != null ? canvas.RemainingSeconds : 0f;

        /// <summary>着墨量 0~1，仅用于标签展示。</summary>
        public float InkRatio => canvas != null ? canvas.InkRatio : 0f;

        void Awake()
        {
            EnsureVisuals();
        }

        /// <summary>
        /// 自愈：场景反序列化后，RoomShapes 里运行时生成的精灵引用会丢
        /// （没有资产 fileID），画布绑定同理。每次启动把该挂的挂上。
        /// </summary>
        public void EnsureVisuals()
        {
            if (frame != null && frame.sprite == null) frame.sprite = RoomShapes.Rect;
            if (barBackground != null && barBackground.sprite == null) barBackground.sprite = RoomShapes.Rect;
            if (barFill != null && barFill.sprite == null) barFill.sprite = RoomShapes.Bar;
            if (canvas != null && canvasRenderer != null) canvas.Bind(canvasRenderer);
            Refresh();
        }

        /// <summary>
        /// 按给定尺寸排布内部组件。
        /// 注意画布精灵的 PPU 等于纹理宽度，所以它的世界尺寸是 1×(高/宽)
        /// （512×384 的纹理就是 1×0.75），localScale 必须按这个比例补偿，
        /// 否则画布会被纵向压扁。
        /// 父物体必须保持 scale=1。
        /// </summary>
        public void Layout(float boardWidth, float boardHeight)
        {
            if (canvasRenderer != null)
            {
                float spriteH = canvas != null
                    ? (float)canvas.canvasHeight / Mathf.Max(1, canvas.canvasWidth)
                    : 1f;
                canvasRenderer.transform.localPosition = Vector3.zero;
                canvasRenderer.transform.localScale = new Vector3(boardWidth, boardHeight / spriteH, 1f);
            }

            if (frame != null)
            {
                // 外框比画布大一圈，垫在底下当边框。
                // 注意 RoomShapes.Rect 是<b>底中心锚点</b>，直接放原点会让框
                // 从画板中心往上长（底部对齐原点），必须上移半个框高才能居中。
                float fx = boardWidth + 0.14f;
                float fy = boardHeight + 0.14f;
                frame.transform.localPosition = new Vector3(0f, -fy * 0.5f, 0f);
                frame.transform.localScale = new Vector3(fx, fy, 1f);
            }

            // 计时条贴在画板顶边上方：BarBg 用底中心锚点，BarFill 用左中锚点，
            // 两者垂直中心对齐在同一高度
            float barW = boardWidth * 0.72f;
            float barY = boardHeight * 0.5f + barGapAbove;
            if (barBackground != null)
            {
                barBackground.transform.localPosition = new Vector3(0f, barY, 0f);
                barBackground.transform.localScale = new Vector3(barW, barHeight, 1f);
            }
            if (barFill != null)
            {
                barFill.transform.localPosition = new Vector3(-barW * 0.5f, barY + barHeight * 0.5f, 0f);
                barFill.transform.localScale = new Vector3(barW, barHeight, 1f);
            }
            _barFullWidth = barW;
        }

        /// <summary>特写里显示 / 回房间时隐藏。</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyVisibility();
            if (visible) Refresh();
        }

        void ApplyVisibility()
        {
            if (frame != null) frame.enabled = _visible;
            if (canvasRenderer != null) canvasRenderer.enabled = _visible;

            bool showBar = _visible && Started;
            if (barBackground != null) barBackground.enabled = showBar;
            if (barFill != null) barFill.enabled = showBar;
        }

        /// <summary>鼠标是否悬停在这块画板上。</summary>
        public void SetHover(bool hover)
        {
            if (_hover == hover) return;
            _hover = hover;
            Refresh();
        }

        void Refresh()
        {
            if (frame != null)
                frame.color = IsDone ? doneColor : (_hover ? hoverColor : normalColor);
            if (barFill != null)
                barFill.color = IsDone ? doneColor : barNormalColor;
        }

        void Update()
        {
            // 只在特写可见时管理视觉，避免回房间后被 Update 重新打开渲染器
            if (!_visible) return;

            if (IsDone != _lastDone)
            {
                _lastDone = IsDone;
                Refresh();
            }
            UpdateBar();
        }

        void UpdateBar()
        {
            if (barFill == null || canvas == null) return;

            bool showBar = Started;
            if (barBackground != null && barBackground.enabled != showBar)
                barBackground.enabled = showBar;
            if (barFill.enabled != showBar)
                barFill.enabled = showBar;
            if (!showBar) return;

            // 填充用左锚点精灵：缩放 x 即从左往右长
            var s = barFill.transform.localScale;
            s.x = Mathf.Max(0.001f, _barFullWidth * canvas.TimeProgress01);
            barFill.transform.localScale = s;
        }

        /// <summary>重置这块画板（开新局 / 调试重跑）。</summary>
        public void ResetBoard()
        {
            if (canvas != null) canvas.ResetPainting();
            _hover = false;
            Refresh();
            UpdateBar();
        }
    }
}
