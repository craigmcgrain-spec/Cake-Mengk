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
            position.y = baseHeight + Mathf.Sin(Time.time * 2.8f + phase) * 0.14f;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Sin(Time.time * 3.6f + phase) * 28f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null)
                levelManager.TryTrimLayer(gameObject);
        }
    }
}
