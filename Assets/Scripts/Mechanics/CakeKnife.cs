using UnityEngine;

namespace Platformer.Mechanics
{
    public class CakeKnife : MonoBehaviour
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
            position.y = baseHeight + Mathf.Sin(Time.time * 2.2f + phase) * 0.1f;
            transform.position = position;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null)
                levelManager.TryTrimLayer(gameObject);
        }
    }
}
