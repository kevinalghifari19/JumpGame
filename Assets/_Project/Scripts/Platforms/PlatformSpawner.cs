using System.Collections.Generic;
using UnityEngine;
using JumpUp.CameraSystem;
using JumpUp.Core;

namespace JumpUp.Platforms
{
    /// <summary>
    /// Procedurally generates platforms and hazards ahead of the camera and recycles them using object pooling.
    /// Implements Doodle-Jump-style spaced layouts: wide vertical gaps, alternating left-center-right distribution,
    /// and mathematically guaranteed simultaneous reachability.
    /// Compatible with Unity 6.3.
    /// </summary>
    public class PlatformSpawner : MonoBehaviour
    {
        public static PlatformSpawner Instance { get; private set; }

        [Header("Platform Prefabs")]
        [Tooltip("Prefab for standard solid platforms.")]
        [SerializeField] private GameObject standardPlatformPrefab;

        [Tooltip("Prefab for horizontally moving platforms.")]
        [SerializeField] private GameObject movingPlatformPrefab;

        [Tooltip("Prefab for fragile breakable platforms.")]
        [SerializeField] private GameObject fragilePlatformPrefab;

        [Tooltip("Prefab for spring booster platforms.")]
        [SerializeField] private GameObject springPlatformPrefab;

        [Tooltip("Fallback platform prefab if specific type prefab is null.")]
        [SerializeField] private GameObject defaultPlatformPrefab;

        [Header("Trap Prefabs")]
        [Tooltip("Prefab for hazard spike obstacles.")]
        [SerializeField] private GameObject spikeTrapPrefab;

        [Header("Spawning Settings")]
        [Tooltip("Buffer distance above the camera top edge where platforms are spawned.")]
        [SerializeField] private float spawnAheadDistance = 6.0f;

        [Tooltip("Buffer distance below camera bottom edge where platforms are despawned.")]
        [SerializeField] private float despawnDistance = 2.5f;

        [Tooltip("Horizontal padding from camera screen borders.")]
        [SerializeField] private float horizontalPadding = 0.8f;

        [Header("Spaced Vertical Gap Settings (Doodle Jump Style)")]
        [Tooltip("Vertical gap range in starting area (< 35m). Spaced between 2.0 and 2.3.")]
        [SerializeField] private Vector2 earlyGapRange = new Vector2(2.0f, 2.3f);

        [Tooltip("Vertical gap range in mid area (35m - 100m). Moderately spaced between 2.2 and 2.6.")]
        [SerializeField] private Vector2 midGapRange = new Vector2(2.2f, 2.6f);

        [Tooltip("Vertical gap range in high area (> 100m). Challenging between 2.4 and 2.8 (max jump is ~3.18m).")]
        [SerializeField] private Vector2 highGapRange = new Vector2(2.4f, 2.8f);

        [Header("Player Physics Calibration Constants")]
        [Tooltip("Player jump initial velocity (PlayerController defaultJumpForce = 12.5).")]
        [SerializeField] private float playerJumpForce = 12.5f;

        [Tooltip("Player effective gravity (gravityScale 2.5 * 9.81 = 24.525).")]
        [SerializeField] private float playerEffectiveGravity = 24.525f;

        [Tooltip("Player max horizontal speed.")]
        [SerializeField] private float playerMoveSpeed = 8.5f;

        [Tooltip("Player horizontal acceleration.")]
        [SerializeField] private float playerAcceleration = 25f;

        [Header("Trap Settings")]
        [Tooltip("Minimum platforms spawned between two consecutive traps to avoid impassable walls.")]
        [SerializeField] private int minPlatformGapBetweenTraps = 4;

        [Header("Pool Configuration")]
        [SerializeField] private int initialPoolSize = 25;
        [SerializeField] private int initialTrapPoolSize = 10;

        private readonly List<Platform> activePlatforms = new List<Platform>();
        private readonly Dictionary<PlatformType, Queue<Platform>> typePools = new Dictionary<PlatformType, Queue<Platform>>();

        private readonly List<HazardTrap> activeTraps = new List<HazardTrap>();
        private readonly Queue<HazardTrap> trapPool = new Queue<HazardTrap>();

        private float highestSpawnedY;
        private float lastSpawnedX;
        private int platformsSinceLastTrap = 10;
        private Transform poolParent;

