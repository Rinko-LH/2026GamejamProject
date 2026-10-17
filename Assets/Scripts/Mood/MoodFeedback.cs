using RoomDemo.Contracts;
using RoomDemo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo.Mood
{
    /// <summary>
    /// 低落表现（S8，策划案 5.4）：任一状态 &lt;20% 时——
    /// ① 全屏去饱和：UIRoot 下灰度 Image 透明度 0↔0.55 淡入淡出（1s），
    ///    raycastTarget=false 不挡交互（URP 2D 无 Volume 后处理，本次已确证，禁引）；
    /// ② 低落气泡：玩家头侧的占位色块 + "好累……"，素材后期换。
    ///    挂玩家子物体自动跟随，特写隐藏玩家时一起隐藏；
    /// ③ 移速 -20% 不在这：由移动脚本经 Services.Stats.IsLowMood 自查（文档约定 0.8）。
    ///
    /// 订阅 Services.Stats.LowMoodChanged（惰性）；IResettable 重开清表现态。
    /// 玩家/宿主引用一律由装配菜单注入，禁止运行时查找（规范明文）。
    /// </summary>
    public class MoodFeedback : MonoBehaviour, IResettable
    {
        [Header("引用（装配注入，禁止查找）")]
        [Tooltip("去饱和全屏 Image 的宿主（UIRoot 下居中零尺寸 RectTransform）")]
        public RectTransform overlayHost;

        [Tooltip("气泡挂点（Player 的 Transform，子物体自动跟随）")]
        public Transform bubbleAnchor;

        [Header("参数")]
        [Tooltip("淡入/淡出时长（秒）")]
        public float fadeSeconds = 1f;

        [Tooltip("去饱和峰值透明度")]
        public float maxAlpha = 0.55f;

        [Tooltip("气泡文案（占位）")]
        public string bubbleText = "好累……";

        /// <summary>气泡压在所有场景精灵（150xx）之上、UI 之下。</summary>
        const int OrderBubble = 15110;

        Image _overlay;
        GameObject _bubble;
        float _alpha;            // 当前透明度 0~maxAlpha
        bool _low;               // 当前低落态
        bool _built;
        PlayerStats _subscribedStats;

        void Start() => Build();

        void Build()
        {
            if (_built) return;
            _built = true;

            // ① 全屏灰度 Image（alpha 0 起步，淡入由 Update 驱动）
            if (overlayHost != null)
            {
                _overlay = UIBuild.Image(overlayHost, "Desaturate",
                    new Color(0.42f, 0.42f, 0.48f, 0f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-960f, -540f), new Vector2(960f, 540f));
                _overlay.raycastTarget = false; // 禁止挡交互（规范明文）
            }

            // ② 气泡：玩家头侧占位色块 + 文字
            if (bubbleAnchor != null)
            {
                var old = bubbleAnchor.Find("MoodBubble");
                if (old != null) Object.Destroy(old.gameObject); // 防装配/重跑残留双份

                var go = new GameObject("MoodBubble");
                go.transform.SetParent(bubbleAnchor, false);
                go.transform.localPosition = new Vector3(0.9f, 0.75f, 0f);
                go.SetActive(false);

                var sr = go.AddComponent<SpriteRenderer>();
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var white = new Color32(255, 255, 255, 255);
                tex.SetPixels32(new[] { white, white, white, white });
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
                sr.color = new Color(1f, 0.97f, 0.88f); // 淡黄便签色
                sr.sortingOrder = OrderBubble;
                go.transform.localScale = new Vector3(1.3f, 0.62f, 1f);

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(go.transform, false);
                textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                var tm = textGo.AddComponent<TextMesh>();
                tm.text = bubbleText;
                tm.font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 48);
                tm.fontSize = 48;
                tm.characterSize = 0.065f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = new Color(0.30f, 0.28f, 0.32f);
                var mr = textGo.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = OrderBubble + 1;

                _bubble = go;
            }
        }

        void Update()
        {
            Build();
            SubscribeStats();

            // 淡入淡出：alpha 向目标值匀速推进（fadeSeconds 内走完 0↔maxAlpha）
            float target = _low ? maxAlpha : 0f;
            if (!Mathf.Approximately(_alpha, target))
            {
                float step = (maxAlpha / Mathf.Max(0.01f, fadeSeconds)) * Time.deltaTime;
                _alpha = Mathf.MoveTowards(_alpha, target, step);
                if (_overlay != null)
                {
                    var c = _overlay.color;
                    _overlay.color = new Color(c.r, c.g, c.b, _alpha);
                }
            }
        }

        void SubscribeStats()
        {
            if (_subscribedStats != null) return;
            var stats = Services.Stats as PlayerStats;
            if (stats == null) return;
            stats.LowMoodChanged -= OnLowMoodChanged;
            stats.LowMoodChanged += OnLowMoodChanged;
            _subscribedStats = stats;

            // 订阅成功后同步一次当前态（错过边缘也不怕开局已低落）
            ApplyLow(stats.IsLowMood);
        }

        void OnLowMoodChanged(bool low) => ApplyLow(low);

        void ApplyLow(bool low)
        {
            _low = low;
            if (_bubble != null && _bubble.activeSelf != low)
                _bubble.SetActive(low);
        }

        void OnDestroy()
        {
            if (_subscribedStats != null)
            {
                _subscribedStats.LowMoodChanged -= OnLowMoodChanged;
                _subscribedStats = null;
            }
        }

        /// <summary>R 重开：去饱和淡出归零、气泡收起。</summary>
        public void ResetForNewRun()
        {
            _low = false;
            _alpha = 0f;
            if (_overlay != null)
            {
                var c = _overlay.color;
                _overlay.color = new Color(c.r, c.g, c.b, 0f);
            }
            if (_bubble != null) _bubble.SetActive(false);
        }
    }
}
