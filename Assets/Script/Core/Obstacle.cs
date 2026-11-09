using UnityEngine;

/// <summary>
/// obstacle_1을 포함하여 모든 장애물에 공통으로 부착하여 사용하는 충돌 판정 컴포넌트입니다.
/// 플레이어(Inkwarrior)와 충돌 시 플레이어의 이동을 즉시 멈추고 게임을 종료시키며, 장애물 자체는 파괴되지 않고 유지됩니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Obstacle : MonoBehaviour
{
    [Header("장애물 설정")]
    [Tooltip("장애물 식별 이름 (예: obstacle_1, obstacle_2 등)")]
    [SerializeField] private string obstacleName = "obstacle_1";

    [Tooltip("충돌 시 즉사 여부 (기본값: true)")]
    [SerializeField] private bool isInstantKill = true;

    /// <summary>
    /// 외부에서 장애물 이름을 읽을 수 있는 프로퍼티
    /// </summary>
    public string ObstacleName => obstacleName;

    /// <summary>
    /// 충돌 시 즉사 여부 프로퍼티
    /// </summary>
    public bool IsInstantKill => isInstantKill;

    private Collider2D col2D;

    private void Awake()
    {
        col2D = GetComponent<Collider2D>();
        if (string.IsNullOrEmpty(obstacleName))
        {
            obstacleName = gameObject.name;
        }
    }

    private void Reset()
    {
        // 인스펙터에 컴포넌트 추가 시 오브젝트 이름으로 기본 설정
        obstacleName = gameObject.name;
    }

    /// <summary>
    /// 플레이어(Inkwarrior)와의 충돌 처리
    /// </summary>
    private void HandleCollision(GameObject targetObject)
    {
        // 1. InkWarriorCollision 컴포넌트 직접 탐색
        if (targetObject.TryGetComponent<InkWarriorCollision>(out var warrior) ||
            (warrior = targetObject.GetComponentInParent<InkWarriorCollision>()) != null ||
            (warrior = targetObject.GetComponentInChildren<InkWarriorCollision>()) != null)
        {
            warrior.HitByObstacle(this);
            return;
        }

        // 2. 만약 InkWarriorCollision 컴포넌트가 없더라도 플레이어 오브젝트인 경우 안전하게 물리 정지 및 게임 종료
        if (targetObject.CompareTag("Player") || targetObject.name.Contains("Inkwarrior"))
        {
            if (targetObject.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
                rb.simulated = false;
            }

            if (targetObject.TryGetComponent<Animator>(out var anim))
            {
                anim.speed = 0f;
            }

            var controller = FindAnyObjectByType<InkRunner.BlackInkPrototype.BlackInkTestController>();
            if (controller != null)
            {
                controller.Fail($"장애물({obstacleName}) 충돌");
            }
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
}