        public void SetPrefabs(GameObject standard, GameObject moving, GameObject fragile, GameObject spring, GameObject fallback = null, GameObject spikeTrap = null)
        {
            standardPlatformPrefab = standard;
            movingPlatformPrefab = moving;
            fragilePlatformPrefab = fragile;
            springPlatformPrefab = spring;
            defaultPlatformPrefab = fallback != null ? fallback : standard;
            if (spikeTrap != null) spikeTrapPrefab = spikeTrap;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            GameObject poolObj = new GameObject("[PlatformPool]");
            poolObj.transform.SetParent(transform);
            poolParent = poolObj.transform;
        }

        private void Start()
        {
            InitializePool();
            SpawnStartingPlatforms();
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.GameOver)
            {
                return;
            }

            CheckAndSpawnAhead();
            CheckAndRecyclePlatforms();
        }

        private void InitializePool()
        {
            typePools[PlatformType.Standard] = new Queue<Platform>();
            typePools[PlatformType.Moving] = new Queue<Platform>();
            typePools[PlatformType.Fragile] = new Queue<Platform>();
            typePools[PlatformType.Spring] = new Queue<Platform>();

            int standardCount = Mathf.RoundToInt(initialPoolSize * 0.6f);
            int movingCount = Mathf.RoundToInt(initialPoolSize * 0.2f);
            int fragileCount = Mathf.RoundToInt(initialPoolSize * 0.1f);
            int springCount = Mathf.RoundToInt(initialPoolSize * 0.1f);

            PrewarmPool(PlatformType.Standard, standardCount);
            PrewarmPool(PlatformType.Moving, movingCount);
            PrewarmPool(PlatformType.Fragile, fragileCount);
            PrewarmPool(PlatformType.Spring, springCount);

            // Prewarm trap pool
            for (int i = 0; i < initialTrapPoolSize; i++)
            {
                HazardTrap trap = CreateNewTrapInstance();
                trap.ResetTrap();
                trapPool.Enqueue(trap);
            }
        }

