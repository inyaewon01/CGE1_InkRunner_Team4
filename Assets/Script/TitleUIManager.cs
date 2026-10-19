using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 타이틀 씬(TitleScene)의 UI 흐름 및 씬 전환을 관리하는 매니저 클래스입니다.
/// - 단일 책임 원칙(SRP) 준수 및 방어적 프로그래밍 적용
/// - 중복 클릭(따닥) 방지 및 비동기 씬 로딩 구현
/// </summary>
public class TitleUIManager : MonoBehaviour
{
    #region Constants
    // 로드 대상 씬 이름 (Build Settings에 등록되어 있어야 함)
    private const string PLAY_SCENE_NAME = "PlayScene";

    // 조작 안내 기본 문구
    private const string HOW_TO_PLAY_GUIDE_TEXT = "마우스 좌클릭: 선택한 먹 사용 / 우클릭: 검은 먹&흰 먹 전환 / ESC: 일시정지";
    #endregion

    #region Serialized Fields
    [Header("=== UI Blocking & Canvas Group ===")]
    [Tooltip("씬 전환 중 전체 UI 입력 차단(중복 클릭 방지)을 위한 CanvasGroup")]
    [SerializeField] private CanvasGroup mainCanvasGroup;

    [Header("=== Main Menu Buttons ===")]
    [Tooltip("게임 시작 버튼")]
    [SerializeField] private Button startButton;

    [Tooltip("조작 안내 팝업 오픈 버튼")]
    [SerializeField] private Button howToPlayButton;

    [Tooltip("게임 종료 버튼")]
    [SerializeField] private Button quitButton;

    [Header("=== How To Play Popup ===")]
    [Tooltip("조작 안내 팝업 패널 게임오브젝트")]
    [SerializeField] private GameObject howToPlayPopup;

    [Tooltip("조작 안내 팝업 내 닫기 버튼")]
    [SerializeField] private Button popupCloseButton;

    [Tooltip("조작 안내 텍스트 컴포넌트 (TextMeshProUGUI)")]
    [SerializeField] private TextMeshProUGUI guideDescriptionText;

    [Tooltip("조작 안내 마우스 가이드 이미지")]
    [SerializeField] private Image mouseGuideImage;
    #endregion

