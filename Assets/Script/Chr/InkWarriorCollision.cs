using UnityEngine;

public class InkWarriorCollision : MonoBehaviour
{
    private bool isDead = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (other.CompareTag("Obstacle"))
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        var animationDriver = GetComponent<InkRunnerAnimation>();
        if (animationDriver != null)
            animationDriver.TriggerCollision();

        Debug.Log("잉크 전사 사망!");

        // 이후 사망 애니메이션과 게임 종료 처리 연결
    }
}
