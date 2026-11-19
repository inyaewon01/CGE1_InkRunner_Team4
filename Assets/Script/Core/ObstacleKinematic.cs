using UnityEngine;

/// <summary>
/// obstacle_3 전용 Kinematic 장애물 컴포넌트.
/// Obstacle.cs의 충돌 판정 기능을 그대로 포함하며,
/// Rigidbody2D(Kinematic)를 이용해 좌우로 왕복 이동합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class ObstacleKinematic : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Obstacle 공통 설정 (Obstacle.cs 동일)
    // ─────────────────────────────────────────────
    [Header("장애물 설정")]
    [Tooltip("장애물 식별 이름 (예: obstacle_3)")]
    [SerializeField] private string obstacleName = "obstacle_3";

    [Tooltip("충돌 시 즉사 여부 (기본값: true)")]
    [SerializeField] private bool isInstantKill = true;

    // ─────────────────────────────────────────────
    //  Kinematic 좌우 이동 설정
    // ─────────────────────────────────────────────
    [Header("좌우 이동 설정")]
    [Tooltip("이동 속도 (단위/초)")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("좌우 이동 거리 (스폰 위치 기준 ±)")]
    [SerializeField] private float moveRange = 2f;

    [Tooltip("시작 이동 방향 (1 = 오른쪽, -1 = 왼쪽)")]
    [SerializeField] private float startDirection = 1f;

    // ─────────────────────────────────────────────
    //  프로퍼티
    // ─────────────────────────────────────────────
    public string ObstacleName => obstacleName;
    public bool IsInstantKill  => isInstantKill && isActiveAndEnabled;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private Rigidbody2D rb;
    private Collider2D  col2D;
    private float       originX;      // 스폰 시 X 위치 (왕복 중심)
    private float       direction;    // 현재 이동 방향 (+1 or -1)

    // ─────────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────────
    private void Awake()
    {
        col2D = GetComponent<Collider2D>();
        rb    = GetComponent<Rigidbody2D>();

        // Kinematic 설정 강제화
        rb.bodyType        = RigidbodyType2D.Kinematic;
        rb.interpolation   = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (string.IsNullOrEmpty(obstacleName))
            obstacleName = gameObject.name;

        originX   = transform.position.x;
        direction = Mathf.Sign(startDirection == 0f ? 1f : startDirection);
    }

    private void Reset()
    {
        obstacleName = gameObject.name;
    }

    private void FixedUpdate()
    {
        MoveHorizontal();
    }

    // ─────────────────────────────────────────────
    //  좌우 왕복 이동 (Kinematic MovePosition)
    // ─────────────────────────────────────────────
    private void MoveHorizontal()
    {
        float nextX = rb.position.x + direction * moveSpeed * Time.fixedDeltaTime;

        // 범위 초과 시 방향 반전 & 위치 클램핑
        if (nextX > originX + moveRange)
        {
            nextX     = originX + moveRange;
            direction = -1f;
        }
        else if (nextX < originX - moveRange)
        {
            nextX     = originX - moveRange;
            direction = 1f;
        }

        rb.MovePosition(new Vector2(nextX, rb.position.y));
    }

    // ─────────────────────────────────────────────
    //  충돌 처리 (Obstacle.cs 동일 로직)
    // ─────────────────────────────────────────────
    private void HandleCollision(GameObject targetObject)
    {
        if (!isActiveAndEnabled) return;

        // 1. InkWarriorCollision 컴포넌트 탐색
        if (targetObject.TryGetComponent<InkWarriorCollision>(out var warrior) ||
            (warrior = targetObject.GetComponentInParent<InkWarriorCollision>()) != null ||
            (warrior = targetObject.GetComponentInChildren<InkWarriorCollision>()) != null)
        {
            // Obstacle 대신 ObstacleKinematic을 넘기기 위해 임시 Obstacle 래퍼 없이
            // HitByObstacle이 Obstacle 타입을 요구하면 아래 주석 해제 후 사용:
            // warrior.HitByObstacle(GetComponent<Obstacle>());

            // InkWarriorCollision.HitByObstacle 시그니처가 MonoBehaviour / 인터페이스를
            // 받도록 리팩터링되기 전까지는 직접 처리
            ApplyPlayerStop(targetObject);
            TriggerGameFail();
            return;
        }

        // 2. InkWarriorCollision 없을 때 태그/이름으로 폴백
        if (targetObject.CompareTag("Player") || targetObject.name.Contains("Inkwarrior"))
        {
            ApplyPlayerStop(targetObject);
            TriggerGameFail();
        }
    }

    private void ApplyPlayerStop(GameObject targetObject)
    {
        if (targetObject.TryGetComponent<Rigidbody2D>(out var playerRb))
        {
            playerRb.linearVelocity  = Vector2.zero;
            playerRb.angularVelocity = 0f;
            playerRb.constraints     = RigidbodyConstraints2D.FreezeAll;
            playerRb.simulated       = false;
        }

        if (targetObject.TryGetComponent<Animator>(out var anim))
        {
            anim.speed = 0f;
        }
    }

    private void TriggerGameFail()
    {
        var controller = FindAnyObjectByType<InkRunner.BlackInkPrototype.BlackInkTestController>();
        if (controller != null)
        {
            controller.Fail($"장애물({obstacleName}) 충돌");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleCollision(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    // ─────────────────────────────────────────────
    //  에디터 Gizmo (이동 범위 시각화)
    // ─────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float cx = Application.isPlaying ? originX : transform.position.x;
        float cy = transform.position.y;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        // 이동 경로 선
        Gizmos.DrawLine(new Vector3(cx - moveRange, cy, 0f),
                        new Vector3(cx + moveRange, cy, 0f));
        // 좌우 끝점 표시
        Gizmos.DrawSphere(new Vector3(cx - moveRange, cy, 0f), 0.12f);
        Gizmos.DrawSphere(new Vector3(cx + moveRange, cy, 0f), 0.12f);
        // 중심점
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(new Vector3(cx, cy, 0f), 0.08f);
    }
#endif
}
