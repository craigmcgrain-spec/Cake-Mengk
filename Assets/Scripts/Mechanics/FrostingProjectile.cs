using UnityEngine;

namespace Platformer.Mechanics
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class FrostingProjectile : MonoBehaviour
    {
        float knockbackForce;
        float destroyAt;
        bool spent;

        public void Initialize(float force, float lifetime)
        {
            knockbackForce = force;
            destroyAt = Time.time + lifetime;
        }

        void Update()
        {
            if (Time.time >= destroyAt)
                Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (spent) return;

            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                spent = true;
                if (player.controlEnabled)
                {
                    var direction = GetComponent<Rigidbody2D>().linearVelocity.normalized;
                    var push = new Vector2(direction.x * knockbackForce,
                        Mathf.Max(1.8f, direction.y * knockbackForce));
                    player.ApplyImpulse(push);
                    player.GetComponent<CakeCharacterVisual>()?
                        .PlayOof(-direction, knockbackForce);
                }
                Destroy(gameObject);
                return;
            }

            if (!other.isTrigger)
                Destroy(gameObject);
        }
    }
}
