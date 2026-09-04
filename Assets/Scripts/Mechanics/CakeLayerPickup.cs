using UnityEngine;

namespace Platformer.Mechanics
{
    public class CakeLayerPickup : MonoBehaviour
    {
        ProceduralLevelManager levelManager;
        float baseHeight;
        float phase;

        public void Initialize(ProceduralLevelManager manager, float height)
        {
            levelManager = manager;
            baseHeight = height;
            phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            var position = transform.position;
            position.y = baseHeight + Mathf.Sin(Time.time * 2.6f + phase) * 0.13f;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Sin(Time.time * 2f + phase) * 4f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null)
                levelManager.CollectLayer(gameObject);
        }
    }
}
