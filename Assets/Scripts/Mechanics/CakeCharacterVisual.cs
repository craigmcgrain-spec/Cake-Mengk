using System.Collections.Generic;
using UnityEngine;

namespace Platformer.Mechanics
{
    [RequireComponent(typeof(PlayerController), typeof(SpriteRenderer))]
    public class CakeCharacterVisual : MonoBehaviour
    {
        [Min(1)] public int startingLayers = 1;
        [Min(1)] public int maximumLayers = 8;

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
        readonly List<Object> generatedAssets = new List<Object>();

        PlayerController player;
        SpriteRenderer originalRenderer;
        Transform visualRoot;
        Sprite cakeSprite;
        Sprite frostingSprite;
        Sprite circleSprite;
        Sprite rectangleSprite;
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
            CreateSprites();

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
            Celebrate();
            return true;
        }

        public void ResetLayers()
        {
            CurrentLayers = Mathf.Clamp(startingLayers, 1, maximumLayers);
            BuildCake();
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

                var width = 1f - i * 0.075f;
                var restPosition = new Vector3(0f, i * 0.23f - 0.04f, 0f);
                layerRoot.localPosition = restPosition;

                CreateRenderer(layerRoot, "Cake", cakeSprite, CakeColors[i % CakeColors.Length],
                    Vector3.zero, new Vector3(width, 1f, 1f), 0);
                CreateRenderer(layerRoot, "Frosting", frostingSprite,
                    new Color(1f, 0.94f, 0.88f), new Vector3(0f, 0.105f, 0f),
                    new Vector3(width, 1f, 1f), 1);

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
            CreateRenderer(parent, "Left Eye", circleSprite, dark,
                new Vector3(-0.14f, 0.015f, -0.01f), new Vector3(0.22f, 0.28f, 1f), 3);
            CreateRenderer(parent, "Right Eye", circleSprite, dark,
                new Vector3(0.14f, 0.015f, -0.01f), new Vector3(0.22f, 0.28f, 1f), 3);
            CreateRenderer(parent, "Smile", circleSprite, dark,
                new Vector3(0f, -0.075f, -0.01f), new Vector3(0.38f, 0.22f, 1f), 3);
            CreateRenderer(parent, "Smile Cover", rectangleSprite,
                CakeColors[0], new Vector3(0f, -0.045f, -0.02f),
                new Vector3(0.5f, 0.14f, 1f), 4);
        }

        void AddCandle(Transform parent)
        {
            CreateRenderer(parent, "Candle", rectangleSprite,
                new Color(0.45f, 0.72f, 1f), new Vector3(0f, 0.245f, 0f),
                new Vector3(0.2f, 0.69f, 1f), 2);
            CreateRenderer(parent, "Flame", circleSprite,
                new Color(1f, 0.72f, 0.14f), new Vector3(0f, 0.39f, 0f),
                new Vector3(0.27f, 0.41f, 1f), 3);
        }

        SpriteRenderer CreateRenderer(Transform parent, string objectName, Sprite sprite,
            Color color, Vector3 position, Vector3 scale, int orderOffset)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;

            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerID = originalRenderer.sortingLayerID;
            renderer.sortingOrder = originalRenderer.sortingOrder + orderOffset;
            return renderer;
        }

        void CreateSprites()
        {
            cakeSprite = CreateMaskedSprite(72, 28, PixelShape.RoundedRectangle);
            frostingSprite = CreateMaskedSprite(72, 13, PixelShape.Frosting);
            circleSprite = CreateMaskedSprite(32, 32, PixelShape.Circle);
            rectangleSprite = CreateMaskedSprite(32, 32, PixelShape.Rectangle);
        }

        enum PixelShape
        {
            Rectangle,
            RoundedRectangle,
            Circle,
            Frosting
        }

        Sprite CreateMaskedSprite(int width, int height, PixelShape shape)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"Generated Cake {shape}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    pixels[y * width + x] = IsVisiblePixel(x, y, width, height, shape)
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            generatedAssets.Add(sprite);
            generatedAssets.Add(texture);
            return sprite;
        }

        static bool IsVisiblePixel(int x, int y, int width, int height, PixelShape shape)
        {
            if (shape == PixelShape.Rectangle) return true;

            if (shape == PixelShape.Circle)
            {
                var nx = (x + 0.5f) / width * 2f - 1f;
                var ny = (y + 0.5f) / height * 2f - 1f;
                return nx * nx + ny * ny <= 1f;
            }

            if (shape == PixelShape.Frosting)
            {
                var drip = x % 18;
                var lowerEdge = drip >= 7 && drip <= 11 ? 1 : 4;
                return y >= lowerEdge && IsInsideRoundedRect(x, y, width, height, 5);
            }

            return IsInsideRoundedRect(x, y, width, height, 7);
        }

        static bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
        {
            var cornerX = x < radius ? radius - x : x >= width - radius ? x - (width - radius - 1) : 0;
            var cornerY = y < radius ? radius - y : y >= height - radius ? y - (height - radius - 1) : 0;
            return cornerX == 0 || cornerY == 0 ||
                cornerX * cornerX + cornerY * cornerY <= radius * radius;
        }

        void OnDestroy()
        {
            for (var i = 0; i < generatedAssets.Count; i++)
                if (generatedAssets[i] != null) Destroy(generatedAssets[i]);
        }
    }
}
