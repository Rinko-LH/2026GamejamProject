using RoomDemo.Contracts;
using RoomDemo.Input;
using RoomDemo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo.Ending
{
    /// <summary>
    /// 结算界面（S10，策划案 5.3 / 6.2）：进 Settlement 弹出的全屏面板。
    /// 内容 = 结局标题 + 收尾文案（S9 产出，同域直连）+ 本局统计
    /// （画作 n/3、专注占比、支线分项、猫捣乱、偶然发现、状态终值、用时）。
    ///
    /// R 重开二段式确认（D11）：首次按 R 只弹确认（空格 确认 / Esc 取消），
    /// 确认后才经 Services.Flow.ResetRun() 重开——GameManager 已摘除 R 直连
    /// （IGameFlow.ResetRun），R 只在这里生效，防误触。
    ///
    /// UI 全部 Start 里用 UIBuild 自建（占位期系统字体），默认整面板隐藏，
    /// 只认 Services.Flow.StateChanged 边缘显隐。
    /// </summary>
    public class SettlementUI : MonoBehaviour, IResettable
    {
        GameObject _root;
        Text _title, _line, _stats, _hint, _confirm;
        bool _built;
        bool _confirming;
        bool _subscribedFlow;
        bool _subscribedInput;
        bool _subscribedJudge;
        EndingJudge _judge;

        void Start() => Build();

        // ---------- 构建 ----------

        void Build()
        {
            if (_built) return;
            _built = true;

            // 宿主是 UIRoot 下居中的零尺寸 RectTransform（装配 EnsureUiHost）。
            // 全屏 Root 直接给 1920×1080（CanvasScaler 参考分辨率），居中即满屏。
            _root = new GameObject("Root");
            _root.transform.SetParent(transform, false);
            var rootRt = _root.AddComponent<RectTransform>();
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = new Vector2(1920f, 1080f);

            // 半透明压暗底
            UIBuild.Image(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.78f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-960f, -540f), new Vector2(960f, 540f));

            // 中央信息板 980×760
            var box = UIBuild.Image(_root.transform, "Box", new Color(0.10f, 0.12f, 0.18f, 0.97f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-490f, -380f), new Vector2(490f, 380f));
            var boxT = box.transform;

            _title = UIBuild.Text(boxT, "Title", 76, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 110f));
            _line = UIBuild.Text(boxT, "Line", 40, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.70f),
                new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(900f, 120f));
            _stats = UIBuild.Text(boxT, "Stats", 36, TextAnchor.MiddleLeft, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(880f, 360f));
            _hint = UIBuild.Text(boxT, "Hint", 36, TextAnchor.MiddleCenter, new Color(0.75f, 0.78f, 0.85f),
                new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(900f, 56f));
            _hint.text = "按 R 重开一局";

            _confirm = UIBuild.Text(boxT, "Confirm", 44, TextAnchor.MiddleCenter, new Color(1f, 0.90f, 0.40f),
                new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(900f, 120f));
            _confirm.text = "要重开一局吗？\n空格 确认 ／ Esc 取消";
            _confirm.gameObject.SetActive(false);

            _root.SetActive(false);
        }

        // ---------- 订阅（惰性，与 TaskHints 同一套时序兜底） ----------

        void Update()
        {
            Build();
            SubscribeFlow();
            SubscribeInput();
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

        void SubscribeInput()
        {
            if (_subscribedInput) return;
            var im = InputManager.Instance;
            if (im == null) return;
            im.DebugRestart -= OnDebugRestart; im.DebugRestart += OnDebugRestart;
            im.Confirm -= OnConfirm; im.Confirm += OnConfirm;
            im.CancelPressed -= OnCancel; im.CancelPressed += OnCancel;
            _subscribedInput = true;
        }

        void OnDestroy()
        {
            if (Services.Flow != null) Services.Flow.StateChanged -= OnStateChanged;
            if (_judge != null) _judge.Judged -= OnJudged;
            var im = InputManager.Instance;
            if (im != null)
            {
                im.DebugRestart -= OnDebugRestart;
                im.Confirm -= OnConfirm;
                im.CancelPressed -= OnCancel;
            }
            _subscribedFlow = false;
            _subscribedInput = false;
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
                ResetConfirm();
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

        // ---------- R 二段式确认（D11） ----------

        void OnDebugRestart()
        {
            var flow = Services.Flow;
            if (flow == null || flow.State != GameFlowState.Settlement) return;
            if (_confirming) return; // 确认态里再按 R 不重复响应

            _confirming = true;
            if (_hint != null) _hint.gameObject.SetActive(false);
            if (_confirm != null) _confirm.gameObject.SetActive(true);
        }

        void OnConfirm()
        {
            if (!_confirming) return;
            var flow = Services.Flow;
            ResetConfirm();
            if (flow == null || flow.State != GameFlowState.Settlement) return;
            flow.ResetRun();
        }

        void OnCancel()
        {
            if (!_confirming) return;
            ResetConfirm();
        }

        void ResetConfirm()
        {
            _confirming = false;
            if (_hint != null) _hint.gameObject.SetActive(true);
            if (_confirm != null) _confirm.gameObject.SetActive(false);
        }

        /// <summary>R 重开 / 新一局：收起确认态、整面板隐藏（StateChanged 也会兜一道）。</summary>
        public void ResetForNewRun()
        {
            ResetConfirm();
            if (_root != null) _root.SetActive(false);
        }
    }
}
