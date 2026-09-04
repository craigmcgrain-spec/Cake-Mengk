using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        [Min(0)] public float minimumOofSpeed = 1.75f;
        [Min(1)] public int maximumTrailSplatters = 48;

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
        readonly List<GameObject> trailSplatters = new List<GameObject>();
        readonly Dictionary<Color32, Material> materials = new Dictionary<Color32, Material>();

        PlayerController player;
        SpriteRenderer originalRenderer;
        Transform visualRoot;
        Transform splatterRoot;
        Material splatterParticleMaterial;
        float celebrationVelocity;
        float oofStrength;
        float oofVelocity;
        float nextOofTime;
        Vector2 oofNormal = Vector2.up;
        Vector3 lastSplatterPosition;

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
            visualRoot.localPosition = new Vector3(0f, 0f, -2.1f);
            splatterRoot = new GameObject("Frosting Splatter Trail").transform;
            lastSplatterPosition = transform.position;

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
                ? Mathf.Sin(Time.time * 10f) * idleWobble * 1.8f
                : 0f;

            celebrationVelocity -= celebrationVelocity * Mathf.Min(1f, deltaTime * 5f);
            oofStrength = Mathf.SmoothDamp(oofStrength, 0f, ref oofVelocity,
                0.16f, Mathf.Infinity, deltaTime);

            var verticalImpact = Mathf.Abs(oofNormal.y);
            visualRoot.localScale = new Vector3(
                1f + oofStrength * (0.22f * verticalImpact - 0.2f * (1f - verticalImpact)),
                1f + oofStrength * (-0.24f * verticalImpact + 0.14f * (1f - verticalImpact)),
                1f + oofStrength * 0.08f);
            visualRoot.localRotation = Quaternion.Euler(0f, 0f,
                -oofNormal.x * oofStrength * 8f);

            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var heightFactor = i + 1f;
                var speedWobble = Mathf.Abs(player.velocity.x) * 0.006f;
                var target = new Vector2(
                    -player.velocity.x * movementSway * heightFactor +
                    Mathf.Sin(Time.time * 5.5f + i * 0.9f) *
                    (idleWobble + speedWobble) * heightFactor,
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
                layer.transform.localRotation = Quaternion.Euler(
                    layer.offset.y * 18f,
                    Mathf.Sin(Time.time * 4.2f + i * 1.3f) * 5f,
                    layer.angle);

                var squash = groundedSquash / Mathf.Sqrt(heightFactor);
                var sideWiggle = Mathf.Sin(Time.time * 7f + i) * speedWobble;
                layer.transform.localScale = new Vector3(
                    1f + squash + sideWiggle,
                    1f - squash * 1.2f,
                    1f - sideWiggle * 0.7f);
            }

            UpdateSplatterTrail();
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
            ClearTrailSplatters();
        }

        public void BounceOnLanding()
        {
            for (var i = 0; i < layers.Count; i++)
                layers[i].velocity.y = Mathf.Max(layers[i].velocity.y, 0.9f + i * 0.22f);
        }

        public void PlayOof(Vector2 surfaceNormal, float impactSpeed)
        {
            if (impactSpeed < minimumOofSpeed || Time.time < nextOofTime) return;

            nextOofTime = Time.time + 0.12f;
            oofNormal = surfaceNormal.normalized;
            oofStrength = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(
                minimumOofSpeed, minimumOofSpeed + 7f, impactSpeed));
            oofVelocity = 0f;

            for (var i = 0; i < layers.Count; i++)
            {
                var delayFactor = 1f + i * 0.18f;
                layers[i].velocity += oofNormal * oofStrength * delayFactor * 2.4f;
                layers[i].angularVelocity +=
                    -oofNormal.x * oofStrength * delayFactor * 95f;
            }

            if (surfaceNormal.y >= 0.5f)
                EmitLandingFrosting(oofStrength);
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
                AddFrostingDetails(layerRoot, i, width);

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
            var white = new Color(1f, 0.98f, 0.9f);
            CreatePrimitive(parent, "Left Googly Eye", PrimitiveType.Sphere, white,
                new Vector3(-0.17f, 0.035f, -0.38f), new Vector3(0.15f, 0.19f, 0.08f));
            CreatePrimitive(parent, "Right Googly Eye", PrimitiveType.Sphere, white,
                new Vector3(0.16f, 0.055f, -0.39f), new Vector3(0.19f, 0.23f, 0.09f));
            CreatePrimitive(parent, "Left Pupil", PrimitiveType.Sphere, dark,
                new Vector3(-0.145f, 0.015f, -0.455f), new Vector3(0.055f, 0.07f, 0.035f));
            CreatePrimitive(parent, "Right Pupil", PrimitiveType.Sphere, dark,
                new Vector3(0.12f, 0.095f, -0.48f), new Vector3(0.07f, 0.085f, 0.04f));
            CreatePrimitive(parent, "Smile", PrimitiveType.Cube, dark,
                new Vector3(0f, -0.075f, -0.38f), new Vector3(0.16f, 0.035f, 0.035f));
            var tongue = CreatePrimitive(parent, "Tongue", PrimitiveType.Sphere,
                new Color(1f, 0.25f, 0.45f), new Vector3(0.055f, -0.11f, -0.41f),
                new Vector3(0.085f, 0.07f, 0.035f));
            tongue.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        }

        void AddFrostingDetails(Transform parent, int layerIndex, float width)
        {
            if (layerIndex > 16) return;

            var frosting = new Color(1f, 0.94f, 0.88f);
            for (var i = 0; i < 3; i++)
            {
                var x = (i - 1) * width * 0.27f;
                var dripLength = 0.06f + ((layerIndex + i) % 3) * 0.025f;
                CreatePrimitive(parent, $"Frosting Drip {i + 1}", PrimitiveType.Sphere,
                    frosting, new Vector3(x, 0.085f - dripLength, -0.37f),
                    new Vector3(0.11f, dripLength, 0.055f));
            }

            if (layerIndex > 12) return;
            var sprinkleColors = new[]
            {
                new Color(1f, 0.2f, 0.38f),
                new Color(0.2f, 0.75f, 1f),
                new Color(1f, 0.78f, 0.12f)
            };
            for (var i = 0; i < 3; i++)
            {
                var sprinkle = CreatePrimitive(parent, $"Sprinkle {i + 1}",
                    PrimitiveType.Cube, sprinkleColors[(layerIndex + i) % sprinkleColors.Length],
                    new Vector3((i - 1) * width * 0.23f, 0.17f, -0.2f + i * 0.18f),
                    new Vector3(0.035f, 0.075f, 0.035f));
                sprinkle.transform.localRotation =
                    Quaternion.Euler(0f, 0f, -25f + i * 28f);
            }
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

        GameObject CreatePrimitive(Transform parent, string objectName, PrimitiveType primitiveType,
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
            return child;
        }

        void UpdateSplatterTrail()
        {
            if (!player.IsGrounded || Mathf.Abs(player.velocity.x) < 0.45f) return;

            var position = new Vector3(transform.position.x,
                transform.position.y - 0.43f, -0.82f);
            if (Vector3.Distance(position, lastSplatterPosition) < 0.48f) return;

            lastSplatterPosition = position;
            CreatePersistentSplatter(position, 0.12f, Random.Range(0.75f, 1.25f));
        }

        void EmitLandingFrosting(float strength)
        {
            var landingPosition = new Vector3(transform.position.x,
                transform.position.y - 0.4f, -0.9f);
            for (var i = 0; i < 5; i++)
            {
                var offset = new Vector3((i - 2) * 0.16f, 0f,
                    Random.Range(-0.18f, 0.18f));
                CreatePersistentSplatter(landingPosition + offset,
                    Random.Range(0.1f, 0.18f), Random.Range(0.65f, 1.35f));
            }

            var burstObject = new GameObject("Landing Frosting Burst");
            burstObject.transform.SetParent(splatterRoot, true);
            burstObject.transform.position = landingPosition + Vector3.up * 0.15f;
            var particles = burstObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f + strength);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.9f, 0.96f), new Color(1f, 0.42f, 0.68f));
            main.gravityModifier = 0.85f;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(14f + strength * 14f))
            });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.18f;

            burstObject.GetComponent<ParticleSystemRenderer>().material =
                GetSplatterParticleMaterial();
            particles.Play();
        }

        void CreatePersistentSplatter(Vector3 position, float size, float stretch)
        {
            var color = Random.value > 0.35f
                ? new Color(1f, 0.82f, 0.92f)
                : new Color(1f, 0.5f, 0.72f);
            var splatter = CreatePrimitive(splatterRoot, "Frosting Splatter",
                PrimitiveType.Sphere, color, position,
                new Vector3(size * stretch, 0.018f, size));
            trailSplatters.Add(splatter);

            while (trailSplatters.Count > maximumTrailSplatters)
            {
                var oldest = trailSplatters[0];
                trailSplatters.RemoveAt(0);
                if (oldest != null) Destroy(oldest);
            }
        }

        Material GetSplatterParticleMaterial()
        {
            if (splatterParticleMaterial != null) return splatterParticleMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            splatterParticleMaterial = new Material(shader)
            {
                name = "Frosting Burst Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            splatterParticleMaterial.SetFloat("_Surface", 1f);
            splatterParticleMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            splatterParticleMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            splatterParticleMaterial.SetInt("_ZWrite", 0);
            splatterParticleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            splatterParticleMaterial.renderQueue = (int)RenderQueue.Transparent;
            return splatterParticleMaterial;
        }

        void ClearTrailSplatters()
        {
            foreach (var splatter in trailSplatters)
                if (splatter != null) Destroy(splatter);
            trailSplatters.Clear();
            lastSplatterPosition = transform.position;
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
            if (splatterParticleMaterial != null) Destroy(splatterParticleMaterial);
            if (splatterRoot != null) Destroy(splatterRoot.gameObject);
        }
    }
}
