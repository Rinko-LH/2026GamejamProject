using RoomDemo;
using TMPro;
using UnityEngine;

namespace RoomDemo.UI
{
    /// <summary>
    /// 时钟显示（S1，策划案 1.6）：读 DayTimer，显示游戏内时钟与剩余秒数。
    /// timer 由装配菜单接线；为空时退化到场景查找。
    /// </summary>
    public class ClockDisplay : MonoBehaviour
    {
        public DayTimer timer;
        TMP_Text _label;

        void Start()
        {
            Build();
            if (timer == null) timer = FindObjectOfType<DayTimer>();
        }

        void Build()
        {
            if (_label != null) return;
            _label = transform.Find("Label")?.GetComponent<TMP_Text>();
            if (_label == null)
                Debug.LogError("[时钟] 缺少场景 UI 子物体 Label（需要 TextMeshProUGUI）。", this);
        }

        void Update()
        {
            Build();
            if (_label == null || timer == null) return;

            // 结算态时钟让位：结算面板呈现最终局面，顶部再挂个
            // 「剩余 480s/0s」只有干扰（R 重开后显示值也不对）。
            var flow = RoomDemo.Contracts.Services.Flow;
            bool settle = flow != null && flow.State == RoomDemo.GameFlowState.Settlement;
            if (_label.gameObject.activeSelf == settle)
                _label.gameObject.SetActive(!settle);
            if (settle) return;

            _label.text = $"时钟 {timer.ClockText}　剩余 {Mathf.CeilToInt(timer.RemainingSeconds)}s";
        }
    }
}
