using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using Platformer.View;

namespace Platformer.Mechanics
{
    public class ProceduralLevelManager : MonoBehaviour
    {
        [Header("Generation")]
        public int seed = 4815;
        [Min(1)] public int startingLevel = 1;
        [Min(4)] public int basePlatformCount = 6;
        [Min(1)] public int levelsPerPhase = 5;

        [Header("World")]
        public float deathHeight = -7f;
        public float levelBaseHeight = -5.5f;
        public Color platformColor = new Color(0.32f, 0.21f, 0.42f);
        public Color platformTopColor = new Color(0.96f, 0.68f, 0.78f);

        public int CurrentLevel { get; private set; }
        public int CurrentPhase { get; private set; }
        public int RequiredLayers { get; private set; }
        public float LevelTime => levelComplete
            ? completedLevelTime
            : Mathf.Max(0f, Time.time - levelStartTime);
        public int LastLevelScore { get; private set; }
        public int TotalScore { get; private set; }

        readonly string[] legacyRootNames =
        {
            "Level", "Enemies", "Zones", "Tokens", "PatrolPaths"
        };

        PlayerController player;
        CakeCharacterVisual cake;
        Transform levelRoot;
        readonly Dictionary<Color32, Material> materials = new Dictionary<Color32, Material>();
        Material particleMaterial;
        Vector2 spawnPosition;
        Vector3 platterPosition;
        string statusMessage;
        float statusMessageUntil;
        bool changingLevel;
        bool levelComplete;
        float levelStartTime;
        float completedLevelTime;

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
            ConfigurePresentation();
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
            CurrentPhase = (CurrentLevel - 1) / Mathf.Max(1, levelsPerPhase) + 1;
            changingLevel = false;
            levelComplete = false;
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
            levelStartTime = Time.time;
            ShowStatus($"Phase {CurrentPhase}: reach exactly {RequiredLayers} layers.");
        }

