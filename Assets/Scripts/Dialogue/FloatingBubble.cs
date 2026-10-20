using System.Collections;
using TMPro;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 从起始点往上飘浮上升、渐隐消失的圆形提示气泡。
    ///
    /// 气泡是半透明圆形（程序生成的径向圆形贴图），文字用 TextMeshPro + SDF 字体，
    /// 保证中文清晰。可从 startOffset 处出发往上飘升，到顶后淡出销毁。
    /// 支持按间隔自动循环（可限定只在画板特写期间飘）。
    /// </summary>
    public class FloatingBubble : MonoBehaviour
    {
        [Header("文字")]
        [Tooltip("气泡文字。可在 Inspector 里改，或运行时调 Show(text) 覆盖。")]
        public string text = "喵~";

        [Header("外观")]
        [Tooltip("文字使用的 TMP SDF 字体（拖项目里的 .asset 字体，如阿里普惠体 SDF）。")]
        public TMP_FontAsset fontAsset;
        [Tooltip("文字字号（TextMeshPro 的 fontSize）。")]
        public float fontSize = 8f;
        [Tooltip("气泡横向直径（世界单位，椭圆长轴）。")]
        public float bubbleWidth = 1.8f;
        [Tooltip("气泡纵向直径（世界单位，椭圆短轴）。")]
        public float bubbleHeight = 1.1f;
        [Tooltip("气泡填充色（RGBA，A 控制透明度，如 0.35 半透明）。")]
        public Color bubbleColor = new Color(1f, 1f, 1f, 0.35f);
        [Tooltip("文字颜色。")]
        public Color textColor = new Color(0.30f, 0.28f, 0.32f, 1f);

        [Header("飘动")]
        [Tooltip("飘升速度（世界单位/秒）。")]
        public float riseSpeed = 0.8f;
        [Tooltip("相对起始点上升的总高度。")]
        public float riseHeight = 1.2f;
        [Tooltip("飘到顶后淡出的时长（秒）。")]
        public float fadeDuration = 0.5f;
        [Tooltip("起始偏移（相对挂载物体）。")]
        public Vector3 startOffset = new Vector3(0f, -0.5f, 0f);

        [Header("层级")]
        [Tooltip("气泡底板的 sortingOrder，需高于画板、低于 UI。")]
        public int sortingOrder = 15110;

        [Header("自动循环")]
        [Tooltip("勾选后按间隔自动反复飘，无需手动调用。")]
        public bool autoRepeat = false;
        [Tooltip("两次飘浮之间的间隔（秒）。")]
        public float repeatInterval = 3f;
        [Tooltip("勾选后场景加载即开始自动循环（或播放一次）。")]
        public bool playOnStart = false;
        [Tooltip("勾选后只在画板特写期间飘：进特写自动开始循环，退出自动停止。")]
        public bool onlyInPaintingMode = false;

        GameObject _bubble;
        SpriteRenderer _bg;
        TMP_Text _tm;
        Vector3 _startWorldPos;
        bool _autoLooping;
        Coroutine _floatRoutine;
        Coroutine _autoRoutine;

        void OnEnable()
        {
            if (!onlyInPaintingMode) return;
            var db = FindObjectOfType<DrawingBoardInteract>();
            if (db == null) return;
            db.EnteredPaintingMode -= OnEnterPainting;
            db.EnteredPaintingMode += OnEnterPainting;
            db.ExitedPaintingMode -= OnExitPainting;
            db.ExitedPaintingMode += OnExitPainting;
        }

        void OnDisable()
        {
            if (!onlyInPaintingMode) return;
            var db = FindObjectOfType<DrawingBoardInteract>();
            if (db != null)
            {
                db.EnteredPaintingMode -= OnEnterPainting;
                db.ExitedPaintingMode -= OnExitPainting;
            }
            StopAutoRepeat();
        }

        void OnEnterPainting()
        {
            if (autoRepeat) StartAutoRepeat();
            else Show(text);
        }

        void OnExitPainting()
        {
            StopAutoRepeat();
            if (_floatRoutine != null) { StopCoroutine(_floatRoutine); _floatRoutine = null; }
            if (_bubble != null) { Destroy(_bubble); _bubble = null; }
        }

        void Start()
        {
            if (onlyInPaintingMode) return;
            if (playOnStart)
            {
                if (autoRepeat) StartAutoRepeat();
                else Show(text);
            }
        }

        /// <summary>开始自动循环：按 repeatInterval 间隔反复飘一次。可重复调用（幂等）。</summary>
        public void StartAutoRepeat()
        {
            if (_autoLooping) return;
            _autoLooping = true;
            _autoRoutine = StartCoroutine(AutoRepeatRoutine());
        }

        /// <summary>停止自动循环。</summary>
        public void StopAutoRepeat()
        {
            _autoLooping = false;
            if (_autoRoutine != null) { StopCoroutine(_autoRoutine); _autoRoutine = null; }
        }

        IEnumerator AutoRepeatRoutine()
        {
            while (_autoLooping)
            {
                _startWorldPos = transform.position + startOffset;
                BuildBubble();
                yield return FloatRoutine();
                yield return new WaitForSeconds(repeatInterval);
            }
        }

        /// <summary>播放一次飘浮气泡。可传新文字覆盖 Inspector 默认值。</summary>
        public void Show(string bubbleText = null)
        {
            if (!string.IsNullOrEmpty(bubbleText)) text = bubbleText;
            if (_floatRoutine != null) { StopCoroutine(_floatRoutine); _floatRoutine = null; }
            if (_bubble != null) { Destroy(_bubble); _bubble = null; }

            _startWorldPos = transform.position + startOffset;
            BuildBubble();
            _floatRoutine = StartCoroutine(FloatRoutine());
        }

        void BuildBubble()
        {
            _bubble = new GameObject("FloatingBubble");
            _bubble.transform.position = _startWorldPos;

            // 半透明椭圆底板（圆形贴图 + X/Y 不等缩放）
            _bg = _bubble.AddComponent<SpriteRenderer>();
            _bg.sprite = MakeCircleSprite(128);
            _bg.color = bubbleColor;
            _bg.sortingOrder = sortingOrder;
            _bubble.transform.localScale = new Vector3(bubbleWidth, bubbleHeight, 1f);

            // 文字（TextMeshPro，SDF 字体，清晰）
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(_bubble.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            _tm = textGo.AddComponent<TextMeshPro>();
            _tm.text = text;
            if (fontAsset != null) _tm.font = fontAsset;
            _tm.fontSize = fontSize;
            _tm.alignment = TextAlignmentOptions.Center;
            _tm.color = textColor;
            // 文字占满椭圆内切区域
            var rt = _tm.rectTransform;
            rt.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);

            var mr = textGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = sortingOrder + 1;
        }

        /// <summary>生成一张径向渐变的圆形贴图（中心不透明、边缘渐隐，抗锯齿）。</summary>
        static Sprite MakeCircleSprite(int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = resolution * 0.5f;
            var pixels = new Color32[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // 1 像素软边抗锯齿
                    float edge = 1f - Mathf.Clamp01((d - 0.92f) / 0.08f);
                    byte a = (byte)(255f * edge);
                    pixels[y * resolution + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f), resolution);
        }

        IEnumerator FloatRoutine()
        {
            Vector3 start = _startWorldPos;
            Vector3 end = start + Vector3.up * riseHeight;
            float elapsed = 0f;

            while (elapsed < riseHeight / Mathf.Max(0.001f, riseSpeed))
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / (riseHeight / Mathf.Max(0.001f, riseSpeed)));
                if (_bubble != null)
                    _bubble.transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            if (_bubble != null) _bubble.transform.position = end;

            float fadeElapsed = 0f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(fadeElapsed / fadeDuration);
                ApplyAlpha(a);
                yield return null;
            }

            if (_bubble != null) Destroy(_bubble);
            _bubble = null;
            _floatRoutine = null;
        }

        void ApplyAlpha(float a)
        {
            if (_bg != null) _bg.color = new Color(bubbleColor.r, bubbleColor.g, bubbleColor.b, bubbleColor.a * a);
            if (_tm != null) _tm.color = new Color(textColor.r, textColor.g, textColor.b, textColor.a * a);
        }

        void OnDestroy()
        {
            if (_bubble != null) Destroy(_bubble);
        }
    }
}
