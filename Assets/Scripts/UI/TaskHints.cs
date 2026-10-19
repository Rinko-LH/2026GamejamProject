using RoomDemo.Contracts;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RoomDemo.UI
{
    /// <summary>
    /// 任务提示（S1，策划案 1.6）：订阅 Services.Progress.QuestCompleted，
    /// 把已完成的支线列在左下角，给玩家"今天还差什么"的进展感。
    /// </summary>
    public class TaskHints : MonoBehaviour, IResettable
    {
        TMP_Text _label;
        readonly List<string> _done = new List<string>();
        bool _subscribed;

        void Start() => Build();

        void Build()
        {
            if (_label != null) return;
            _label = transform.Find("Label")?.GetComponent<TMP_Text>();
            if (_label == null)
            {
                Debug.LogError("[任务提示] 缺少场景 UI 子物体 Label（需要 TextMeshProUGUI）。", this);
                return;
            }
            _label.text = "支线进展：\n（暂无）";
        }

        void Update()
        {
            Build();
            if (_label == null) return;
            if (_subscribed) return;
            var p = Services.Progress;
            if (p == null) return;
            p.QuestCompleted -= OnQuest;
            p.QuestCompleted += OnQuest;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (Services.Progress != null) Services.Progress.QuestCompleted -= OnQuest;
            _subscribed = false;
        }

        void OnQuest(QuestCompletedEvent e)
        {
            var id = e.Id.ToString();
            // 同一支线只列一次（猫打乱一局可触发多次，面板去重显示）
            if (_done.Contains(id)) return;
            _done.Add(id);
            _label.text = "支线进展：\n· " + string.Join("\n· ", _done);
        }

        /// <summary>
        /// R 重开：清空支线列表（否则进度总线已清零，面板却继续追加旧条目——P3）。
        /// </summary>
        public void ResetForNewRun()
        {
            Build();
            _done.Clear();
            if (_label != null) _label.text = "支线进展：\n（暂无）";
        }
    }
}
