using System.Collections.Generic;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 画板本体：负责在纹理上画笔触、累计着墨量、判断是否达标。
    ///
    /// 实现要点：
    /// - 笔触用<b>线段</b>连接两次采样的点，而不是一个个孤立的点，
    ///   否则鼠标移动稍快就会断成虚线。
    /// - 着墨量按"新涂到的像素数"累加，而不是按笔画长度累加，
    ///   这样来回涂抹不会重复刷分，玩家必须真的画出新内容。
    /// - 美术后期接入时，只要把 <see cref="CanvasTexture"/> 换掉即可，
    ///   判定逻辑完全不受影响。
    /// </summary>
    public class DrawingCanvas : MonoBehaviour
    {
        [Header("画布规格（像素）")]
        public int canvasWidth = 512;
        public int canvasHeight = 384;

        [Header("达标要求")]
        [Tooltip("需要涂满多少比例的画布才算完成")]
        [Range(0.01f, 0.6f)]
        public float requiredInkRatio = 0.22f;

        [Header("笔触")]
        public float brushRadius = 7f;
        public Color inkColor = new Color(0.16f, 0.18f, 0.24f, 1f);

        [Header("时间消耗")]
        [Tooltip("每涂满 1% 画布消耗的游戏内分钟数")]
        public float minutesPerInkPercent = 0.9f;

        /// <summary>画布纹理。外部可替换（美术资源接入点）。</summary>
        public Texture2D CanvasTexture { get; private set; }

        /// <summary>着墨量占画布总像素的比例，0~1。</summary>
        public float InkRatio { get; private set; }

        /// <summary>着墨量 0~1，用于驱动进度条。</summary>
        public float InkProgress01 => Mathf.Clamp01(InkRatio / requiredInkRatio);

        /// <summary>是否已达到"画完"的标准。</summary>
        public bool IsComplete => InkRatio >= requiredInkRatio;

        /// <summary>当前画作序号，1~3。</summary>
        public int CurrentPaintingIndex { get; private set; } = 1;

        /// <summary>需要完成的总幅数。</summary>
        public int totalPaintings = 3;

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
  // 纹理只有 512×384 RGBA（≈786KB），直接重建最稳。
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

        /// <summary>把画布清空为纸色。切换到新一幅画时调用。</summary>
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
        /// 落笔。传入的是画布内的像素坐标。
        /// </summary>
        public void DrawAt(Vector2 canvasPos)
        {
            EnsureBuilt();

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

        /// <summary>该像素是否已经有墨（用于去重计分）。</summary>
        bool InkPixel(int x, int y)
        {
            var c = _pixels[y * canvasWidth + x];
            return c.r < 200 && c.g < 200 && c.b < 200;
        }

        /// <summary>
        /// 完成一幅画，推进到下一幅。
        /// 按用户选择：每幅画独立，画完清空重来。
        /// </summary>
        /// <returns>true 表示还有下一幅</returns>
        public bool AdvanceToNextPainting()
        {
            if (CurrentPaintingIndex >= totalPaintings)
            {
                CurrentPaintingIndex = totalPaintings;
                return false;
            }

            CurrentPaintingIndex++;
            ClearCanvas();
            return true;
        }

        public void ResetAllPaintings()
        {
            CurrentPaintingIndex = 1;
            ClearCanvas();
        }

        /// <summary>完成当前这幅画应消耗的分钟数，受状态影响。</summary>
        public float RequiredMinutes(float conditionFactor)
        {
            // conditionFactor 越低（状态越差）耗时越长，最低 0.5 → 2 倍
            float multiplier = Mathf.Lerp(2f, 1f, Mathf.Clamp01(conditionFactor));
            return requiredInkRatio * 100f * minutesPerInkPercent * multiplier;
        }
    }
}