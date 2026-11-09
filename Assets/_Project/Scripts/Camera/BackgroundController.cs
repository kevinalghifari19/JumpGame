using UnityEngine;

namespace JumpUp.CameraSystem
{
    /// <summary>
    /// Infinite vertical parallax background for JUMP UP!
    ///
    /// Design
    /// ──────
    /// Two sprite tiles are repositioned analytically every frame so they always cover
    /// the camera viewport — no accumulation drift, no gaps, no empty edges ever.
    ///
    /// Parallax model
    /// ──────────────
    /// A "virtual background origin" scrolls slower than the camera:
    ///
    ///     bgOriginY = startBgY + (camY - startCamY) * parallaxFactor
    ///
    /// parallaxFactor = 0  → background is pinned to world (fully static)
    /// parallaxFactor = 1  → background moves 1:1 with camera (no parallax effect)
    /// parallaxFactor = 0.3 → background moves 30% as fast → sky drifts slowly upward
    ///
    /// Tile placement
    /// ──────────────
    /// Given bgOriginY and tileWorldHeight we compute which repeating tile row the
    /// bottom of the camera currently sits in, then place:
    ///   TileA = that row's tile (bottom of stack)
    ///   TileB = one tile above TileA
    ///
    /// Two stacked tiles are always taller than the viewport, so the sky is always covered.
    /// TileB uses flipX=true so the moon appears on the opposite side — reduces obvious
    /// repetition without requiring a second sprite asset.
    ///
    /// Portrait-aware · Unity 6.3 LTS / URP 2D · zero physics/gameplay side-effects.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(20)]   // runs after CameraFollow (default order -50)
    public class BackgroundController : MonoBehaviour
    {
        // ── Inspector fields ──────────────────────────────────────────────────

        [Header("Sprite")]
        [Tooltip("Night-sky sprite to use as background. Assigned from the scene.")]
        [SerializeField] private Sprite backgroundSprite;

        [Header("Parallax")]
        [Tooltip(
            "How much the background moves relative to camera.\n" +
            "0 = fully pinned to world (static).\n" +
            "1 = moves 1:1 with camera (no depth effect).\n" +
            "0.2–0.4 gives a beautiful slow-drifting sky feel.")]
        [Range(0f, 1f)]
        [SerializeField] private float parallaxFactor = 0.3f;

        [Header("Coverage")]
        [Tooltip("Scale margin added on top of exact-fit scale. 1.02 = 2% safety bleed.")]
        [Range(1f, 1.3f)]
        [SerializeField] private float coverageMargin = 1.02f;

        [Header("Sorting")]
        [Tooltip("Render behind all gameplay objects. Must be lower than platform/player sorting order.")]
        [SerializeField] private int sortingOrder = -100;

        // ── Runtime state ─────────────────────────────────────────────────────

        private Camera       cam;
        private Transform    tileA;
        private Transform    tileB;
        private SpriteRenderer rendA;
        private SpriteRenderer rendB;

        /// <summary>World-unit height of one scaled tile.</summary>
        private float tileWorldHeight;

        /// <summary>Camera Y at the moment Build() ran — parallax reference.</summary>
        private float startCamY;

        /// <summary>Background origin world Y at the moment Build() ran.</summary>
        private float startBgY;

        /// <summary>Uniform scale applied to both tiles.</summary>
        private float tileScale;

        // ── Singleton ─────────────────────────────────────────────────────────
        public static BackgroundController Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Build();
        }

        private void LateUpdate()
        {
            UpdateTilePositions();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) Build();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) Build();
        }
