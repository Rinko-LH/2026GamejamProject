using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace RoomDemo.UI
{
    /// <summary>主菜单按钮回调：开始玩法、打开制作人员页、退出应用。</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] string gameSceneName = "MainScene";
        [SerializeField] GameObject creditsPanel;
        [SerializeField] Font menuFont;

        void Awake()
        {
            // Convert these menu labels to legacy Text so the imported project font
            // can render Chinese without depending on a machine-installed font.
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            foreach (var tmp in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null) continue;
                var label = tmp.GetComponent<Text>();
                if (label == null) label = tmp.gameObject.AddComponent<Text>();
                label.font = menuFont != null ? menuFont : GetMenuFont();
                label.text = tmp.text;
                label.fontSize = Mathf.Max(1, Mathf.RoundToInt(tmp.fontSize));
                label.color = tmp.color;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.raycastTarget = false;
                tmp.enabled = false;
            }
            if (creditsPanel != null) creditsPanel.SetActive(false);
        }

        static Font GetMenuFont()
        {
            string[] names = { "Arial", "Segoe UI", "Microsoft YaHei", "Liberation Sans" };
            foreach (var name in names)
            {
                try
                {
                    var fallback = Font.CreateDynamicFontFromOSFont(name, 32);
                    if (fallback != null) return fallback;
                }
                catch (System.Exception) { /* Try the next installed system font. */ }
            }
            return null;
        }

        public void StartGame()
        {
            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError($"[主菜单] 场景未加入 Build Settings 或名称无效：{gameSceneName}");
                return;
            }
            SceneManager.LoadScene(gameSceneName);
        }

        public void ShowCredits()
        {
            if (creditsPanel != null) creditsPanel.SetActive(true);
        }

        public void HideCredits()
        {
            if (creditsPanel != null) creditsPanel.SetActive(false);
        }

        public void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            Debug.Log("[主菜单] 退出游戏按钮已触发（Editor 中不会关闭 Unity）。");
#endif
        }
    }
}
