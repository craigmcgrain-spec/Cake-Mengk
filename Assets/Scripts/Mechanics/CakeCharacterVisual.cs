using System.Collections.Generic;
using UnityEngine;

namespace Platformer.Mechanics
{
    [RequireComponent(typeof(PlayerController), typeof(SpriteRenderer))]
    public class CakeCharacterVisual : MonoBehaviour
    {
        [Min(1)] public int startingLayers = 1;
        [Min(1)] public int maximumLayers = 64;

        [Header("Jiggle")]
        [Min(0)] public float springStrength = 42f;
        [Min(0)] public float damping = 8f;
        [Min(0)] public float movementSway = 0.018f;
        [Min(0)] public float idleWobble = 0.012f;

        public int CurrentLayers { get; private set; }
        public int MaximumLayers => maximumLayers;

        sealed class LayerMotion
        {
            public Transform transform;
            public Vector3 restPosition;
            public Vector2 offset;
            public Vector2 velocity;
            public float angle;
            public float angularVelocity;
        }

        readonly List<LayerMotion> layers = new List<LayerMotion>();
        readonly Dictionary<Color32, Material> materials = new Dictionary<Color32, Material>();

        PlayerController player;
        SpriteRenderer originalRenderer;
        Transform visualRoot;
        float celebrationVelocity;

        static readonly Color[] CakeColors =
        {
            new Color(1f, 0.55f, 0.63f),
            new Color(1f, 0.78f, 0.35f),
            new Color(0.43f, 0.82f, 0.76f),
            new Color(0.64f, 0.55f, 0.91f),
            new Color(0.97f, 0.68f, 0.35f),
            new Color(0.42f, 0.68f, 0.95f)
        };

        void Awake()
        {
            player = GetComponent<PlayerController>();
            originalRenderer = GetComponent<SpriteRenderer>();

            visualRoot = new GameObject("Cake Visual").transform;
            visualRoot.SetParent(transform, false);

            CurrentLayers = Mathf.Clamp(startingLayers, 1, maximumLayers);
            BuildCake();
        }

        void Start()
        {
            originalRenderer.enabled = false;
        }

        void LateUpdate()
        {
            if (layers.Count == 0) return;

            var deltaTime = Mathf.Min(Time.deltaTime, 0.033f);
            var groundedSquash = player.IsGrounded
                ? Mathf.Sin(Time.time * 8f) * idleWobble
                : 0f;

            celebrationVelocity -= celebrationVelocity * Mathf.Min(1f, deltaTime * 5f);

            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var heightFactor = i + 1f;
                var target = new Vector2(
                    -player.velocity.x * movementSway * heightFactor +
                    Mathf.Sin(Time.time * 4.5f + i * 0.8f) * idleWobble * heightFactor,
                    celebrationVelocity * 0.018f * heightFactor);

                layer.velocity += (target - layer.offset) * springStrength * deltaTime;
                layer.velocity *= Mathf.Exp(-damping * deltaTime);
                layer.offset += layer.velocity * deltaTime;

                var targetAngle = -player.velocity.x * 1.4f * heightFactor +
                    Mathf.Sin(Time.time * 3.8f + i) * 1.2f;
                layer.angularVelocity += Mathf.DeltaAngle(layer.angle, targetAngle) *
                    springStrength * deltaTime;
                layer.angularVelocity *= Mathf.Exp(-damping * deltaTime);
                layer.angle += layer.angularVelocity * deltaTime;

                layer.transform.localPosition = layer.restPosition +
                    new Vector3(layer.offset.x, layer.offset.y, 0f);
                layer.transform.localRotation = Quaternion.Euler(0f, 0f, layer.angle);

                var squash = groundedSquash / heightFactor;
                layer.transform.localScale = new Vector3(1f + squash, 1f - squash, 1f);
            }
        }

        public bool AddLayer()
        {
            if (CurrentLayers >= maximumLayers) return false;

            CurrentLayers++;
            BuildCake();
            player.SetCakeLayerCount(CurrentLayers);
            Celebrate();
            return true;
        }

        public bool RemoveLayer()
        {
            if (CurrentLayers <= startingLayers) return false;

            CurrentLayers--;
            BuildCake();
            player.SetCakeLayerCount(CurrentLayers);
            Celebrate();
            return true;
        }

