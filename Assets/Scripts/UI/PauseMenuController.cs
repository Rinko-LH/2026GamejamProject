using RoomDemo.Contracts;
using RoomDemo.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoomDemo.UI
{
    /// <summary>仅在普通探索时接管 Esc；对话和特写继续使用各自的取消逻辑。</summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button mainMenuButton;
        [SerializeField] string mainMenuSceneName = "MainMenu";

        InputManager _input;
        float _previousTimeScale = 1f;
        bool _previousAudioPause;
        GameObject _previousSelection;
        bool _leavingScene;

        public bool IsPaused { get; private set; }
        public bool CanPause => isActiveAndEnabled && !_leavingScene && panel != null &&
            Services.Flow != null && Services.Flow.State == GameFlowState.Exploring &&
            (Services.Dialogue == null || !Services.Dialogue.IsPlaying) &&
            !RoomInteractable.CloseupActive && _input != null && !_input.IsBlocked;

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        void OnEnable() => TrySubscribe();
        void Update() => TrySubscribe();

        void TrySubscribe()
        {
            if (_input != null || InputManager.Instance == null) return;
            _input = InputManager.Instance;
            _input.PauseRequested += HandlePauseRequest;
        }

        bool HandlePauseRequest()
        {
            if (IsPaused)
            {
                ResumeGame();
                return true;
            }
            if (!CanPause) return false;
            PauseGame();
            return true;
        }

        public void PauseGame()
        {
            TrySubscribe();
            if (IsPaused || !CanPause) return;
            _previousTimeScale = Time.timeScale;
            _previousAudioPause = AudioListener.pause;
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            IsPaused = true;
            _input.PushContext(InputManager.PauseContext);
            Time.timeScale = 0f;
            AudioListener.pause = true;
            panel.SetActive(true);
            // Selected 的优先级高于 Highlighted；不预选按钮，保证鼠标移入时使用悬停色。
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;
            IsPaused = false;
            if (_input != null) _input.PopContext(InputManager.PauseContext);
            Time.timeScale = _previousTimeScale;
            AudioListener.pause = _previousAudioPause;
            if (panel != null) panel.SetActive(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
        }

        public void ReturnToMainMenu()
        {
            if (!IsPaused || _leavingScene) return;
            if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                Debug.LogError($"[暂停菜单] 场景未加入 Build Settings 或名称无效：{mainMenuSceneName}", this);
                return;
            }
            _leavingScene = true;
            if (resumeButton != null) resumeButton.interactable = false;
            if (mainMenuButton != null) mainMenuButton.interactable = false;
            ResumeGame();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        void OnDisable()
        {
            ResumeGame();
            if (_input != null) _input.PauseRequested -= HandlePauseRequest;
            _input = null;
        }
    }
}
