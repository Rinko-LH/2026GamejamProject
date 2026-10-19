using RoomDemo.Contracts;
using TMPro;
using UnityEngine;

namespace RoomDemo.UI
{
    /// <summary>
    /// 状态栏（S1，策划案 1.6）：订阅 Services.Stats，每帧刷新水分/饱食/精神。
    /// 任一低落时整条变橙红提示。占位期用系统字体，美术到位可换 prefab。
    /// </summary>
    public class StatusBar : MonoBehaviour
    {
        TMP_Text _label;

        void Start() => Build();

        void Build()
        {
            if (_label != null) return;
            _label = transform.Find("Label")?.GetComponent<TMP_Text>();
            if (_label == null)
                Debug.LogError("[状态栏] 缺少场景 UI 子物体 Label（需要 TextMeshProUGUI）。", this);
        }

        void Update()
        {
            Build();
            if (_label == null) return;
            var s = Services.Stats;
            if (s == null) { _label.text = "状态：加载中…"; return; }

            _label.text = $"水分 {s.WaterValue:F0}　饱食 {s.SatietyValue:F0}　精神 {s.SpiritValue:F0}";
            _label.color = s.IsLowMood ? new Color(1f, 0.55f, 0.45f) : Color.white;
        }
    }
}
