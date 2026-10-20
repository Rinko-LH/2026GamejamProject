using System;
using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 画布本体：在一块纹理上画笔触，并按<b>时间</b>判定是否画完。
    ///
    /// 实现要点：
    /// - 笔触用<b>线段</b>连接两次采样的点，而不是一个个孤立的点，
    ///   否则鼠标移动稍快就会断成虚线。
    /// - 着墨量按"新涂到的像素数"累加，重复涂抹同一区域不会涨——
    ///   现在它只用于展示（"着墨 23%"），不再决定是否画完。
    /// - 完成判定改为<b>计时</b>：落第一笔开始计时，画满
    ///   <see cref="paintingSeconds"/> 秒即达标。计时只在特写里推进
    ///   （由 <see cref="DrawingBoardInteract"/> 在玩家落笔时调 <see cref="Tick"/>），
    ///   中途退出特写会暂停，回来接着算。
    /// - 美术后期接入时，只要把 <see cref="CanvasTexture"/> 换掉即可，
    ///   判定逻辑完全不受影响。
    /// </summary>
    public class DrawingCanvas : MonoBehaviour
    {
        [Header("画布规格（像素）")]
        public int canvasWidth = 512;
        public int canvasHeight = 384;

        [Header("达标要求")]
        [Tooltip("画满多少秒算达标（落笔计时，松开鼠标/切画板会暂停）。数值重规划案：60 秒")]
        public float paintingSeconds = 60f;

        [Header("低落惩罚（S7，策划案 5.4）")]
        [Tooltip("低落（任一状态<20%）时作画效率 = 1/该值：60s 达标线需要 90s 真实时间。倍率内化在 Tick 里，不动调用点")]
        public float lowMoodScale = 1.5f;

        [Header("笔触")]
        public float brushRadius = 7f;
        public Color inkColor = new Color(0.16f, 0.18f, 0.24f, 1f);

        /// <summary>画布纹理。外部可替换（美术资源接入点）。</summary>
        public Texture2D CanvasTexture { get; private set; }

        /// <summary>着墨量占画布总像素的比例，0~1。仅用于展示，不参与达标判定。</summary>
        public float InkRatio { get; private set; }

        /// <summary>是否已经落过笔（计时是否已启动）。</summary>
        public bool TimerStarted { get; private set; }

        /// <summary>已经画了多少秒。</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>距离达标还剩多少秒。</summary>
        public float RemainingSeconds => Mathf.Max(0f, paintingSeconds - ElapsedSeconds);

        /// <summary>计时进度 0~1。</summary>
        public float TimeProgress01 => paintingSeconds <= 0f ? 1f : Mathf.Clamp01(ElapsedSeconds / paintingSeconds);

        /// <summary>是否已达标。达标只是标记，不锁画——玩家可以继续改。</summary>
        public bool IsComplete { get; private set; }

        /// <summary>刚好画满时触发一次（由 <see cref="Tick"/> 推进到达标线时触发），参数是这块画布。</summary>
        public event Action<DrawingCanvas> Completed;

        SpriteRenderer _sr;
        Texture2D _tex;
        Color32[] _pixels;
        Vector2Int _lastDrawPoint;
        bool _hasLastPoint;

        void Awake()
        {
            BuildCanvas();
        }

        /// <summary>
        /// 确保纹理已就绪。菜单装配时 Awake 可能还没跑，所以提供惰性入口。
        /// </summary>
        public void EnsureBuilt()
        {
            if (_pixels == null || CanvasTexture == null) BuildCanvas();
        }

        /// <summary>
        /// 当前真正被绘制、被渲染的那张纹理。
        /// 外部一律用这个，不要直接摸 <see cref="CanvasTexture"/>——
        /// 万一 _tex 因为任何原因被重建过，这个能保证拿到在用的那张。
        /// </summary>
        public Texture2D ActiveTexture
        {
            get
            {
                EnsureBuilt();
                return _tex != null ? _tex : CanvasTexture;
            }
        }

        void BuildCanvas()
        {
            // 每次都新建一张纹理。
            //
            // 关键：这里<b>不复用</b> CanvasTexture。
            // 装配菜单在编辑器模式下会调 EnsureBuilt() 生成一张纹理，
            // 而播放时 Awake 又会重建——两次调用产生的 Texture2D 是不同对象，
            // 一旦材质挂的是上一张、逻辑写的是新张，笔迹就表现为"凭空消失"。
            // 纹理只有 512×384 RGBA（约 786KB），直接重建最稳。
            _tex = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false)
            {
                name = "PaintingCanvas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            CanvasTexture = _tex;

            _pixels = new Color32[canvasWidth * canvasHeight];
            ClearPixels();
            ApplyPixels();
        }

        /// <summary>把画布清空为纸色。</summary>
        public void ClearCanvas()
        {
            EnsureBuilt();
            ClearPixels();
            ApplyPixels();
            InkRatio = 0f;
            _hasLastPoint = false;
        }

        void ClearPixels()
        {
            // 纸色用白偏暖，和房间木色区分开
            Color32 paper = new Color32(0xF7, 0xF3, 0xE8, 0xFF);
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = paper;
        }

        void ApplyPixels()
        {
            if (_tex == null) return;
            _tex.SetPixels32(_pixels);
            _tex.Apply(false);

            // 纹理如果被重建过（比如 BuildCanvas 又跑了一次），
            // sprite 仍然指向上一次那张，必须重新绑回去，
            // 否则会出现"逻辑在画新图、屏幕显示旧图（空白）"。
            if (_sr != null && _sr.sprite != null && _sr.sprite.texture != _tex)
            {
                _sr.sprite = Sprite.Create(
                    _tex,
                    new Rect(0f, 0f, canvasWidth, canvasHeight),
                    new Vector2(0.5f, 0.5f),
                    canvasWidth);
            }
        }

        /// <summary>
        /// 绑定到场景里的画板精灵上。
        ///
        /// ⚠️ <b>必须把画布纹理做成 Sprite 赋给 renderer.sprite</b>，
        /// 光设 <c>material.mainTexture</c> 是<b>没用的</b>：
        /// Sprites/Default 在 sprite 已有纹理时会直接采样 sprite.texture，
        /// 材质上那张会被忽略。结果就是纸色能显示（因为 RoomRect 是白图），
        /// 但笔迹永远不显示。
        ///
        /// 这个坑排查起来很费时间：纹理里确实有像素、InkRatio 也正常涨，
        /// 但真实渲染出来一片纯色。只有渲染到 RenderTexture 读回像素才能确认。
        /// </summary>
        public void Bind(SpriteRenderer renderer)
        {
            _sr = renderer;
            EnsureBuilt();
            if (_sr == null || _tex == null) return;

            // 画布纹理 → Sprite → 赋给渲染器（核心）
            //
            // 用 1×1 的归一化 Sprite（PPU 等于纹理宽高），这样 sprite 的世界尺寸
            // 恒为 1×1，实际大小完全由 transform.localScale 决定。
            // 若直接用默认 PPU=100，世界尺寸会变成 512/100=5.12 再乘 scale，
            // 画布会被撑得远超视野。
            var sp = Sprite.Create(
                _tex,
                new Rect(0f, 0f, canvasWidth, canvasHeight),
                new Vector2(0.5f, 0.5f),
                canvasWidth);   // PPU = 宽 → 世界尺寸 = 宽/宽 = 1
            sp.name = "PaintingSprite";
            _sr.sprite = sp;

            // 材质用默认 Sprite 材质即可，此时它采样的就是上面的 sprite
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.name = "PaintingCanvasMat";
            _sr.material = mat;
            _sr.color = Color.white;
        }

        /// <summary>
        /// 落笔。传入的是画布内的像素坐标。第一笔会启动计时。
        /// </summary>
        public void DrawAt(Vector2 canvasPos)
        {
            EnsureBuilt();

            // 落笔即开始计时这幅画的 60 秒
            if (!TimerStarted && !IsComplete) TimerStarted = true;

            var p = new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(canvasPos.x), 0, canvasWidth - 1),
                Mathf.Clamp(Mathf.RoundToInt(canvasPos.y), 0, canvasHeight - 1));

            int painted = StampDisk(p);

            if (_hasLastPoint)
            {
                // 把上一次到这一次的空隙补上，避免快速移动时断线
                painted += StrokeBetween(_lastDrawPoint, p);
            }

            _lastDrawPoint = p;
            _hasLastPoint = true;

            if (painted > 0)
            {
                InkRatio += (float)painted / _pixels.Length;
                ApplyPixels();
            }
        }

        /// <summary>抬笔。下次落笔会重新起笔，不会连上上次的笔画。</summary>
        public void LiftPen()
        {
            _hasLastPoint = false;
        }

        /// <summary>
        /// 推进计时。只有"正在这块画板上落笔"时才该被调用，
        /// 所以计时本身就是实打实的作画时间。
        /// </summary>
        /// <summary>
        /// 达标容差（秒）。浮点累加（真实 dt 或定值 tick）可能差一丝到不了达标线
        /// ——float32 在 32~64 区间的 ULP 约 3.8e-6，1200 次累加误差可达 1e-3，
        /// 不容差的话进度条会卡在 99.99% 永远差一丝。10ms 对 60 秒的作画无感知。
        /// </summary>
        const float CompletionEpsilon = 0.01f;

        public void Tick(float dt)
        {
            if (IsComplete || !TimerStarted || dt <= 0f) return;

            // S7 低落机制：低落时每秒真实时间只推进 1/1.5 的作画进度——
            // 达标线仍是 60s（进度条语义不变），但真实耗时变成 90s。
            // 只在这里内化倍率：调用点（DBI）传的是原始 dt，PaintingSeconds
            // 统计口径不受倍率污染（契约明文）。
            var stats = Services.Stats;
            bool low = stats != null && stats.IsLowMood;
            ElapsedSeconds += dt / (low ? Mathf.Max(1f, lowMoodScale) : 1f);
            if (ElapsedSeconds >= paintingSeconds - CompletionEpsilon)
            {
                // 吸附到达标线：容差内一律视为画满，进度/剩余秒数显示归零
                ElapsedSeconds = paintingSeconds;
                IsComplete = true;
                Completed?.Invoke(this);
            }
        }

        /// <summary>以画板中心为圆心画一个实心圆，返回新增像素数。</summary>
        int StampDisk(Vector2Int center)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(brushRadius));
            int r2 = r * r;
            int painted = 0;

            int minX = Mathf.Max(0, center.x - r);
            int maxX = Mathf.Min(canvasWidth - 1, center.x + r);
            int minY = Mathf.Max(0, center.y - r);
            int maxY = Mathf.Min(canvasHeight - 1, center.y + r);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - center.x;
                    int dy = y - center.y;
                    if (dx * dx + dy * dy > r2) continue;
                    if (InkPixel(x, y)) continue;
                    _pixels[y * canvasWidth + x] = inkColor;
                    painted++;
                }
            }
            return painted;
        }

        /// <summary>在两点之间补画，间距不超过笔刷半径的一半以免出现断点。</summary>
        int StrokeBetween(Vector2Int a, Vector2Int b)
        {
            int dist = Mathf.RoundToInt(Vector2Int.Distance(a, b));
            if (dist <= 1) return 0;

            int step = Mathf.Max(1, Mathf.RoundToInt(brushRadius * 0.4f));
            int total = 0;
            for (int d = 0; d <= dist; d += step)
            {
                float t = (float)d / dist;
                int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
                if (x < 0 || y < 0 || x >= canvasWidth || y >= canvasHeight) continue;
                total += StampDisk(new Vector2Int(x, y));
            }
            return total;
        }

        /// <summary>该像素是否已经有墨（用于去重计展示量）。</summary>
        bool InkPixel(int x, int y)
        {
            var c = _pixels[y * canvasWidth + x];
            return c.r < 200 && c.g < 200 && c.b < 200;
        }

        /// <summary>重置这块画板：清空纸面 + 计时归零（开新局时用）。</summary>
        public void ResetPainting()
        {
            ClearCanvas();
            ElapsedSeconds = 0f;
            TimerStarted = false;
            IsComplete = false;
        }
    }
}