#endif

        // ─────────────────────────────────────────────────────────────────────
        // Setup
        // ─────────────────────────────────────────────────────────────────────

        private void Build()
        {
            ResolveCamera();
            ResolveSprite();
            DestroyOldTiles();

            if (cam == null || backgroundSprite == null) return;

            tileScale       = ComputeUniformScale();
            tileWorldHeight = (backgroundSprite.rect.height / backgroundSprite.pixelsPerUnit) * tileScale;

            tileA = CreateTile("BG_TileA", tileScale, flipX: false);
            tileB = CreateTile("BG_TileB", tileScale, flipX: true);   // mirrored to vary moon position

            startCamY = cam.transform.position.y;
            startBgY  = startCamY;   // background origin starts at camera position

            // Do an immediate placement so tiles are correct before first rendered frame
            PlaceTiles(startCamY);
        }

        private void ResolveCamera()
        {
            if (CameraFollow.Instance != null && CameraFollow.Instance.Cam != null)
                cam = CameraFollow.Instance.Cam;
            else
                cam = Camera.main;
        }

        private void ResolveSprite()
        {
            if (backgroundSprite == null)
            {
                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                if (sr != null) backgroundSprite = sr.sprite;
            }
        }

        private void DestroyOldTiles()
        {
            // Destroy children named BG_TileA / BG_TileB
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c.name == "BG_TileA" || c.name == "BG_TileB")
                {
                    if (Application.isPlaying) Destroy(c.gameObject);
                    else                       DestroyImmediate(c.gameObject);
                }
            }

            // Also sweep for any scene-level orphans left by earlier builds
            SweepOrphan("BG_TileA");
            SweepOrphan("BG_TileB");

            tileA = tileB = null;
            rendA = rendB = null;
        }

        private void SweepOrphan(string tileName)
        {
            GameObject go = GameObject.Find(tileName);
            if (go != null)
            {
                if (Application.isPlaying) Destroy(go);
                else                       DestroyImmediate(go);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Scale computation
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the uniform scale that makes the sprite cover the full camera viewport.
        /// Uses Mathf.Max(scaleH, scaleW) so the larger axis always fills — aspect preserved, cropped.
        /// </summary>
        private float ComputeUniformScale()
        {
            float orthoSize = cam.orthographic ? cam.orthographicSize : 5f;
            float viewH     = orthoSize * 2f;
            float viewW     = viewH * cam.aspect;

            float spriteH   = backgroundSprite.rect.height / backgroundSprite.pixelsPerUnit;
            float spriteW   = backgroundSprite.rect.width  / backgroundSprite.pixelsPerUnit;

            if (spriteH < 0.0001f) spriteH = 1f;
            if (spriteW < 0.0001f) spriteW = 1f;

            float scaleH = viewH / spriteH;
            float scaleW = viewW / spriteW;

            // coverageMargin adds a small safety bleed so sub-pixel gaps never appear
            return Mathf.Max(scaleH, scaleW) * coverageMargin;
        }

        private Transform CreateTile(string tileName, float scale, bool flipX)
        {
            GameObject go = new GameObject(tileName);
            go.transform.SetParent(transform, worldPositionStays: false);

            SpriteRenderer sr   = go.AddComponent<SpriteRenderer>();
            sr.sprite            = backgroundSprite;
            sr.sortingOrder      = sortingOrder;
            sr.maskInteraction   = SpriteMaskInteraction.None;
            sr.drawMode          = SpriteDrawMode.Simple;
            sr.flipX             = flipX;

            go.transform.localScale = new Vector3(scale, scale, 1f);

            if (tileName == "BG_TileA") rendA = sr;
            else                        rendB = sr;

            return go.transform;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Per-frame update
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateTilePositions()
        {
            if (cam == null || tileA == null || tileB == null) return;

            float camY = cam.transform.position.y;

            // Rebuild camera reference if cam moved before Build() ran (edge case in Editor)
            if (!Application.isPlaying && tileWorldHeight < 0.001f)
            {
                Build();
                return;
            }

            PlaceTiles(camY);
        }

        /// <summary>
        /// Analytically computes where both tiles must sit so they cover the camera viewport.
        /// No delta accumulation → no drift error possible, even at score 10 000+.
        /// </summary>
        private void PlaceTiles(float camY)
        {
            if (tileWorldHeight < 0.001f) return;

            // ── 1.  Background virtual origin ─────────────────────────────────
            // The background scrolls at parallaxFactor of the camera's total ascent.
            // This is computed fresh every frame from startCamY — zero drift over time.
            float totalCamDelta = camY - startCamY;
            float bgOriginY     = startBgY + totalCamDelta * parallaxFactor;

            // ── 2.  Which repeating tile row covers the camera bottom? ─────────
            // "Row index" of the camera's bottom edge relative to bgOriginY.
            float camBottom   = camY - cam.orthographicSize - 0.5f;   // 0.5 safety below
            float rowFloat    = (camBottom - bgOriginY) / tileWorldHeight;
            int   rowIndex    = Mathf.FloorToInt(rowFloat);

            // ── 3.  Place TileA at that row, TileB one row above ──────────────
            float tileAY = bgOriginY + rowIndex * tileWorldHeight;
            float tileBY = tileAY + tileWorldHeight;

            // Keep all background tiles at z = 1  (camera at z = -10, gameplay at z = 0)
            tileA.position = new Vector3(0f, tileAY, 1f);
            tileB.position = new Vector3(0f, tileBY, 1f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resets the parallax anchor to the current camera position.
        /// Call this when restarting the game so the background origin is re-synced.
        /// </summary>
        public void ResetToCamera()
        {
            if (cam == null) ResolveCamera();
            if (cam == null) return;

            startCamY = cam.transform.position.y;
            startBgY  = startCamY;

            PlaceTiles(startCamY);
        }
    }
}
