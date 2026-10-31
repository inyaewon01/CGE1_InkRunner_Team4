
using System.Collections;
using UnityEngine;



public class SceneManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static SceneManager Instance { get; private set; }

    // 이름 정의
    private const string TITLE_SCENE = "TitleScene";
    private const string PLAY_SCENE = "BlackInkTestScene";
    private const string SCORE_SCENE = "ScoreScene";

    // 현재 씬을 로딩하고 있는지 확인
    public bool IsLoading { get; private set; }


    // 싱글톤 초기화
    private void Awake()
    {
        // 이미 SceneManager가 존재하고
        // 현재 오브젝트가 기존 인스턴스가 아니라면
        if (Instance != null && Instance != this)
        {
            // 중복 생성된 오브젝트 제거
            Destroy(gameObject);
            return;
        }

        // 현재 인스턴스 등록
        Instance = this;

        // 씬 전환 시에도 오브젝트 유지
        DontDestroyOnLoad(gameObject);
    }


    // ==========================================
    // 씬 전환 함수
    // ==========================================

    // 타이틀 씬으로 이동
    public void LoadTitle()
    {
        LoadScene(TITLE_SCENE);
    }

    // 플레이 씬으로 이동
    public void LoadPlay()
    {
        LoadScene(PLAY_SCENE);
    }

    // 스코어 씬으로 이동
    public void LoadScore()
    {
        LoadScene(SCORE_SCENE);
    }


    // ==========================================
    // 씬 로딩 요청
    // ==========================================

    private void LoadScene(string sceneName)
    {
        // 중복 로딩 방지
        if (IsLoading)
            return;

        // 비동기 씬 로딩 코루틴 실행
        StartCoroutine(LoadSceneRoutine(sceneName));
    }


    // ==========================================
    // 비동기 씬 로딩
    // ==========================================

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        // 로딩 상태 활성화
        IsLoading = true;

        // Unity 기본 SceneManager 호출
        // 사용자 정의 SceneManager와 이름이 같으므로
        // 전체 네임스페이스를 명시한다.
        AsyncOperation operation =
            UnityEngine.SceneManagement.SceneManager
                .LoadSceneAsync(sceneName);

        // 로딩 작업 생성 실패 시 종료
        if (operation == null)
        {
            IsLoading = false;
            yield break;
        }

        // 씬 로딩이 완료될 때까지 대기
        while (!operation.isDone)
        {
            // 다음 프레임까지 대기
            yield return null;
        }

        // 로딩 완료
        IsLoading = false;
    }
}
