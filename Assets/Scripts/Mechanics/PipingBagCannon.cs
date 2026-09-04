using UnityEngine;

namespace Platformer.Mechanics
{
    public class PipingBagCannon : MonoBehaviour
    {
        ProceduralLevelManager levelManager;
        PlayerController player;
        Transform nozzlePivot;
        Transform muzzle;
        float fireInterval;
        float projectileSpeed;
        float knockbackForce;
        float nextFireTime;

        public void Initialize(ProceduralLevelManager manager, PlayerController target,
            Transform pivot, Transform muzzlePoint, float interval, float speed, float knockback)
        {
            levelManager = manager;
            player = target;
            nozzlePivot = pivot;
            muzzle = muzzlePoint;
            fireInterval = interval;
            projectileSpeed = speed;
            knockbackForce = knockback;
            nextFireTime = Time.time + Random.Range(0.8f, fireInterval);
        }

        void Update()
        {
            if (player == null || !player.controlEnabled) return;

            var aim = (Vector2)(player.transform.position - muzzle.position);
            if (aim.sqrMagnitude > 144f) return;

            var direction = aim.normalized;
            nozzlePivot.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

            if (Time.time < nextFireTime) return;

            levelManager.FireFrosting(muzzle.position, direction,
                projectileSpeed, knockbackForce);
            nextFireTime = Time.time + fireInterval;
        }
    }
}
