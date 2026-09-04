using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace Platformer.Mechanics
{
    public class ProceduralLevelManager : MonoBehaviour
    {
        [Header("Generation")]
        public int seed = 4815;
        [Min(1)] public int startingLevel = 1;
        [Min(4)] public int basePlatformCount = 6;

        [Header("World")]
        public float deathHeight = -7f;
        public Color platformColor = new Color(0.32f, 0.21f, 0.42f);
        public Color platformTopColor = new Color(0.96f, 0.68f, 0.78f);

        public int CurrentLevel { get; private set; }
        public int RequiredLayers { get; private set; }

        readonly string[] legacyRootNames =
        {
            "Level", "Enemies", "Zones", "Tokens", "PatrolPaths"
        };

        PlayerController player;
        CakeCharacterVisual cake;
        Transform levelRoot;
        Sprite squareSprite;
        Sprite circleSprite;
        Vector2 spawnPosition;
        string statusMessage;
        float statusMessageUntil;
        bool changingLevel;

        void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("ProceduralLevelManager requires a PlayerController in the scene.");
                enabled = false;
                return;
            }

            cake = player.GetComponent<CakeCharacterVisual>();
            if (cake == null)
            {
                Debug.LogError("ProceduralLevelManager requires CakeCharacterVisual on the player.");
                enabled = false;
                return;
            }

            DisableLegacyLevel();
            var cameraConfiner = FindAnyObjectByType<CinemachineConfiner2D>();
            if (cameraConfiner != null) cameraConfiner.enabled = false;
            CreateRuntimeSprites();
            BeginLevel(Mathf.Max(1, startingLevel));
        }

        void Update()
        {
            if (!changingLevel && player.transform.position.y < deathHeight)
                Respawn();
        }

        void DisableLegacyLevel()
        {
            foreach (var rootName in legacyRootNames)
            {
                var legacyRoot = GameObject.Find(rootName);
                if (legacyRoot != null) legacyRoot.SetActive(false);
            }
        }

        void BeginLevel(int levelNumber)
        {
            CurrentLevel = levelNumber;
            changingLevel = false;
            player.controlEnabled = true;
            cake.ResetLayers();

            if (levelRoot != null)
            {
                levelRoot.gameObject.SetActive(false);
                Destroy(levelRoot.gameObject);
            }

            levelRoot = new GameObject($"Procedural Level {CurrentLevel}").transform;
            GenerateLevel();
            Respawn();
            ShowStatus($"Level {CurrentLevel}: collect {RequiredLayers - cake.startingLayers} cake layers.");
        }

        void GenerateLevel()
        {
            var random = new System.Random(seed + CurrentLevel * 7919);
            var intermediatePlatformCount = basePlatformCount + CurrentLevel;
            RequiredLayers = Mathf.Min(cake.MaximumLayers,
                2 + Mathf.CeilToInt(CurrentLevel * 0.5f));
            var pickupCount = RequiredLayers - cake.startingLayers;

            var platforms = new List<Vector2>();
            var currentX = 0f;
            var currentY = 0f;
            CreatePlatform(new Vector2(currentX, currentY), 4.2f, 0.65f);
            spawnPosition = new Vector2(currentX - 1.2f, currentY + 1.1f);

            for (var i = 0; i < intermediatePlatformCount; i++)
            {
                var difficulty = CurrentLevel - 1;
                var widthMin = Mathf.Max(1.45f, 2.8f - difficulty * 0.07f);
                var widthMax = Mathf.Max(widthMin + 0.35f, 3.8f - difficulty * 0.05f);
                var width = RandomRange(random, widthMin, widthMax);
                var gap = RandomRange(random, 1.15f,
                    Mathf.Min(3.5f, 1.65f + difficulty * 0.13f));
                var verticalRange = Mathf.Min(1.75f, 0.45f + difficulty * 0.09f);

                currentX += gap + width * 0.5f + (i == 0 ? 2.1f : platforms[i - 1].x);
                currentY = Mathf.Clamp(currentY +
                    RandomRange(random, -verticalRange, verticalRange), -1.8f, 3.4f);

                var position = new Vector2(currentX, currentY);
                platforms.Add(position);
                CreatePlatform(position, width, 0.6f);
                currentX = width * 0.5f;
            }

            for (var i = 0; i < pickupCount; i++)
            {
                var platformIndex = Mathf.Clamp(
                    Mathf.RoundToInt((i + 1f) * platforms.Count / (pickupCount + 1f)),
                    0, platforms.Count - 1);
                CreateLayerPickup(platforms[platformIndex] + Vector2.up * 1.05f);
            }

            var lastPlatform = platforms[platforms.Count - 1];
            var endPosition = new Vector2(lastPlatform.x + currentX + 4.3f,
                Mathf.Clamp(lastPlatform.y + RandomRange(random, -0.5f, 0.5f), -1.5f, 3.2f));
            CreatePlatform(endPosition, 5f, 0.7f);
            CreatePlatter(endPosition + Vector2.up * 0.52f);
        }

        static float RandomRange(System.Random random, float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }

        void CreatePlatform(Vector2 position, float width, float height)
        {
            var platform = new GameObject("Platform");
            platform.transform.SetParent(levelRoot, false);
            platform.transform.position = position;

            var renderer = platform.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = platformColor;
            renderer.sortingOrder = -1;
            platform.transform.localScale = new Vector3(width, height, 1f);

            var collider = platform.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            var top = new GameObject("Icing Edge");
            top.transform.SetParent(platform.transform, false);
            top.transform.localPosition = new Vector3(0f, 0.43f, -0.01f);
            top.transform.localScale = new Vector3(1f, 0.14f, 1f);
            var topRenderer = top.AddComponent<SpriteRenderer>();
            topRenderer.sprite = squareSprite;
            topRenderer.color = platformTopColor;
            topRenderer.sortingOrder = 0;
        }

        void CreateLayerPickup(Vector2 position)
        {
            var pickup = new GameObject("Cake Layer Pickup");
            pickup.transform.SetParent(levelRoot, false);
            pickup.transform.position = position;

            var bodyObject = new GameObject("Cake");
            bodyObject.transform.SetParent(pickup.transform, false);
            bodyObject.transform.localScale = new Vector3(0.72f, 0.28f, 1f);
            var body = bodyObject.AddComponent<SpriteRenderer>();
            body.sprite = squareSprite;
            body.color = new Color(1f, 0.57f, 0.65f);
            body.sortingOrder = 4;

            var icing = new GameObject("Icing");
            icing.transform.SetParent(pickup.transform, false);
            icing.transform.localPosition = new Vector3(0f, 0.13f, -0.01f);
            icing.transform.localScale = new Vector3(0.76f, 0.09f, 1f);
            var icingRenderer = icing.AddComponent<SpriteRenderer>();
            icingRenderer.sprite = squareSprite;
            icingRenderer.color = new Color(1f, 0.95f, 0.88f);
            icingRenderer.sortingOrder = 5;

            var collider = pickup.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 0.55f);
            collider.isTrigger = true;

            pickup.AddComponent<CakeLayerPickup>().Initialize(this, position.y);
        }

        void CreatePlatter(Vector2 position)
        {
            var platter = new GameObject("Cake Platter");
            platter.transform.SetParent(levelRoot, false);
            platter.transform.position = position;

            var plateVisual = new GameObject("Plate");
            plateVisual.transform.SetParent(platter.transform, false);
            plateVisual.transform.localScale = new Vector3(2.5f, 0.38f, 1f);
            var renderer = plateVisual.AddComponent<SpriteRenderer>();
            renderer.sprite = circleSprite;
            renderer.color = new Color(0.86f, 0.93f, 1f);
            renderer.sortingOrder = 1;

            var goal = platter.AddComponent<BoxCollider2D>();
            goal.isTrigger = true;
            goal.size = new Vector2(2.5f, 1.8f);
            goal.offset = new Vector2(0f, 0.75f);
            platter.AddComponent<CakePlatterGoal>().Initialize(this);

            for (var i = 0; i < RequiredLayers; i++)
            {
                var guide = new GameObject($"Required Layer {i + 1}");
                guide.transform.SetParent(platter.transform, false);
                guide.transform.localPosition = new Vector3(0f, 0.3f + i * 0.22f, 0f);
                guide.transform.localScale = new Vector3(0.72f - i * 0.025f, 0.18f, 1f);
                var guideRenderer = guide.AddComponent<SpriteRenderer>();
                guideRenderer.sprite = squareSprite;
                guideRenderer.color = new Color(1f, 1f, 1f, 0.22f);
                guideRenderer.sortingOrder = 0;
            }
        }

        public void CollectLayer(GameObject pickup)
        {
            if (!cake.AddLayer()) return;

            Destroy(pickup);
            ShowStatus($"Cake size: {cake.CurrentLayers}/{RequiredLayers} layers");
        }

        public void TryFinishLevel()
        {
            if (changingLevel) return;

            if (cake.CurrentLayers < RequiredLayers)
            {
                ShowStatus($"Too small! Collect {RequiredLayers - cake.CurrentLayers} more.");
                return;
            }

            changingLevel = true;
            player.controlEnabled = false;
            ShowStatus("Perfect fit!");
            StartCoroutine(AdvanceLevel());
        }

        IEnumerator AdvanceLevel()
        {
            yield return new WaitForSeconds(1.25f);
            BeginLevel(CurrentLevel + 1);
        }

        void Respawn()
        {
            player.Teleport(spawnPosition);
            player.jumpState = PlayerController.JumpState.Grounded;
            player.controlEnabled = true;
            ShowStatus("Click anywhere on the board to jump toward it.");
        }

        void ShowStatus(string message)
        {
            statusMessage = message;
            statusMessageUntil = Time.time + 2.5f;
        }

        void OnGUI()
        {
            GUI.Box(new Rect(18f, 18f, 285f, 76f), string.Empty);
            GUI.Label(new Rect(32f, 27f, 250f, 22f),
                $"Level {CurrentLevel}   Cake {cake?.CurrentLayers ?? 0}/{RequiredLayers}   " +
                $"Weight {player?.CurrentWeight ?? 1f:0.0}x");
            GUI.Label(new Rect(32f, 52f, 255f, 30f),
                Time.time < statusMessageUntil
                    ? statusMessage
                    : "Collect every layer, then reach the platter.");
        }

        void CreateRuntimeSprites()
        {
            squareSprite = CreateSprite(2, false);
            circleSprite = CreateSprite(48, true);
        }

        static Sprite CreateSprite(int size, bool circle)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = circle ? "Runtime Circle" : "Runtime Square",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var visible = true;
                    if (circle)
                    {
                        var nx = (x + 0.5f) / size * 2f - 1f;
                        var ny = (y + 0.5f) / size * 2f - 1f;
                        visible = nx * nx + ny * ny <= 1f;
                    }
                    pixels[y * size + x] = visible
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        void OnDestroy()
        {
            if (squareSprite != null)
            {
                Destroy(squareSprite.texture);
                Destroy(squareSprite);
            }
            if (circleSprite != null)
            {
                Destroy(circleSprite.texture);
                Destroy(circleSprite);
            }
        }
    }
}
