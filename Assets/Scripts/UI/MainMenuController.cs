using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoomDemo.UI
{
    /// <summary>主菜单按钮回调：开始玩法、打开制作人员页、退出应用。</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] string gameSceneName = "MainScene";
        [SerializeField] GameObject creditsPanel;

        void Awake()
        {
            // 菜单文本已在场景中配置 TMP 字体资源；不要再添加旧版 UI.Text，
            // 因为 TMP_Text 同样是 Graphic，同一个 GameObject 不能同时拥有两者。
            if (creditsPanel != null) creditsPanel.SetActive(false);
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