        public void ResetLayers()
        {
            CurrentLayers = Mathf.Clamp(startingLayers, 1, maximumLayers);
            BuildCake();
            player.SetCakeLayerCount(CurrentLayers);
        }

        public void BounceOnLanding()
        {
            for (var i = 0; i < layers.Count; i++)
                layers[i].velocity.y = Mathf.Max(layers[i].velocity.y, 0.9f + i * 0.22f);
        }

        void Celebrate()
        {
            celebrationVelocity = 5f;
            for (var i = 0; i < layers.Count; i++)
            {
                layers[i].velocity = new Vector2(
                    (i % 2 == 0 ? -1f : 1f) * (0.7f + i * 0.1f),
                    2.4f + i * 0.35f);
                layers[i].angularVelocity = (i % 2 == 0 ? -1f : 1f) * 75f;
            }
        }

        void BuildCake()
        {
            for (var i = visualRoot.childCount - 1; i >= 0; i--)
                Destroy(visualRoot.GetChild(i).gameObject);

            layers.Clear();

            for (var i = 0; i < CurrentLayers; i++)
            {
                var layerRoot = new GameObject($"Cake Layer {i + 1}").transform;
                layerRoot.SetParent(visualRoot, false);

                var width = Mathf.Max(0.45f, 1f - i * 0.045f);
                var restPosition = new Vector3(0f, i * 0.23f - 0.04f, 0f);
                layerRoot.localPosition = restPosition;

                CreatePrimitive(layerRoot, "Cake", PrimitiveType.Cylinder,
                    CakeColors[i % CakeColors.Length], Vector3.zero,
                    new Vector3(width, 0.11f, 0.72f));
                CreatePrimitive(layerRoot, "Frosting", PrimitiveType.Cylinder,
                    new Color(1f, 0.94f, 0.88f), new Vector3(0f, 0.125f, 0f),
                    new Vector3(width * 1.03f, 0.025f, 0.74f));

                layers.Add(new LayerMotion
                {
                    transform = layerRoot,
                    restPosition = restPosition
                });
            }

            AddFace(layers[0].transform);
            AddCandle(layers[layers.Count - 1].transform);
        }

        void AddFace(Transform parent)
        {
            var dark = new Color(0.22f, 0.12f, 0.18f);
            CreatePrimitive(parent, "Left Eye", PrimitiveType.Sphere, dark,
                new Vector3(-0.14f, 0.025f, -0.37f), new Vector3(0.075f, 0.1f, 0.045f));
            CreatePrimitive(parent, "Right Eye", PrimitiveType.Sphere, dark,
                new Vector3(0.14f, 0.025f, -0.37f), new Vector3(0.075f, 0.1f, 0.045f));
            CreatePrimitive(parent, "Smile", PrimitiveType.Cube, dark,
                new Vector3(0f, -0.075f, -0.38f), new Vector3(0.16f, 0.035f, 0.035f));
        }

        void AddCandle(Transform parent)
        {
            CreatePrimitive(parent, "Candle", PrimitiveType.Cube,
                new Color(0.45f, 0.72f, 1f), new Vector3(0f, 0.27f, 0f),
                new Vector3(0.065f, 0.22f, 0.065f));
            CreatePrimitive(parent, "Flame", PrimitiveType.Sphere,
                new Color(1f, 0.55f, 0.08f), new Vector3(0f, 0.43f, 0f),
                new Vector3(0.09f, 0.14f, 0.07f));
        }

        void CreatePrimitive(Transform parent, string objectName, PrimitiveType primitiveType,
            Color color, Vector3 position, Vector3 scale)
        {
            var child = GameObject.CreatePrimitive(primitiveType);
            child.name = objectName;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;

            var primitiveCollider = child.GetComponent<Collider>();
            if (primitiveCollider != null) Destroy(primitiveCollider);

            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMaterial(color);
        }

        Material GetMaterial(Color color)
        {
            var key = (Color32)color;
            if (materials.TryGetValue(key, out var material)) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            material = new Material(shader)
            {
                name = $"Cake {ColorUtility.ToHtmlStringRGB(color)}",
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            materials.Add(key, material);
            return material;
        }

        void OnDestroy()
        {
            foreach (var material in materials.Values)
                if (material != null) Destroy(material);
        }
    }
}
