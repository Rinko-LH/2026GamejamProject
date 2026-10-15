using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 交互提示：主角走进交互范围时，让可交互物高亮 + 显示一个上浮的提示块。
    ///
    /// 现在美术资源改为手绘透视，本脚本<b>不再程序化生成任何图形</b>：
    /// markerSprite 必须由外部（场景里的手绘图或画板玩法装配）指定，
    /// 没指定就只做呼吸高亮、不生成提示块，绝不退回程序化方块。
    /// </summary>
    public class InteractionHint : MonoBehaviour
    {
        [Header("引用")]
        public Transform player;
        public float triggerRange = 1.6f;

        [Header("高亮")]
        public SpriteRenderer[] highlightTargets;
        public Color normalColor = Color.white;
        public Color highlightColor = new Color(1.25f, 1.15f, 0.7f, 1f);
        public float pulseSpeed = 3f;

        [Header("提示块")]
        public bool showMarker = true;
        [Tooltip("手绘提示图。留空则不生成提示块（仍保留呼吸高亮）。")]
        public Sprite markerSprite;
        public Color markerColor = new Color(1f, 0.92f, 0.55f, 1f);
        public float markerBobHeight = 0.12f;

        [Header("层级")]
        [Tooltip("提示块的绘制层，需大于场景中其他手绘图，否则会被家具挡住。")]
        public int markerSortingOrder = 15200;

        SpriteRenderer _markerSr;
        float _phase;

        void Awake()
        {
            // 只有明确给了手绘提示图才生成提示块。
            // 以前这里会退回程序化的 RoomShapes.Rect，那是"零美术资源占位期"的做法，
            // 现在美术走手绘透视，程序化方块不再参与渲染。
            if (showMarker && markerSprite != null)
            {
                var go = new GameObject("HintMarker");
                go.transform.SetParent(transform, false);
                // 提示块要压在所有家具之上，否则会被书桌挡住看不见
                go.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                go.transform.localScale = new Vector3(0.28f, 0.28f, 1f);
                _markerSr = go.AddComponent<SpriteRenderer>();
                _markerSr.sprite = markerSprite;
                _markerSr.color = markerColor;
                _markerSr.sortingOrder = markerSortingOrder;
                _markerSr.enabled = false;
            }

            if (highlightTargets == null || highlightTargets.Length == 0)
            {
                // 没指定就自动收集本物体下的所有渲染器（跳过提示块自己）
                highlightTargets = GetComponentsInChildren<SpriteRenderer>(true);
            }

            CacheNormalColors();
        }

        Color[] _normals;

        void CacheNormalColors()
        {
            if (highlightTargets == null) return;
            _normals = new Color[highlightTargets.Length];
            for (int i = 0; i < highlightTargets.Length; i++)
                _normals[i] = highlightTargets[i] != null ? highlightTargets[i].color : normalColor;
        }

        void Update()
        {
            bool inRange = PlayerInRange;

            if (_markerSr != null && _markerSr.enabled != inRange)
                _markerSr.enabled = inRange;

            if (!inRange)
            {
                RestoreColors();
                return;
            }

            // 呼吸式高亮，比常亮更容易注意到
            _phase += Time.deltaTime * pulseSpeed;
            float k = (Mathf.Sin(_phase) + 1f) * 0.5f;
            Color c = Color.Lerp(highlightColor, normalColor, k * 0.55f);
            ApplyColor(c);

            if (_markerSr != null)
            {
                var lp = _markerSr.transform.localPosition;
                lp.y = 0.9f + Mathf.Sin(_phase * 1.2f) * markerBobHeight;
                _markerSr.transform.localPosition = lp;
            }
        }

        public bool PlayerInRange
        {
            get
            {
                // 玩家被藏起来（比如进了画板特写）时，一切提示都不该出现
                if (player == null || !player.gameObject.activeInHierarchy) return false;
                return Mathf.Abs(player.position.x - transform.position.x) < triggerRange;
            }
        }

        void ApplyColor(Color c)
        {
            if (highlightTargets == null || _normals == null) return;
            for (int i = 0; i < highlightTargets.Length && i < _normals.Length; i++)
                if (highlightTargets[i] != null) highlightTargets[i].color = c;
        }

        void RestoreColors()
        {
            if (highlightTargets == null || _normals == null) return;
            for (int i = 0; i < highlightTargets.Length && i < _normals.Length; i++)
                if (highlightTargets[i] != null) highlightTargets[i].color = _normals[i];
        }
    }
}