    #region Private Fields
    // 씬 전환 중 중복 클릭 방지 플래그
    private bool isTransitioning = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // UI 버튼 이벤트 리스너 등록
        RegisterButtonEvents();
    }

    private void Start()
    {
        // 타이틀 씬 진입 시 초기 UI 상태 설정
        InitializeUI();
    }

    private void OnDestroy()
    {
        // 씬 언로드 또는 오브젝트 파괴 시 이벤트 리스너 해제 (메모리 누수 방지)
        UnregisterButtonEvents();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// 각 버튼의 onClick 이벤트에 핸들러를 바인딩합니다.
    /// </summary>
    private void RegisterButtonEvents()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnClickStartButton);

        if (howToPlayButton != null)
            howToPlayButton.onClick.AddListener(OnClickHowToPlayButton);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnClickQuitButton);

        if (popupCloseButton != null)
            popupCloseButton.onClick.AddListener(OnClickClosePopup);
    }

    /// <summary>
    /// 등록된 모든 버튼 이벤트를 안전하게 해제합니다.
    /// </summary>
    private void UnregisterButtonEvents()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnClickStartButton);

        if (howToPlayButton != null)
            howToPlayButton.onClick.RemoveListener(OnClickHowToPlayButton);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnClickQuitButton);

        if (popupCloseButton != null)
            popupCloseButton.onClick.RemoveListener(OnClickClosePopup);
    }

    /// <summary>
    /// 씬 시작 시 기본 UI 상태를 초기화합니다.
    /// 3개의 메인 버튼 활성화 및 팝업 패널 닫힘 상태를 보장합니다.
    /// </summary>
    private void InitializeUI()
    {
        isTransitioning = false;

        // 메인 캔버스 인터랙션 활성화
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.interactable = true;
            mainCanvasGroup.blocksRaycasts = true;
        }

        // 메인 메뉴 3개 버튼 활성화 보장
        SetMainButtonsInteractable(true);

        // 조작 안내 팝업은 시작 시 비활성화
        if (howToPlayPopup != null)
        {
            howToPlayPopup.SetActive(false);
        }

        // 조작 안내 텍스트 설정
        if (guideDescriptionText != null)
        {
            guideDescriptionText.text = HOW_TO_PLAY_GUIDE_TEXT;
        }
    }
    #endregion

    #region UI Event Handlers
    /// <summary>
    /// [게임 시작] 버튼 클릭 이벤트 처리
    /// - 중복 클릭(따닥) 방지 처리 후 PlayScene 비동기 로드 시작
    /// </summary>
    private void OnClickStartButton()
    {
        // 이미 전환 중이면 즉시 리턴
        if (isTransitioning) return;

        // 1. 중복 클릭 방지 처리 (CanvasGroup 블록 및 버튼 인터랙션 잠금)
        LockAllInteractions();

        // 2. PlayScene 비동기 로드 코루틴 실행
        StartCoroutine(LoadPlaySceneAsync());
    }

    /// <summary>
    /// [조작 안내] 버튼 클릭 이벤트 처리
    /// </summary>
    private void OnClickHowToPlayButton()
    {
        if (isTransitioning) return;

        OpenHowToPlayPopup();
    }

    /// <summary>
    /// [조작 안내 닫기] 버튼 클릭 이벤트 처리
    /// </summary>
    private void OnClickClosePopup()
    {
        if (isTransitioning) return;

        CloseHowToPlayPopup();
    }

    /// <summary>
    /// [게임 종료] 버튼 클릭 이벤트 처리
    /// </summary>
    private void OnClickQuitButton()
    {
        if (isTransitioning) return;

        QuitGame();
    }
    #endregion

    #region Popup Logic
    /// <summary>
    /// 조작 안내 팝업을 활성화하고 가이드 문구를 확인합니다.
    /// </summary>
    public void OpenHowToPlayPopup()
    {
        if (howToPlayPopup != null)
        {
            // 인스펙터나 런타임에서 텍스트가 비어있을 경우 기본 상수로 채움
            if (guideDescriptionText != null && string.IsNullOrEmpty(guideDescriptionText.text))
            {
                guideDescriptionText.text = HOW_TO_PLAY_GUIDE_TEXT;
            }

            howToPlayPopup.SetActive(true);
        }
    }

    /// <summary>
    /// 조작 안내 팝업을 비활성화합니다.
    /// </summary>
    public void CloseHowToPlayPopup()
    {
        if (howToPlayPopup != null)
        {
            howToPlayPopup.SetActive(false);
        }
    }
    #endregion

    #region Scene Loading & Interaction Locking
    /// <summary>
    /// 모든 UI 입력 및 레이캐스트를 즉시 차단하여 더블 클릭(따닥)을 원천 방지합니다.
    /// </summary>
    private void LockAllInteractions()
    {
        isTransitioning = true;

        // 1차 방어: CanvasGroup을 통한 전체 레이캐스트 및 인터랙션 차단
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.interactable = false;
            mainCanvasGroup.blocksRaycasts = false;
        }

        // 2차 방어: 개별 메인 버튼 비활성화
        SetMainButtonsInteractable(false);

        // 팝업 닫기 버튼도 함께 비활성화
        if (popupCloseButton != null)
        {
            popupCloseButton.interactable = false;
        }
    }

    /// <summary>
    /// 메인 메뉴 3개 버튼의 interactable 상태를 일괄 설정합니다.
    /// </summary>
    /// <param name="isInteractable">활성화 여부</param>
    private void SetMainButtonsInteractable(bool isInteractable)
    {
        if (startButton != null) startButton.interactable = isInteractable;
        if (howToPlayButton != null) howToPlayButton.interactable = isInteractable;
        if (quitButton != null) quitButton.interactable = isInteractable;
    }

    /// <summary>
    /// PlayScene을 비동기로 로드하는 코루틴입니다.
    /// </summary>
    private IEnumerator LoadPlaySceneAsync()
    {
        // 네임스페이스 모호성(Ambiguous reference)을 방지하기 위해 풀 네임스페이스 명시
        AsyncOperation asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(PLAY_SCENE_NAME);

        // Build Settings에 씬이 등록되지 않았거나 로드에 실패한 경우의 예외 처리
        if (asyncOperation == null)
        {
            Debug.LogError($"[TitleUIManager] '{PLAY_SCENE_NAME}' 씬을 로드할 수 없습니다. Build Settings에 해당 씬이 추가되어 있는지 확인하세요.");

            // 에러 발생 시 UI 잠금 해제하여 유저가 갇히지 않도록 복구
            isTransitioning = false;
            if (mainCanvasGroup != null)
            {
                mainCanvasGroup.interactable = true;
                mainCanvasGroup.blocksRaycasts = true;
            }
            SetMainButtonsInteractable(true);
            yield break;
        }

        // 씬 로딩 완료까지 대기 (필요 시 로딩 프로그레스 바나 페이드 연출 추가 가능)
        while (!asyncOperation.isDone)
        {
            // asyncOperation.progress는 0.0f ~ 0.9f까지 진행됨 (0.9f에서 활성화 준비 완료)
            yield return null;
        }
    }

    /// <summary>
    /// 게임 애플리케이션을 종료합니다.
    /// 에디터 환경에서는 플레이 모드를 해제하고, 빌드 환경에서는 프로세스를 종료합니다.
    /// </summary>
    private void QuitGame()
    {
#if UNITY_EDITOR
        // 유니티 에디터 실행 환경 대응
        EditorApplication.isPlaying = false;
#else
        // 빌드된 실행 파일 환경 대응
        Application.Quit();
#endif
    }
    #endregion
}