        private void PrewarmPool(PlatformType type, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Platform plat = CreateNewPlatformInstance(type);
                plat.ResetPlatform();
                typePools[type].Enqueue(plat);
            }
        }

        private GameObject GetPrefabForType(PlatformType type)
        {
            switch (type)
            {
                case PlatformType.Standard:
                    return standardPlatformPrefab != null ? standardPlatformPrefab : defaultPlatformPrefab;
                case PlatformType.Moving:
                    return movingPlatformPrefab != null ? movingPlatformPrefab : defaultPlatformPrefab;
                case PlatformType.Fragile:
                    return fragilePlatformPrefab != null ? fragilePlatformPrefab : defaultPlatformPrefab;
                case PlatformType.Spring:
                    return springPlatformPrefab != null ? springPlatformPrefab : defaultPlatformPrefab;
                default:
                    return defaultPlatformPrefab;
            }
        }

        private Platform CreateNewPlatformInstance(PlatformType type)
        {
            GameObject prefab = GetPrefabForType(type);
            GameObject obj;

            if (prefab != null)
            {
                obj = Instantiate(prefab, poolParent);
            }
            else
            {
                obj = new GameObject($"Platform_{type}_Runtime");
                obj.transform.SetParent(poolParent);

                SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDefaultPlatformSprite();
                sr.sortingOrder = 1;

                BoxCollider2D bc = obj.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(2.0f, 0.45f);

                obj.AddComponent<Platform>();
            }

            Platform platformComp = obj.GetComponent<Platform>();
            if (platformComp == null)
            {
                platformComp = obj.AddComponent<Platform>();
            }

            return platformComp;
        }

        private HazardTrap CreateNewTrapInstance()
        {
            GameObject obj;
            if (spikeTrapPrefab != null)
            {
                obj = Instantiate(spikeTrapPrefab, poolParent);
            }
            else
            {
                obj = new GameObject("SpikeTrap_Runtime");
                obj.transform.SetParent(poolParent);

                SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.9f, 0.2f, 0.2f, 1f);
                sr.sortingOrder = 3;

                BoxCollider2D bc = obj.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(0.85f, 0.75f);
                bc.isTrigger = true;

                obj.AddComponent<HazardTrap>();
            }

            HazardTrap trapComp = obj.GetComponent<HazardTrap>();
            if (trapComp == null)
            {
                trapComp = obj.AddComponent<HazardTrap>();
            }

            return trapComp;
        }

        private Sprite CreateDefaultPlatformSprite()
        {
            int width = 64;
            int height = 16;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool border = (x == 0 || x == width - 1 || y == 0 || y == height - 1);
                    colors[y * width + x] = border ? new Color(0.9f, 0.9f, 0.9f, 1f) : Color.white;
                }
            }

            texture.SetPixels(colors);
            texture.filterMode = FilterMode.Point;
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
        }

        private void SpawnStartingPlatforms()
        {
            // P1: Starting platform placed right under player start
            SpawnPlatform(new Vector3(0f, -0.6f, 0f), PlatformType.Standard);
            highestSpawnedY = -0.6f;
            lastSpawnedX = 0f;
            platformsSinceLastTrap = 10;

            // Pre-spawn platforms up to camera top + buffer
            float targetTop = 10.0f;
            if (CameraFollow.Instance != null)
            {
                targetTop = CameraFollow.Instance.TopBoundY + spawnAheadDistance;
            }

            while (highestSpawnedY < targetTop)
            {
                SpawnNextProceduralPlatform();
            }
        }

        private void CheckAndSpawnAhead()
        {
            float targetTopY = 10.0f;

            if (CameraFollow.Instance != null)
            {
                targetTopY = CameraFollow.Instance.TopBoundY + spawnAheadDistance;
            }

            while (highestSpawnedY < targetTopY)
            {
                SpawnNextProceduralPlatform();
            }
        }

        /// <summary>
        /// Calculates vertical gap range based on player height / progression.
        /// Early: 2.0 - 2.3 | Mid: 2.2 - 2.6 | High: 2.4 - 2.8
        /// </summary>
        private Vector2 GetVerticalGapRange(float height, PlatformType type)
        {
            Vector2 range;
            if (height < 35f)
            {
                range = earlyGapRange;
            }
            else if (height < 100f)
            {
                float t = (height - 35f) / 65f;
                range = Vector2.Lerp(earlyGapRange, midGapRange, t);
            }
            else
            {
                float t = Mathf.Clamp01((height - 100f) / 100f);
                range = Vector2.Lerp(midGapRange, highGapRange, t);
            }

            // Moving platforms oscillate dynamically across the screen.
            // Cap their vertical gap slightly (max 2.45m) to ensure a comfortable landing window even at turn-around points.
            if (type == PlatformType.Moving)
            {
                range.y = Mathf.Min(range.y, 2.45f);
            }

            return range;
        }

        /// <summary>
        /// Calculates the next horizontal position using a widely spaced alternating left-center-right distribution
        /// with simultaneous physics-based reachability guarantee and multi-candidate solver.
        /// </summary>
        private float CalculateNextHorizontalPosition(float leftBound, float rightBound, float gapY)
        {
            // 1. Determine zone of previous platform
            // Left: < -0.55 | Center: [-0.55, 0.55] | Right: > 0.55
            int lastZone; // -1 = Left, 0 = Center, 1 = Right
            if (lastSpawnedX < -0.55f) lastZone = -1;
            else if (lastSpawnedX > 0.55f) lastZone = 1;
            else lastZone = 0;

            // 2. Physical Reachability Limit Calculation:
            // Calculate air time before player falls past gapY
            float v0 = playerJumpForce;
            float g = playerEffectiveGravity;
            float discriminant = (v0 * v0) - (2f * g * gapY);

            // Discriminant is positive as max theoretical jump is ~3.18m and max gap is 2.8m
            float airTime = (v0 + Mathf.Sqrt(Mathf.Max(0.01f, discriminant))) / g;

            // Compute maximum horizontal displacement during airTime considering acceleration
            float tAccel = Mathf.Min(airTime, playerMoveSpeed / playerAcceleration);
            float dAccel = 0.5f * playerAcceleration * tAccel * tAccel;
            float dCruise = playerMoveSpeed * Mathf.Max(0f, airTime - tAccel);
            float maxPhysicalTravel = dAccel + dCruise;

            // Apply 75% safety factor + 0.85m platform half-width tolerance so human reaction timing is comfortable
            float maxSafeHorizontalDelta = (maxPhysicalTravel * 0.75f) + 0.85f;

            // 3. Multi-candidate solver: try wide alternating zones first
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int nextZone;
                float roll = Random.value;

                if (lastZone == -1)
                {
                    // From Left: 70% jump to Right, 20% jump to Center, 10% stay Left with offset
                    if (roll < 0.70f) nextZone = 1;
                    else if (roll < 0.90f) nextZone = 0;
                    else nextZone = -1;
                }
                else if (lastZone == 1)
                {
                    // From Right: 70% jump to Left, 20% jump to Center, 10% stay Right with offset
                    if (roll < 0.70f) nextZone = -1;
                    else if (roll < 0.90f) nextZone = 0;
                    else nextZone = 1;
                }
                else
                {
                    // From Center: 50% jump to Left, 50% jump to Right
                    nextZone = (roll < 0.50f) ? -1 : 1;
                }

                // If later attempts, fallback towards center or closer zone
                if (attempt >= 3)
                {
                    nextZone = 0; // Try center zone to reduce horizontal distance
                }

                float candidateX;
                if (nextZone == -1)
                {
                    candidateX = Random.Range(-1.90f, -0.75f);
                }
                else if (nextZone == 1)
                {
                    candidateX = Random.Range(0.75f, 1.90f);
                }
                else
                {
                    candidateX = Random.Range(-0.40f, 0.40f);
                }

                // Prevent stacking right on top of previous platform
                if (Mathf.Abs(candidateX - lastSpawnedX) < 0.65f)
                {
                    candidateX = (lastSpawnedX >= 0f) ? candidateX - 0.75f : candidateX + 0.75f;
                }

                candidateX = Mathf.Clamp(candidateX, leftBound, rightBound);

                // Check physical reachability
                float deltaX = Mathf.Abs(candidateX - lastSpawnedX);
                if (deltaX <= maxSafeHorizontalDelta)
                {
                    return candidateX;
                }
            }

            // Fallback: place smoothly at maximum safe reach towards target direction
            float fallbackDir = (lastSpawnedX >= 0f) ? -1f : 1f;
            float fallbackX = Mathf.Clamp(lastSpawnedX + (fallbackDir * maxSafeHorizontalDelta * 0.9f), leftBound, rightBound);
            return fallbackX;
        }

        private void SpawnNextProceduralPlatform()
        {
            // 1. Select platform type with progressive difficulty first
            PlatformType selectedType = SelectPlatformTypeForHeight(highestSpawnedY);

            // 2. Calculate vertical gap within specified wide target range
            Vector2 gapRange = GetVerticalGapRange(highestSpawnedY, selectedType);
            float gap = Random.Range(gapRange.x, gapRange.y);
            highestSpawnedY += gap;

            // 3. Determine camera boundaries
            float left = -2.0f;
            float right = 2.0f;

            if (CameraFollow.Instance != null)
            {
                left = CameraFollow.Instance.LeftBoundX + horizontalPadding;
                right = CameraFollow.Instance.RightBoundX - horizontalPadding;
            }

            // 4. Compute widely spaced alternating horizontal placement with simultaneous reachability
            float newX = CalculateNextHorizontalPosition(left, right, gap);
            lastSpawnedX = newX;

            Vector3 platformPos = new Vector3(newX, highestSpawnedY, 0f);

            // 5. Spawn platform & track trap frequency
            SpawnPlatform(platformPos, selectedType);
            platformsSinceLastTrap++;

            // 6. Occasionally spawn hazard spikes in a fair, non-blocking position
            MaybeSpawnTrap(platformPos, selectedType, highestSpawnedY);
        }

        private PlatformType SelectPlatformTypeForHeight(float height)
        {
            float roll = Random.value;

            if (height < 35f)
            {
                // Early zone: standard platforms with occasional springs
                if (roll < 0.10f) return PlatformType.Spring;
                if (roll < 0.15f) return PlatformType.Moving;
                return PlatformType.Standard;
            }
            else if (height < 100f)
            {
                // Mid zone: moving and fragile platforms become more common
                if (roll < 0.10f) return PlatformType.Spring;
                if (roll < 0.35f) return PlatformType.Moving;
                if (roll < 0.50f) return PlatformType.Fragile;
                return PlatformType.Standard;
            }
            else
            {
                // High zone: full dynamic challenge combining all types
                if (roll < 0.10f) return PlatformType.Spring;
                if (roll < 0.40f) return PlatformType.Moving;
                if (roll < 0.65f) return PlatformType.Fragile;
                return PlatformType.Standard;
            }
        }

        /// <summary>
        /// Spawns a trap obstacle on a side of the platform while preserving a clear landing zone.
        /// </summary>
        private void MaybeSpawnTrap(Vector3 platformPosition, PlatformType type, float height)
        {
            // No traps in early zone (< 35m)
            // Never place traps on Fragile platforms (already risky on their own)
            // Ensure minimum platform separation between traps
            if (height < 35f || type == PlatformType.Fragile || platformsSinceLastTrap < minPlatformGapBetweenTraps)
            {
                return;
            }

            float trapChance = height < 100f ? 0.08f : 0.14f;
            if (Random.value > trapChance)
            {
                return;
            }

            // Platform width is 2.0 (-1.0 to +1.0).
            // Place spike on either the left edge (-0.65) or right edge (+0.65).
            // This leaves > 1.3 units of guaranteed clear landing surface on the opposite side.
            float spikeOffsetSide = Random.value > 0.5f ? 0.65f : -0.65f;
            Vector3 spikePos = new Vector3(platformPosition.x + spikeOffsetSide, platformPosition.y + 0.38f, 0f);

            SpawnTrap(spikePos);
            platformsSinceLastTrap = 0;
        }

        private void SpawnPlatform(Vector3 position, PlatformType type)
        {
            if (!typePools.ContainsKey(type))
            {
                typePools[type] = new Queue<Platform>();
            }

            Platform platform = typePools[type].Count > 0 ? typePools[type].Dequeue() : CreateNewPlatformInstance(type);
            platform.Configure(type, position);
            activePlatforms.Add(platform);
        }

        private void SpawnTrap(Vector3 position)
        {
            HazardTrap trap = trapPool.Count > 0 ? trapPool.Dequeue() : CreateNewTrapInstance();
            trap.Configure(position);
            activeTraps.Add(trap);
        }

        private void CheckAndRecyclePlatforms()
        {
            float despawnY = -10f;

            if (CameraFollow.Instance != null)
            {
                despawnY = CameraFollow.Instance.BottomBoundY - despawnDistance;
            }

            // Recycle off-screen platforms
            for (int i = activePlatforms.Count - 1; i >= 0; i--)
            {
                Platform plat = activePlatforms[i];
                if (plat.transform.position.y < despawnY || !plat.gameObject.activeSelf)
                {
                    activePlatforms.RemoveAt(i);
                    plat.ResetPlatform();
                    if (!typePools.ContainsKey(plat.Type))
                    {
                        typePools[plat.Type] = new Queue<Platform>();
                    }
                    typePools[plat.Type].Enqueue(plat);
                }
            }

            // Recycle off-screen hazard traps
            for (int i = activeTraps.Count - 1; i >= 0; i--)
            {
                HazardTrap trap = activeTraps[i];
                if (trap.transform.position.y < despawnY || !trap.gameObject.activeSelf)
                {
                    activeTraps.RemoveAt(i);
                    trap.ResetTrap();
                    trapPool.Enqueue(trap);
                }
            }
        }

        public void ResetAllPlatforms()
        {
            for (int i = activePlatforms.Count - 1; i >= 0; i--)
            {
                Platform plat = activePlatforms[i];
                plat.ResetPlatform();
                if (!typePools.ContainsKey(plat.Type))
                {
                    typePools[plat.Type] = new Queue<Platform>();
                }
                typePools[plat.Type].Enqueue(plat);
            }
            activePlatforms.Clear();

            for (int i = activeTraps.Count - 1; i >= 0; i--)
            {
                HazardTrap trap = activeTraps[i];
                trap.ResetTrap();
                trapPool.Enqueue(trap);
            }
            activeTraps.Clear();

            highestSpawnedY = -0.6f;
            lastSpawnedX = 0f;
            platformsSinceLastTrap = 10;
        }
    }
}
