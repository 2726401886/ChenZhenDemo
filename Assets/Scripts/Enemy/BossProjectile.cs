using UnityEngine;

/// <summary>
/// Boss弹幕投射物 - Boss远程攻击弹幕逻辑
/// 运行时动态创建，向前飞行，碰撞玩家造成伤害
/// WebGL平台兼容
/// </summary>
public class BossProjectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float damage;
    private GameObject owner;
    private float lifetime = 4f;
    private float timer = 0f;

    /// <summary>
    /// 初始化弹幕参数
    /// </summary>
    public void Initialize(Vector3 dir, float spd, float dmg, GameObject ownerObj)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        owner = ownerObj;
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);

        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == owner) return;

        CombatSystem targetCombat = other.GetComponent<CombatSystem>();
        if (targetCombat != null && targetCombat.IsPlayer)
        {
            targetCombat.TakeDamage(owner, damage, other.transform.position);
            Debug.Log("[BossProjectile] 命中: " + other.name + " 伤害: " + damage);
        }

        Destroy(gameObject);
    }
}
