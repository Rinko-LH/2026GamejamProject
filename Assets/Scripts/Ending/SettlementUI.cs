using RoomDemo.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoomDemo.Ending
{
    /// <summary>
    /// 结算界面（S10，策划案 5.3 / 6.2）：进 Settlement 弹出的全屏面板。
    /// 内容 = 结局标题 + 收尾文案（S9 产出，同域直连）+ 本局统计
    /// （画作 n/3、专注占比、支线分项、猫捣乱、偶然发现、状态终值、用时）。
    /// UI 在场景层级中制作，脚本只填充本局数据、处理按钮与控制面板显隐。
    /// 默认整面板隐藏，只认 Services.Flow.StateChanged 边缘显隐。
    /// </summary>
    public class SettlementUI : MonoBehaviour, IResettable
    {
        GameObject _root;
        TMP_Text _title, _line, _stats;
        Button _mainMenuButton;
        bool _built;
        bool _subscribedFlow;
        bool _subscribedJudge;
        EndingJudge _judge;

        void Start() => Build();

        // ---------- 构建 ----------

        void Build()
        {
            if (_built) return;
            _built = true;

            var root = transform.Find("Root");
            _root = root != null ? root.gameObject : null;
            if (_root == null)
            {
                Debug.LogError("[结算界面] 缺少场景 UI 子物体 Root。", this);
                return;
            }
            _title = root.Find("Title")?.GetComponent<TMP_Text>();
            _line = root.Find("Line")?.GetComponent<TMP_Text>();
            _stats = root.Find("Stats")?.GetComponent<TMP_Text>();
            var buttonTransform = root.Find("MainMenuButton");
            _mainMenuButton = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
            if (_title == null || _line == null || _stats == null || _mainMenuButton == null)
            {
                Debug.LogError("[结算界面] Root 下需要 Title、Line、Stats 和 MainMenuButton 场景 UI 子物体。", this);
                return;
            }
            _mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            _mainMenuButton.onClick.AddListener(ReturnToMainMenu);

            _root.SetActive(false);
        }

        // ---------- 订阅（惰性，与 TaskHints 同一套时序兜底） ----------

        void Update()
        {
            Build();
            SubscribeFlow();
            SubscribeJudge();
        }

        /// <summary>
        /// 订阅 S9 的 Judged：StateChanged 的订阅顺序不定，面板可能比判定先跑
        /// （实测 Fill 时 Current 还是 null，标题回落成「结算」）——结局就绪后再刷一遍。
        /// </summary>
        void SubscribeJudge()
        {
            if (_subscribedJudge) return;
            var judge = EndingJudge.Instance;
            if (judge == null) return;
            judge.Judged -= OnJudged;
            judge.Judged += OnJudged;
            _judge = judge;
            _subscribedJudge = true;
        }

        void SubscribeFlow()
        {
            if (_subscribedFlow) return;
            var flow = Services.Flow;
            if (flow == null) return;
            flow.StateChanged -= OnStateChanged;
            flow.StateChanged += OnStateChanged;
            _subscribedFlow = true;
        }

        void OnDestroy()
        {
            if (Services.Flow != null) Services.Flow.StateChanged -= OnStateChanged;
            if (_judge != null) _judge.Judged -= OnJudged;
            _subscribedFlow = false;
            _subscribedJudge = false;
        }

        // ---------- 显隐与填数 ----------

        void OnStateChanged(GameFlowState oldState, GameFlowState newState)
        {
            if (newState == GameFlowState.Settlement)
            {
                Fill();
                if (_root != null) _root.SetActive(true);
            }
            else
            {
                if (_root != null) _root.SetActive(false);
            }
        }

        void OnJudged(EndingResult result)
        {
            var flow = Services.Flow;
            if (flow == null || flow.State != GameFlowState.Settlement) return;
            Fill(); // 结局判定晚于面板填充时补上标题/文案
        }

        void Fill()
        {
            Build();
            var flow = Services.Flow;
            var stats = Services.Stats;
            var progress = Services.Progress;

            var judge = EndingJudge.Instance;
            var result = judge != null ? judge.Current : null;
            _title.text = result != null ? result.Title : "结算";
            _line.text = result != null ? result.Line : "";

            int total = flow != null ? flow.TotalPaintings : 3;
            int done = flow != null ? flow.PaintingsCompleted : 0;
            float elapsed = flow != null ? flow.DayElapsedSeconds : 0f;
            float paint = flow != null ? flow.PaintingSeconds : 0f;
            float ratio = elapsed > 0.01f ? Mathf.Clamp01(paint / elapsed) : 0f;
            int mins = Mathf.FloorToInt(elapsed / 60f);
            int secs = Mathf.FloorToInt(elapsed - mins * 60f);

            int breakfast = progress != null ? progress.Count(QuestId.Breakfast) : 0;
            int tidyDesk = progress != null ? progress.Count(QuestId.TidyDesk) : 0;
            int tidyShelf = progress != null ? progress.Count(QuestId.TidyShelf) : 0;
            int water = progress != null ? progress.Count(QuestId.Water) : 0;
            int mischief = progress != null ? progress.Count(QuestId.CatMischief) : 0;
            int finds = progress != null ? progress.Count(QuestId.RandomFind) : 0;

            _stats.text =
                $"完成画作：{done} / {total}\n" +
                $"画画专注占比：{ratio:P0}（下笔 {paint:F0} 秒 ／ 全天 {elapsed:F0} 秒）\n" +
                $"支线：早餐 {breakfast} · 收拾桌面 {tidyDesk} · 接水 {water} · 整理书架 {tidyShelf}\n" +
                $"被猫弄乱 {mischief} 次 · 偶然发现 {finds} 次\n" +
                $"状态终值：水分 {stats?.WaterValue:F0} ／ 饱食 {stats?.SatietyValue:F0} ／ 精神 {stats?.SpiritValue:F0}\n" +
                $"用时：{mins} 分 {secs:D2} 秒";
        }

        void ReturnToMainMenu()
        {
            const string mainMenuScene = "MainMenu";
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogError($"[结算] 主菜单场景未加入 Build Settings 或名称无效：{mainMenuScene}");
                return;
            }
            SceneManager.LoadScene(mainMenuScene);
        }

        /// <summary>新一局开始时隐藏结算面板。</summary>
        public void ResetForNewRun()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
