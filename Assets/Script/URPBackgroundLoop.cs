using UnityEngine;

/// <summary>
/// 캐릭터의 이동에 맞춰 배경을 자연스럽게 스크롤(패럴랙스)하고,
/// 화면 밖으로 벗어난 배경 타일을 반대편 끝으로 재배치하여
/// 끊김 없는(심리스, Seamless) 무한 루프를 구현하는 스크립트입니다.
/// background1.png 등의 배경 스프라이트에 적용하여 사용합니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class URPBackgroundLoop : MonoBehaviour
{
    [Header("추적 대상 (Target Tracking)")]
    [Tooltip("이동을 추적할 캐릭터의 Transform입니다. 미할당 시 'Player' 태그 또는 씬 내 캐릭터를 자동 탐색합니다.")]
    [SerializeField] private Transform character;

    [Tooltip("참조할 카메라입니다. 미할당 시 Camera.main을 자동으로 사용합니다.")]
    [SerializeField] private Camera targetCamera;

    [Header("스크롤 및 원근감 설정 (Parallax & Scroll)")]
    [Tooltip("캐릭터 이동에 따른 배경 이동 비율 (0: 카메라에 완전 고정, 0.5: 절반 속도의 원경, 1: 전경과 동일 속도).")]
    [Range(0f, 1f)]
    [SerializeField] private float parallaxFactor = 0.5f;

    [Tooltip("캐릭터를 찾을 수 없거나 고정 속도로 자동 스크롤할 때 사용할 속도(기존 호환용).")]
    [SerializeField] private float scrollSpeed = 0f;

    [Header("심리스 루프 설정 (Seamless Loop)")]
    [Tooltip("스프라이트 렌더러의 bounds 크기를 기반으로 배경 너비를 자동 계산합니다 (권장).")]
    [SerializeField] private bool autoCalculateWidth = true;

    [Tooltip("배경 1장의 가로 실제 길이(월드 단위)입니다. autoCalculateWidth가 켜져 있으면 자동 갱신됩니다.")]
    [SerializeField] private float backgroundWidth = 0f;

    [Tooltip("동일 레이어에서 연속으로 루프되는 배경 오브젝트의 총 개수입니다.")]
    [SerializeField] private int backgroundCount = 2;

    [Tooltip("게임 시작 시 인접 배경 타일들을 정확한 너비 간격으로 자동 정렬하여 틈새(Seam)를 방지합니다.")]
    [SerializeField] private bool autoAlignAtStart = true;

    [Tooltip("화면 밖 판정 시 적용할 여유 버퍼(월드 단위)입니다.")]
    [SerializeField] private float offscreenBuffer = 1f;

    private SpriteRenderer spriteRenderer;
    private Vector3 lastCharacterPosition;
    private bool hasCharacter;

    // 씬 내 중복 정렬 방지를 위한 정적 플래그
    private static int lastAlignedFrame = -1;

    public float BackgroundWidth => backgroundWidth;
    public float ParallaxFactor
    {
        get => parallaxFactor;
        set => parallaxFactor = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        InitializeReferences();
        CalculateBackgroundWidth();

        if (autoAlignAtStart && Time.frameCount != lastAlignedFrame)
        {
            lastAlignedFrame = Time.frameCount;
            AutoAlignSiblings();
        }

        if (character != null)
        {
            lastCharacterPosition = character.position;
            hasCharacter = true;
        }
    }

    private void LateUpdate()
    {
        EnsureReferences();

        // 1. 캐릭터 이동량에 따른 패럴랙스 이동
        MoveWithCharacter();

        // 2. 카메라 시야를 벗어난 배경 조각을 재배치하여 심리스 무한 루프 구현
        CheckAndReposition();
    }

    /// <summary>
    /// 캐릭터 및 카메라 레퍼런스 자동 탐색
    /// </summary>
    private void InitializeReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindObjectOfType<Camera>();
            }
        }

        if (character == null)
        {
            // 1. Tag 'Player'로 탐색
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                character = playerObj.transform;
            }
            else
            {
                // 2. BlackInkTestController 내 character 필드 탐색
                var controller = FindObjectOfType<InkRunner.BlackInkPrototype.BlackInkTestController>();
                if (controller != null && controller.character != null)
                {
                    character = controller.character.transform;
                }
                else
                {
                    // 3. 오브젝트 이름으로 탐색
                    var warrior = GameObject.Find("Inkwarrior");
                    if (warrior != null)
                    {
                        character = warrior.transform;
                    }
                }
            }
        }
    }

    private void EnsureReferences()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (!hasCharacter && character != null)
        {
            lastCharacterPosition = character.position;
            hasCharacter = true;
        }
    }

    /// <summary>
    /// SpriteRenderer bounds를 기반으로 배경 1장의 월드 가로 너비를 정확하게 계산합니다.
    /// </summary>
    private void CalculateBackgroundWidth()
    {
        if (autoCalculateWidth && spriteRenderer != null && spriteRenderer.sprite != null)
        {
            backgroundWidth = spriteRenderer.bounds.size.x;
        }

        // 스프라이트가 없거나 너비가 0 이하인 경우 안전 fallback
        if (backgroundWidth <= 0f)
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                backgroundWidth = spriteRenderer.bounds.size.x;
            }
            if (backgroundWidth <= 0f)
            {
                backgroundWidth = 19.2f;
            }
        }
    }

    /// <summary>
    /// 씬 내의 URPBackgroundLoop 컴포넌트들을 감지하여 정확한 간격으로 배치하고,
    /// 카메라 시야가 처음부터 빈틈없이 채워지도록 자동 정렬합니다.
    /// </summary>
    private void AutoAlignSiblings()
    {
        var allLoops = FindObjectsByType<URPBackgroundLoop>(FindObjectsSortMode.None);
        if (allLoops == null || allLoops.Length == 0) return;

        // X 좌표 순으로 오름차순 정렬
        System.Array.Sort(allLoops, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        int count = allLoops.Length;

        for (int i = 0; i < count; i++)
        {
            if (allLoops[i].spriteRenderer == null)
            {
                allLoops[i].spriteRenderer = allLoops[i].GetComponent<SpriteRenderer>();
            }
            allLoops[i].backgroundCount = count;
            allLoops[i].backgroundWidth = backgroundWidth;
        }

        if (targetCamera != null && count > 0 && backgroundWidth > 0.001f)
        {
            float camHeight = 2f * targetCamera.orthographicSize;
            float camWidth = camHeight * targetCamera.aspect;
            float camLeftEdge = targetCamera.transform.position.x - camWidth * 0.5f;

            // 정렬 기준점: 첫 번째 배경의 bounds.min.x가 카메라 왼쪽 시야 바깥에 걸치도록 정렬
            float startX = allLoops[0].transform.position.x;
            float offsetFromOrigin = allLoops[0].spriteRenderer != null ?
                (allLoops[0].transform.position.x - allLoops[0].spriteRenderer.bounds.min.x) : 0f;

            int safety = 0;
            // 카메라 왼쪽 시야보다 첫 번째 배경 시작점이 오른쪽에 있다면 왼쪽으로 이동
            while ((startX - offsetFromOrigin) > camLeftEdge && safety++ < 20)
            {
                startX -= backgroundWidth;
            }

            safety = 0;
            // 첫 번째 배경이 이미 카메라 왼쪽 화면 밖으로 너무 멀리 가 있다면 오른쪽으로 이동
            while ((startX - offsetFromOrigin) + backgroundWidth < camLeftEdge && safety++ < 20)
            {
                startX += backgroundWidth;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = allLoops[i].transform.position;
                pos.x = startX + i * backgroundWidth;
                allLoops[i].transform.position = pos;
            }
        }
    }

    /// <summary>
    /// 캐릭터의 X축 이동에 맞춰 배경을 이동시킵니다.
    /// 카메라가 캐릭터를 따라가는 러너 환경에서 parallaxFactor에 따라 깊이감(원근감)을 형성합니다.
    /// </summary>
    private void MoveWithCharacter()
    {
        if (character != null)
        {
            float deltaX = character.position.x - lastCharacterPosition.x;
            lastCharacterPosition = character.position;

            // 캐릭터가 사망 후 리셋/리스폰 등으로 순간이동(Teleport)한 경우
            if (Mathf.Abs(deltaX) > backgroundWidth * 0.5f)
            {
                RepositionAroundCamera();
                return;
            }

            if (Mathf.Abs(deltaX) > 0.00001f)
            {
                // 카메라가 캐릭터와 함께 이동할 때:
                // parallaxFactor = 1.0 -> 배경은 월드에 정지 (카메라 시점에선 전경 속도로 스크롤)
                // parallaxFactor = 0.5 -> 배경이 캐릭터 이동의 50%만큼 따라감 (카메라 시점에선 절반 속도로 스크롤)
                // parallaxFactor = 0.0 -> 배경이 캐릭터 이동의 100%만큼 따라감 (카메라 시점에선 완전 정지)
                float moveAmount = deltaX * (1f - parallaxFactor);
                transform.position += new Vector3(moveAmount, 0f, 0f);
            }
        }
        else if (scrollSpeed != 0f)
        {
            // 캐릭터가 없는 경우 기존 호환용 단순 스크롤
            transform.position += Vector3.left * (scrollSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 배경 조각이 카메라 시야 밖으로 완전히 벗어났는지 확인하고,
    /// 반대편 끝으로 이동시켜 빈틈없는 루프를 유지합니다.
    /// </summary>
    private void CheckAndReposition()
    {
        if (targetCamera == null) return;

        float camHeight = 2f * targetCamera.orthographicSize;
        float camWidth = camHeight * targetCamera.aspect;
        float camLeftEdge = targetCamera.transform.position.x - camWidth * 0.5f;
        float camRightEdge = targetCamera.transform.position.x + camWidth * 0.5f;

        float totalLoopWidth = backgroundWidth * backgroundCount;
        float myLeft = spriteRenderer != null ? spriteRenderer.bounds.min.x : transform.position.x;
        float myRight = spriteRenderer != null ? spriteRenderer.bounds.max.x : (transform.position.x + backgroundWidth);

        // 1. 화면 왼쪽 밖으로 완전히 벗어난 경우 -> 오른쪽 끝으로 재배치
        int safetyCount = 0;
        while (myRight < (camLeftEdge - offscreenBuffer) && safetyCount++ < 5)
        {
            transform.position += new Vector3(totalLoopWidth, 0f, 0f);
            if (spriteRenderer != null)
            {
                myLeft = spriteRenderer.bounds.min.x;
                myRight = spriteRenderer.bounds.max.x;
            }
            else
            {
                myLeft += totalLoopWidth;
                myRight += totalLoopWidth;
            }
        }

        // 2. 화면 오른쪽 밖으로 완전히 벗어난 경우 (역방향 이동 시) -> 왼쪽 끝으로 재배치
        safetyCount = 0;
        while (myLeft > (camRightEdge + offscreenBuffer) && safetyCount++ < 5)
        {
            transform.position -= new Vector3(totalLoopWidth, 0f, 0f);
            if (spriteRenderer != null)
            {
                myLeft = spriteRenderer.bounds.min.x;
                myRight = spriteRenderer.bounds.max.x;
            }
            else
            {
                myLeft -= totalLoopWidth;
                myRight -= totalLoopWidth;
            }
        }
    }

    /// <summary>
    /// 캐릭터가 리스폰되거나 스테이지가 재시작될 때 배경을 현재 카메라 시야에 맞게 재정렬합니다.
    /// </summary>
    public void RepositionAroundCamera()
    {
        AutoAlignSiblings();
    }

    /// <summary>
    /// 추적할 캐릭터 Transform을 외부에서 지정할 수 있습니다.
    /// </summary>
    public void SetCharacter(Transform newCharacter)
    {
        character = newCharacter;
        if (character != null)
        {
            lastCharacterPosition = character.position;
            hasCharacter = true;
        }
    }
}
