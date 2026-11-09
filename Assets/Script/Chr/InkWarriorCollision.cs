using UnityEngine;

/// <summary>
/// Inkwarrior(플레이어)의 충돌 감지 및 사망/게임 종료 처리를 담당하는 스크립트입니다.
/// Obstacle 컴포넌트 또는 "Obstacle" 태그를 가진 오브젝트와 충돌 시 이동을 즉각 멈추고 게임을 종료합니다.
/// </summary>
public class InkWarriorCollision : MonoBehaviour
{
    [Header("상태")]
    [SerializeField] private bool isDead = false;

    [Header("게임 오버 설정")]
    [Tooltip("게임 오버 시 호출되는 유니티 이벤트")]
    public UnityEngine.Events.UnityEvent onGameOver;

    /// <summary>
    /// 플레이어의 사망 여부를 반환합니다.
    /// </summary>
    public bool IsDead => isDead;

    private Rigidbody2D rb;
    private Animator anim;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        CheckObstacleCollision(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        CheckObstacleCollision(collision.gameObject);
    }

    /// <summary>
    /// 충돌한 오브젝트가 장애물인지 판별하고 처리합니다.
    /// </summary>
    private void CheckObstacleCollision(GameObject target)
    {
        // 1. Obstacle 컴포넌트가 있는지 확인 (직접 또는 부모)
        if (target.TryGetComponent<Obstacle>(out var obstacle) ||
            (obstacle = target.GetComponentInParent<Obstacle>()) != null)
        {
            HitByObstacle(obstacle);
            return;
        }

        // 2. 태그로 장애물 판별 (태그 미정의 시 발생하는 예외 방지)
        try
        {
            if (target.CompareTag("Obstacle"))
            {
                HitByObstacle(null);
            }
        }
        catch (UnityException)
        {
            // "Obstacle" 태그가 프로젝트 TagManager에 등록되어 있지 않은 경우 안전하게 무시
        }
    }

    /// <summary>
    /// Obstacle 스크립트와의 충돌 시 호출됩니다.
    /// </summary>
    public void HitByObstacle(Obstacle obstacle)
    {
        if (isDead) return;

        if (obstacle != null && !obstacle.IsInstantKill)
        {
            // 즉사가 아닌 장애물일 경우 추후 체력 시스템 등과 연동 가능
            return;
        }

        string obstacleName = obstacle != null ? obstacle.ObstacleName : "장애물";
        Debug.Log($"[{obstacleName}] 충돌 - 잉크 전사 이동 정지 및 게임 종료!");

        Die($"장애물({obstacleName}) 충돌");
    }

    /// <summary>
    /// 플레이어 사망 및 게임 종료 처리
    /// </summary>
    public void Die(string reason = "장애물 충돌")
    {
        if (isDead) return;

        isDead = true;

        // 1. 물리 이동 즉각 완전 정지 (더 이상 앞으로 가지 못하도록 위치/회전 고정 및 시뮬레이션 중단)
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            rb.simulated = false;
        }

        // 2. 캐릭터 달리기 애니메이션 즉시 정지
        if (anim == null) anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.speed = 0f;
        }

        // 3. 씬의 BlackInkTestController가 있으면 게임 실패(GameOver) 상태로 전환하여 달리기/드로잉 중단
        var controller = FindAnyObjectByType<InkRunner.BlackInkPrototype.BlackInkTestController>();
        if (controller != null)
        {
            controller.Fail(reason);
        }

        // 4. 게임 오버 이벤트 호출
        onGameOver?.Invoke();

        Debug.Log("게임 오버 처리 완료: 잉크 전사가 정지하고 게임이 종료되었습니다.");
    }

    /// <summary>
    /// 플레이어 부활 또는 게임 재시작 시 상태 초기화
    /// </summary>
    public void ResetState()
    {
        isDead = false;

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.simulated = true;
        }

        if (anim == null) anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.speed = 1f;
        }
    }
}