        void GenerateLevel()
        {
            var random = new System.Random(seed + CurrentLevel * 7919);
            var difficulty = CurrentPhase - 1;
            var intermediatePlatformCount = basePlatformCount + difficulty * 2;
            RequiredLayers = Mathf.Min(cake.MaximumLayers, CurrentPhase + 1);
            var extraLayerCount = Mathf.Min(CurrentPhase - 1,
                cake.MaximumLayers - RequiredLayers);
            var pickupCount = RequiredLayers - cake.startingLayers + extraLayerCount;
            var pitFrequency = Mathf.Min(0.3f, 0.15f + difficulty * 0.015f);
            var pitCount = Mathf.Max(1,
                Mathf.RoundToInt(intermediatePlatformCount * pitFrequency));
            var pitIndices = new HashSet<int>();
            while (pitIndices.Count < pitCount)
                pitIndices.Add(random.Next(intermediatePlatformCount));

            var platforms = new List<Vector2>();
            var currentX = 0f;
            var currentY = 0f;
            CreatePlatform(new Vector2(currentX, currentY), 4.2f, 0.65f);
            spawnPosition = new Vector2(currentX - 1.2f, currentY + 1.1f);

            for (var i = 0; i < intermediatePlatformCount; i++)
            {
                var widthMin = Mathf.Max(1.45f, 2.8f - difficulty * 0.07f);
                var widthMax = Mathf.Max(widthMin + 0.35f, 3.8f - difficulty * 0.05f);
                var width = RandomRange(random, widthMin, widthMax);
                var hasPit = pitIndices.Contains(i);
                var gap = hasPit
                    ? RandomRange(random, 0.65f,
                        Mathf.Min(2.25f, 1.25f + difficulty * 0.1f))
                    : RandomRange(random, -0.3f, 0.12f);
                var verticalRange = Mathf.Min(1.3f, 0.85f + difficulty * 0.08f);
                var verticalDirection = random.NextDouble() < 0.5 ? -1f : 1f;
                var heightChange = verticalDirection *
                    RandomRange(random, 0.35f, verticalRange);
                if (currentY + heightChange < -1.8f || currentY + heightChange > 3.4f)
                    heightChange *= -1f;

                currentX += gap + width * 0.5f + (i == 0 ? 2.1f : platforms[i - 1].x);
                currentY = Mathf.Clamp(currentY + heightChange, -1.8f, 3.4f);

                var position = new Vector2(currentX, currentY);
                platforms.Add(position);
                CreatePlatform(position, width, 0.6f);
                currentX = width * 0.5f;
            }

            for (var i = 0; i < pickupCount; i++)
            {
                var platformIndex = Mathf.Clamp(
                    Mathf.FloorToInt((i + 1f) * platforms.Count * 0.72f /
                        (pickupCount + 1f)),
                    0, platforms.Count - 1);
                var offset = new Vector2((i % 2 == 0 ? -1f : 1f) * 0.35f, 1.05f);
                CreateLayerPickup(platforms[platformIndex] + offset, false);
            }

            for (var i = 0; i < extraLayerCount; i++)
            {
                var firstKnifePlatform = Mathf.CeilToInt(platforms.Count * 0.78f);
                var platformIndex = Mathf.Clamp(firstKnifePlatform + i,
                    0, platforms.Count - 1);
                CreateCakeKnife(platforms[platformIndex] + Vector2.up * 1.05f);
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
            var surfaceHeight = position.y + height * 0.5f;
            var foundationHeight = Mathf.Max(height, surfaceHeight - levelBaseHeight);
            platform.transform.position = new Vector3(position.x,
                levelBaseHeight + foundationHeight * 0.5f, 0f);

            CreatePrimitiveVisual(platform.transform, "Foundation", PrimitiveType.Cube,
                platformColor, Vector3.zero, new Vector3(width, foundationHeight, 1.5f));

            var collider = platform.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(width, foundationHeight);

            CreatePrimitiveVisual(platform.transform, "Icing Edge", PrimitiveType.Cube,
                platformTopColor, new Vector3(0f, foundationHeight * 0.5f, -0.03f),
                new Vector3(width + 0.06f, 0.12f, 1.58f));
        }

        GameObject CreateLayerPickup(Vector2 position, bool dropped)
        {
            var pickup = new GameObject("Cake Layer Pickup");
            pickup.transform.SetParent(levelRoot, false);
            pickup.transform.position = position;

            CreatePrimitiveVisual(pickup.transform, "Cake", PrimitiveType.Cylinder,
                new Color(1f, 0.57f, 0.65f), Vector3.zero,
                new Vector3(0.72f, 0.13f, 0.58f));
            CreatePrimitiveVisual(pickup.transform, "Icing", PrimitiveType.Cylinder,
                new Color(1f, 0.95f, 0.88f), new Vector3(0f, 0.15f, 0f),
                new Vector3(0.75f, 0.025f, 0.6f));

            var collider = pickup.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 0.55f);
            collider.isTrigger = !dropped;

            if (dropped)
            {
                var rigidbody = pickup.AddComponent<Rigidbody2D>();
                rigidbody.gravityScale = 1.25f;
                rigidbody.freezeRotation = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rigidbody.linearVelocity = new Vector2(
                    player.velocity.x >= 0f ? -0.35f : 0.35f, 2.2f);
            }

            pickup.AddComponent<CakeLayerPickup>().Initialize(this, position.y, dropped);
            return pickup;
        }

