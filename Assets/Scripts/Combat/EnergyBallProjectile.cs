using UnityEngine;

/// <summary>
/// 能量弹投射物 - 远程技能投射物逻辑
/// 运行时创建，向前飞行，碰撞敌人造成伤害
/// WebGL平台兼容
/// </summary>
public class EnergyBallProjectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float damageMultiplier;
    private GameObject owner;
    private float lifetime = 5f;
    private float timer = 0f;

    /// <summary>
    /// 初始化投射物参数
    /// </summary>
    public void Initialize(Vector3 dir, float spd, float dmgMult, GameObject ownerObj)
    {
        direction = dir.normalized;
        speed = spd;
        damageMultiplier = dmgMult;
        owner = ownerObj;
    }

    private void Update()
    {
        // 飞行
        transform.Translate(direction * speed * Time.deltaTime, Space.World);

        // 生命周期计时
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 跳过自己
        if (other.gameObject == owner) return;

        // 命中敌人
        CombatSystem targetCombat = other.GetComponent<CombatSystem>();
        if (targetCombat != null && !targetCombat.IsPlayer)
        {
            float baseDamage = 10f;
            float finalDamage = baseDamage * damageMultiplier;
            targetCombat.TakeDamage(owner, finalDamage, other.transform.position);
            Debug.Log("[EnergyBall] 命中: " + other.name + " 伤害: " + finalDamage);
        }

        // 销毁投射物
        Destroy(gameObject);
    }
}
