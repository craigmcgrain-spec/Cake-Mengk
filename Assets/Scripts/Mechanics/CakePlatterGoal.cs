using UnityEngine;

namespace Platformer.Mechanics
{
    public class CakePlatterGoal : MonoBehaviour
    {
        ProceduralLevelManager levelManager;

        public void Initialize(ProceduralLevelManager manager)
        {
            levelManager = manager;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null)
                levelManager.TryFinishLevel();
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null)
                levelManager.TryFinishLevel();
        }
    }
}