        void CreateCakeKnife(Vector2 position)
        {
            var knife = new GameObject("Cake Knife");
            knife.transform.SetParent(levelRoot, false);
            knife.transform.position = position;

            var blade = CreatePrimitiveVisual(knife.transform, "Blade", PrimitiveType.Cube,
                new Color(0.83f, 0.9f, 0.96f), Vector3.zero,
                new Vector3(0.75f, 0.16f, 0.16f));
            blade.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            CreatePrimitiveVisual(blade.transform, "Handle", PrimitiveType.Cube,
                new Color(0.38f, 0.18f, 0.24f), new Vector3(-0.7f, 0f, 0f),
                new Vector3(0.45f, 1.6f, 1.25f));

            var collider = knife.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.2f, 0.65f);
            collider.isTrigger = true;
            knife.AddComponent<CakeKnife>().Initialize(this, position.y);
        }

        void CreatePlatter(Vector2 position)
        {
            var platter = new GameObject("Cake Platter");
            platter.transform.SetParent(levelRoot, false);
            platter.transform.position = position;
            platterPosition = position;

            CreatePrimitiveVisual(platter.transform, "Plate", PrimitiveType.Cylinder,
                new Color(0.86f, 0.93f, 1f), Vector3.zero,
                new Vector3(1.35f, 0.09f, 0.9f));
            CreatePrimitiveVisual(platter.transform, "Pedestal", PrimitiveType.Cylinder,
                new Color(0.67f, 0.78f, 0.92f), new Vector3(0f, -0.25f, 0f),
                new Vector3(0.55f, 0.18f, 0.5f));

            var goal = platter.AddComponent<BoxCollider2D>();
            goal.isTrigger = true;
            goal.size = new Vector2(2.5f, 1.8f);
            goal.offset = new Vector2(0f, 0.75f);
            platter.AddComponent<CakePlatterGoal>().Initialize(this);

            for (var i = 0; i < RequiredLayers; i++)
            {
                CreatePrimitiveVisual(platter.transform, $"Required Layer {i + 1}",
                    PrimitiveType.Cylinder, new Color(0.72f, 0.86f, 1f),
                    new Vector3(0f, 0.22f + i * 0.22f, 0.35f),
                    new Vector3(0.72f - i * 0.025f, 0.07f, 0.3f));
            }
        }

        public bool CollectLayer(GameObject pickup)
        {
            if (!cake.AddLayer()) return false;

            Destroy(pickup);
            ShowStatus($"Cake size: {cake.CurrentLayers}/{RequiredLayers} layers");
            return true;
        }

        public void TryTrimLayer(GameObject knife)
        {
            if (cake.CurrentLayers <= RequiredLayers)
            {
                ShowStatus("The cake already fits. No trimming needed.");
                return;
            }

            if (!cake.RemoveLayer()) return;

            CreateLayerPickup((Vector2)knife.transform.position + Vector2.up * 0.65f, true);
            ShowStatus($"Trimmed to {cake.CurrentLayers}/{RequiredLayers}. Layer dropped nearby.");
        }

        public void TryFinishLevel()
        {
            if (changingLevel) return;

            if (cake.CurrentLayers < RequiredLayers)
            {
                ShowStatus($"Too small! Collect {RequiredLayers - cake.CurrentLayers} more.");
                return;
            }

            if (cake.CurrentLayers > RequiredLayers)
            {
                ShowStatus($"Too large! Trim {cake.CurrentLayers - RequiredLayers} layer(s).");
                return;
            }

            changingLevel = true;
            player.controlEnabled = false;
            completedLevelTime = Mathf.Max(0f, Time.time - levelStartTime);
            LastLevelScore = CalculateScore(completedLevelTime);
            TotalScore += LastLevelScore;
            levelComplete = true;
            StartFireworks();
        }

        int CalculateScore(float elapsedSeconds)
        {
            var phaseScore = 10000 * CurrentPhase;
            var timePenalty = Mathf.RoundToInt(elapsedSeconds * 100f);
            return Mathf.Max(1000 * CurrentPhase, phaseScore - timePenalty);
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
            GUI.Box(new Rect(18f, 18f, 340f, 92f), string.Empty);
            GUI.Label(new Rect(32f, 27f, 310f, 22f),
                $"Phase {CurrentPhase}  Level {CurrentLevel}   " +
                $"Cake {cake?.CurrentLayers ?? 0}/{RequiredLayers}   " +
                $"Weight {player?.CurrentWeight ?? 1f:0.0}x");
            GUI.Label(new Rect(32f, 50f, 310f, 22f),
                $"Time {LevelTime:0.0}s   Total score {TotalScore:N0}");
            GUI.Label(new Rect(32f, 73f, 310f, 30f),
                Time.time < statusMessageUntil
                    ? statusMessage
                    : "Collect layers, trim excess, and fit the platter exactly.");

            if (!levelComplete) return;

            var width = Mathf.Min(680f, Screen.width - 40f);
            var height = Mathf.Min(460f, Screen.height - 40f);
            var left = (Screen.width - width) * 0.5f;
            var top = (Screen.height - height) * 0.5f;
            GUI.Box(new Rect(left, top, width, height), string.Empty);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 40,
                fontStyle = FontStyle.Bold
            };
            var centerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24
            };

            GUI.Label(new Rect(left + 30f, top + 35f, width - 60f, 58f),
                "Level Complete!", titleStyle);
            GUI.Label(new Rect(left + 30f, top + 115f, width - 60f, 42f),
                $"Phase {CurrentPhase}  -  Level {CurrentLevel}", centerStyle);
            GUI.Label(new Rect(left + 30f, top + 165f, width - 60f, 42f),
                $"Time: {completedLevelTime:0.00} seconds", centerStyle);
            GUI.Label(new Rect(left + 30f, top + 215f, width - 60f, 42f),
                $"Level score: {LastLevelScore:N0}", centerStyle);
            GUI.Label(new Rect(left + 30f, top + 265f, width - 60f, 42f),
                $"Total score: {TotalScore:N0}", centerStyle);

            if (GUI.Button(new Rect(left + 130f, top + 350f, width - 260f, 64f),
                CurrentLevel % levelsPerPhase == 0
                    ? $"Start Phase {CurrentPhase + 1}"
                    : "Next Level"))
            {
                BeginLevel(CurrentLevel + 1);
            }
        }

        void ConfigurePresentation()
        {
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.orthographic = false;
                mainCamera.fieldOfView = 40f;
            }

            var virtualCamera = FindAnyObjectByType<CinemachineCamera>();
            if (virtualCamera != null)
            {
                var lens = virtualCamera.Lens;
                lens.ModeOverride = LensSettings.OverrideModes.Perspective;
                lens.FieldOfView = 40f;
                virtualCamera.Lens = lens;
                virtualCamera.transform.rotation = Quaternion.Euler(4f, -5f, 0f);
            }

            if (FindAnyObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("2.5D Key Light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.93f, 0.86f);
                light.intensity = 1.35f;
                light.shadows = LightShadows.Soft;
                lightObject.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.48f);

            foreach (var parallaxLayer in FindObjectsByType<ParallaxLayer>(
                FindObjectsInactive.Include))
            {
                var layerName = parallaxLayer.gameObject.name;
                var depth = layerName.Contains("Far") ? 7f :
                    layerName.Contains("Foreground") ? 3f : 5f;
                parallaxLayer.SetWorldDepth(depth);
            }
        }

        GameObject CreatePrimitiveVisual(Transform parent, string objectName,
            PrimitiveType primitiveType, Color color, Vector3 localPosition, Vector3 localScale)
        {
            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = objectName;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;

            var primitiveCollider = visual.GetComponent<Collider>();
            if (primitiveCollider != null) Destroy(primitiveCollider);

            visual.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(color);
            return visual;
        }

        Material GetMaterial(Color color)
        {
            var key = (Color32)color;
            if (materials.TryGetValue(key, out var material)) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            material = new Material(shader)
            {
                name = $"Runtime 2.5D {ColorUtility.ToHtmlStringRGB(color)}",
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            materials.Add(key, material);
            return material;
        }

        void StartFireworks()
        {
            var fireworks = new GameObject("Completion Fireworks");
            fireworks.transform.SetParent(levelRoot, false);

            var colors = new[]
            {
                new Color(1f, 0.28f, 0.45f),
                new Color(1f, 0.78f, 0.16f),
                new Color(0.24f, 0.82f, 1f),
                new Color(0.66f, 0.4f, 1f),
                new Color(0.35f, 1f, 0.62f)
            };

            for (var i = 0; i < colors.Length; i++)
            {
                var burstObject = new GameObject($"Firework {i + 1}");
                burstObject.transform.SetParent(fireworks.transform, false);
                burstObject.transform.position = platterPosition +
                    new Vector3((i - 2) * 1.25f, 2.2f + (i % 2) * 1.1f, -0.8f);

                var particles = burstObject.AddComponent<ParticleSystem>();
                var main = particles.main;
                main.loop = true;
                main.duration = 1.8f;
                main.startDelay = i * 0.16f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.35f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
                main.startColor = colors[i];
                main.gravityModifier = 0.45f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 180;

                var emission = particles.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0.05f, 44)
                });

                var shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.12f;

                var colorOverLifetime = particles.colorOverLifetime;
                colorOverLifetime.enabled = true;
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                    new Gradient
                    {
                        alphaKeys = new[]
                        {
                            new GradientAlphaKey(1f, 0f),
                            new GradientAlphaKey(1f, 0.65f),
                            new GradientAlphaKey(0f, 1f)
                        },
                        colorKeys = new[]
                        {
                            new GradientColorKey(Color.white, 0f),
                            new GradientColorKey(colors[i], 1f)
                        }
                    });

                var particleRenderer = burstObject.GetComponent<ParticleSystemRenderer>();
                particleRenderer.material = GetParticleMaterial();
                particles.Play();
            }
        }

        Material GetParticleMaterial()
        {
            if (particleMaterial != null) return particleMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Standard");
            particleMaterial = new Material(shader)
            {
                name = "Runtime Firework Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            particleMaterial.SetFloat("_Surface", 1f);
            particleMaterial.SetFloat("_Blend", 1f);
            particleMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            particleMaterial.SetInt("_DstBlend", (int)BlendMode.One);
            particleMaterial.SetInt("_ZWrite", 0);
            particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particleMaterial.renderQueue = (int)RenderQueue.Transparent;
            return particleMaterial;
        }

        void OnDestroy()
        {
            foreach (var material in materials.Values)
                if (material != null) Destroy(material);
            if (particleMaterial != null) Destroy(particleMaterial);
        }
    }
}
