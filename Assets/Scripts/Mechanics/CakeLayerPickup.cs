using UnityEngine;

namespace Platformer.Mechanics
{
    public class CakeLayerPickup : MonoBehaviour
    {
        ProceduralLevelManager levelManager;
        float baseHeight;
        float phase;
        float collectableAt;
        bool dropped;
        bool collected;

        public void Initialize(ProceduralLevelManager manager, float height, bool isDropped)
        {
            levelManager = manager;
            baseHeight = height;
            phase = Random.value * Mathf.PI * 2f;
            dropped = isDropped;
            collectableAt = isDropped ? Time.time + 1.75f : Time.time;
        }

        void Update()
        {
            if (dropped) return;

            var position = transform.position;
            position.y = baseHeight + Mathf.Sin(Time.time * 2.6f + phase) * 0.13f;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Sin(Time.time * 2f + phase) * 4f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            TryCollect(other.gameObject);
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            TryCollect(collision.gameObject);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            TryCollect(collision.gameObject);
        }

        void TryCollect(GameObject other)
        {
            if (collected || Time.time < collectableAt ||
                other.GetComponent<PlayerController>() == null)
                return;

            collected = levelManager.CollectLayer(gameObject);
        }
    }
}
