#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using JumpUp.Core;
using JumpUp.Player;
using JumpUp.CameraSystem;
using JumpUp.Platforms;
using JumpUp.UI;

namespace JumpUp.EditorTools
{
    /// <summary>
    /// Automated builder that sets up sprites, platform prefabs, player prefab, UI, and the playable GameScene.
    /// Compatible with Unity 6.3.
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string SpritesPath = "Assets/_Project/Sprites";
        private const string PrefabsPath = "Assets/_Project/Prefabs";
        private const string ScenesPath = "Assets/_Project/Scenes";
        private const string GameSceneFile = "Assets/_Project/Scenes/GameScene.unity";

        [MenuItem("Jump Up/Build Game Scene and Prefabs")]
        public static void BuildGameScene()
        {
            Debug.Log("[GameSceneBuilder] Starting JUMP UP! automated project setup...");

            EnsureDirectories();
            GenerateSprites();
            CreatePlatformPrefabs(out GameObject standardPrefab, out GameObject movingPrefab, out GameObject fragilePrefab, out GameObject springPrefab);
            GameObject playerPrefab = CreatePlayerPrefab();

            AssembleScene(playerPrefab, standardPrefab, movingPrefab, fragilePrefab, springPrefab);
            RegisterBuildScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GameSceneBuilder] Project build complete! Playable scene saved to: " + GameSceneFile);
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(SpritesPath)) Directory.CreateDirectory(SpritesPath);
            if (!Directory.Exists(PrefabsPath)) Directory.CreateDirectory(PrefabsPath);
            if (!Directory.Exists(ScenesPath)) Directory.CreateDirectory(ScenesPath);
            AssetDatabase.Refresh();
        }

        #region Sprite Generation

        private static void GenerateSprites()
        {
            CreatePlayerSprite(Path.Combine(SpritesPath, "PlayerSprite.png"));
            CreatePlatformSprite(Path.Combine(SpritesPath, "StandardPlatform.png"), new Color(0.2f, 0.8f, 0.3f), new Color(0.12f, 0.6f, 0.2f), "standard");
            CreatePlatformSprite(Path.Combine(SpritesPath, "MovingPlatform.png"), new Color(0.2f, 0.65f, 0.95f), new Color(0.12f, 0.45f, 0.8f), "moving");
            CreatePlatformSprite(Path.Combine(SpritesPath, "FragilePlatform.png"), new Color(0.85f, 0.45f, 0.2f), new Color(0.6f, 0.3f, 0.12f), "fragile");
            CreatePlatformSprite(Path.Combine(SpritesPath, "SpringPlatform.png"), new Color(0.95f, 0.75f, 0.15f), new Color(0.8f, 0.55f, 0.08f), "spring");

            AssetDatabase.Refresh();

            ConfigureTextureImporter(Path.Combine(SpritesPath, "PlayerSprite.png"), 32);
            ConfigureTextureImporter(Path.Combine(SpritesPath, "StandardPlatform.png"), 32);
            ConfigureTextureImporter(Path.Combine(SpritesPath, "MovingPlatform.png"), 32);
            ConfigureTextureImporter(Path.Combine(SpritesPath, "FragilePlatform.png"), 32);
            ConfigureTextureImporter(Path.Combine(SpritesPath, "SpringPlatform.png"), 32);
        }

        private static void ConfigureTextureImporter(string path, int ppu)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = ppu;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void CreatePlayerSprite(string path)
        {
            int w = 32, h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - 15.5f) / 14f;
                    float dy = (y - 15.5f) / 14f;
                    float distSq = dx * dx + dy * dy;

                    if (distSq <= 1f)
                    {
                        // Body gradient (Bright cute Cyan/Teal)
                        Color body = Color.Lerp(new Color(0.1f, 0.75f, 0.85f), new Color(0.3f, 0.95f, 0.95f), (y / 32f));

                        // Cheeks
                        if (y >= 10 && y <= 13 && (x == 7 || x == 8 || x == 23 || x == 24))
                        {
                            body = new Color(0.95f, 0.45f, 0.6f);
                        }

                        // Eyes (Left eye: x ~ 10-13, y ~ 16-22; Right eye: x ~ 18-21, y ~ 16-22)
                        bool isLeftEye = (x >= 10 && x <= 13 && y >= 16 && y <= 22);
                        bool isRightEye = (x >= 18 && x <= 21 && y >= 16 && y <= 22);

                        if (isLeftEye || isRightEye)
                        {
                            body = Color.white;
                            // Pupils looking up/right
                            if ((x == 12 || x == 13) && (y >= 18 && y <= 21) && isLeftEye) body = new Color(0.1f, 0.1f, 0.2f);
                            if ((x == 20 || x == 21) && (y >= 18 && y <= 21) && isRightEye) body = new Color(0.1f, 0.1f, 0.2f);
                        }

                        // Cute smile (y ~ 9, x ~ 13-18)
                        if (y == 9 && x >= 13 && x <= 18) body = new Color(0.1f, 0.2f, 0.3f);
                        if (y == 10 && (x == 12 || x == 19)) body = new Color(0.1f, 0.2f, 0.3f);

                        // Highlight
                        if (x >= 8 && x <= 12 && y >= 24 && y <= 27) body = Color.white;

                        tex.SetPixel(x, y, body);
                    }
                    else
                    {
                        tex.SetPixel(x, y, transparent);
                    }
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void CreatePlatformSprite(string path, Color mainColor, Color borderColor, string type)
        {
            int w = 64, h = 18;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Pill shape mask
                    bool inside = false;
                    float radius = 7f;
                    float centerY = 8.5f;

                    if (x < radius)
                    {
                        float dx = x - radius;
                        float dy = y - centerY;
                        inside = (dx * dx + dy * dy) <= (radius * radius);
                    }
                    else if (x > w - 1 - radius)
                    {
                        float dx = x - (w - 1 - radius);
                        float dy = y - centerY;
                        inside = (dx * dx + dy * dy) <= (radius * radius);
                    }
                    else
                    {
                        inside = (y >= 1 && y <= 16);
                    }

                    if (inside)
                    {
                        bool isBorder = (y <= 2 || y >= 15 || x <= 2 || x >= w - 3);
                        Color c = isBorder ? borderColor : mainColor;

                        // Highlight on top surface
                        if (y >= 13 && y <= 14 && x >= 8 && x <= w - 9)
                        {
                            c = Color.Lerp(mainColor, Color.white, 0.45f);
                        }

                        // Specific decorations
                        if (type == "moving" && (y >= 7 && y <= 9))
                        {
                            // Direction arrows in center
                            if ((x >= 28 && x <= 30) || (x >= 34 && x <= 36))
                            {
                                c = Color.white;
                            }
                        }
                        else if (type == "fragile")
                        {
                            // Jagged crack in middle
                            if ((x == 32 && y >= 3 && y <= 7) || (x == 31 && y >= 7 && y <= 11) || (x == 33 && y >= 11 && y <= 15))
                            {
                                c = new Color(0.2f, 0.1f, 0.05f);
                            }
                        }
                        else if (type == "spring")
                        {
                            // Gold/Red spring marker
                            if (x >= 28 && x <= 36 && y >= 5 && y <= 12)
                            {
                                c = (y % 2 == 0) ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.95f, 0.9f, 0.3f);
                            }
                        }

                        tex.SetPixel(x, y, c);
                    }
                    else
                    {
                        tex.SetPixel(x, y, transparent);
                    }
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        #endregion

        #region Prefab Creation

        private static void CreatePlatformPrefabs(out GameObject standard, out GameObject moving, out GameObject fragile, out GameObject spring)
        {
            standard = BuildSinglePlatformPrefab("StandardPlatform", PlatformType.Standard, 1.0f, Path.Combine(SpritesPath, "StandardPlatform.png"));
            moving = BuildSinglePlatformPrefab("MovingPlatform", PlatformType.Moving, 1.0f, Path.Combine(SpritesPath, "MovingPlatform.png"));
            fragile = BuildSinglePlatformPrefab("FragilePlatform", PlatformType.Fragile, 1.0f, Path.Combine(SpritesPath, "FragilePlatform.png"));
            spring = BuildSinglePlatformPrefab("SpringPlatform", PlatformType.Spring, 1.9f, Path.Combine(SpritesPath, "SpringPlatform.png"));
        }

        private static GameObject BuildSinglePlatformPrefab(string name, PlatformType type, float bounceMult, string spritePath)
        {
            string prefabPath = $"{PrefabsPath}/{name}.prefab";

            GameObject tempObj = new GameObject(name);
            SpriteRenderer sr = tempObj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            sr.sortingOrder = 2;

            BoxCollider2D col = tempObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2.0f, 0.45f);
            col.isTrigger = true; // One-way arcade feel: pass through upwards freely, bounce when falling

            Platform plat = tempObj.AddComponent<Platform>();
            plat.Configure(type, Vector3.zero);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempObj, prefabPath);
            Object.DestroyImmediate(tempObj);

            return prefab;
        }

        private static GameObject CreatePlayerPrefab()
        {
            string prefabPath = $"{PrefabsPath}/Player.prefab";

            GameObject tempObj = new GameObject("Player");
            tempObj.tag = "Player";

            SpriteRenderer sr = tempObj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritesPath, "PlayerSprite.png"));
            sr.sortingOrder = 5;

            CircleCollider2D col = tempObj.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;

            Rigidbody2D rb = tempObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 2.5f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            tempObj.AddComponent<PlayerController>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempObj, prefabPath);
            Object.DestroyImmediate(tempObj);

            return prefab;
        }

        #endregion

        #region Scene Assembly

        private static void AssembleScene(GameObject playerPrefab, GameObject stdPrefab, GameObject movPrefab, GameObject fraPrefab, GameObject sprPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera & CameraFollow
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 0f, -10f);

            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.11f, 0.18f); // Midnight blue

            camObj.AddComponent<AudioListener>();
            var urpCam = camObj.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = false;

            CameraFollow camFollow = camObj.AddComponent<CameraFollow>();

            // 2. Global Light 2D
            GameObject lightObj = new GameObject("Global Light 2D");
            var light2d = lightObj.AddComponent<Light2D>();
            light2d.lightType = Light2D.LightType.Global;
            light2d.color = Color.white;
            light2d.intensity = 1.0f;

            // 2b. Background Night Sky
            GameObject bgObj = new GameObject("Background_NightSky");
            SpriteRenderer bgSr = bgObj.AddComponent<SpriteRenderer>();
            bgSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Background/Background_NightSky.png");
            bgSr.sortingOrder = -100;

            // 3. Player Instance
            GameObject playerObj = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerObj.name = "Player";
            playerObj.transform.position = new Vector3(0f, 0f, 0f);

            camFollow.SetTarget(playerObj.transform);

            // 4. Managers
            GameObject managersObj = new GameObject("[Managers]");
            managersObj.AddComponent<GameManager>();
            managersObj.AddComponent<ScoreManager>();

            // 5. Platform Spawner
            GameObject spawnerObj = new GameObject("[PlatformSpawner]");
            PlatformSpawner spawner = spawnerObj.AddComponent<PlatformSpawner>();
            spawner.SetPrefabs(stdPrefab, movPrefab, fraPrefab, sprPrefab, stdPrefab);

            // 6. UI Canvas & EventSystem
            AssembleUI(out GameObject canvasObj, out UIManager uiManager);

            EditorSceneManager.SaveScene(scene, GameSceneFile);
        }

        private static void AssembleUI(out GameObject canvasObj, out UIManager uiManager)
        {
            // EventSystem
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();

            // Canvas
            canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            uiManager = canvasObj.AddComponent<UIManager>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // --- Ready Panel ---
            GameObject readyPanel = CreateUIPanel(canvasObj.transform, "ReadyPanel", new Color(0, 0, 0, 0f));
            CreateUIText(readyPanel.transform, "TitleText", "JUMP UP!", defaultFont, 96, new Color(1f, 0.85f, 0.2f), new Vector2(0, 320), new Vector2(800, 140));
            CreateUIText(readyPanel.transform, "ControlsText", "Use A / D, Arrow Keys\nor Tap Screen Sides to Move", defaultFont, 44, Color.white, new Vector2(0, 160), new Vector2(800, 120));
            CreateUIText(readyPanel.transform, "TapToStartText", "JUMP TO START!", defaultFont, 52, new Color(0.3f, 0.95f, 0.95f), new Vector2(0, -280), new Vector2(800, 100));

            // --- InGame Panel ---
            GameObject inGamePanel = CreateUIPanel(canvasObj.transform, "InGamePanel", new Color(0, 0, 0, 0f));
            Text scoreText = CreateUIText(inGamePanel.transform, "ScoreText", "0", defaultFont, 76, Color.white, new Vector2(0, 780), new Vector2(800, 100));
            Text highScoreText = CreateUIText(inGamePanel.transform, "HighScoreText", "BEST: 0", defaultFont, 38, new Color(1f, 0.85f, 0.3f), new Vector2(0, 710), new Vector2(800, 70));

            // --- GameOver Panel ---
            GameObject gameOverPanel = CreateUIPanel(canvasObj.transform, "GameOverPanel", new Color(0.04f, 0.06f, 0.12f, 0.88f));
            CreateUIText(gameOverPanel.transform, "TitleText", "GAME OVER", defaultFont, 92, new Color(0.95f, 0.3f, 0.3f), new Vector2(0, 340), new Vector2(800, 140));
            Text finalScoreText = CreateUIText(gameOverPanel.transform, "FinalScoreText", "SCORE: 0", defaultFont, 68, Color.white, new Vector2(0, 180), new Vector2(800, 100));
            Text finalHighScoreText = CreateUIText(gameOverPanel.transform, "FinalHighScoreText", "BEST: 0", defaultFont, 50, new Color(1f, 0.85f, 0.3f), new Vector2(0, 70), new Vector2(800, 80));

            // New Best Badge
            GameObject newBestBadge = new GameObject("NewBestBadge");
            newBestBadge.transform.SetParent(gameOverPanel.transform, false);
            RectTransform badgeRect = newBestBadge.AddComponent<RectTransform>();
            badgeRect.anchoredPosition = new Vector2(0, -20);
            badgeRect.sizeDelta = new Vector2(360, 60);
            Text badgeText = CreateUIText(newBestBadge.transform, "BadgeText", "★ NEW BEST! ★", defaultFont, 36, new Color(1f, 0.9f, 0.2f), Vector2.zero, new Vector2(360, 60));
            newBestBadge.SetActive(false);

            // Restart Button
            GameObject restartBtnObj = new GameObject("RestartButton");
            restartBtnObj.transform.SetParent(gameOverPanel.transform, false);
            RectTransform btnRect = restartBtnObj.AddComponent<RectTransform>();
            btnRect.anchoredPosition = new Vector2(0, -180);
            btnRect.sizeDelta = new Vector2(440, 110);

            Image btnImg = restartBtnObj.AddComponent<Image>();
            btnImg.color = new Color(0.18f, 0.8f, 0.44f);

            Button restartButton = restartBtnObj.AddComponent<Button>();
            restartButton.targetGraphic = btnImg;

            CreateUIText(restartBtnObj.transform, "ButtonText", "PLAY AGAIN", defaultFont, 48, Color.white, Vector2.zero, new Vector2(440, 110));

            // Wire UIManager
            uiManager.SetupLegacyReferences(
                readyPanel, inGamePanel, gameOverPanel,
                scoreText, highScoreText,
                finalScoreText, finalHighScoreText,
                newBestBadge, restartButton
            );

            EditorUtility.SetDirty(uiManager);
        }

        private static GameObject CreateUIPanel(Transform parent, string name, Color bgColor)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            if (bgColor.a > 0.01f)
            {
                Image img = panel.AddComponent<Image>();
                img.color = bgColor;
            }

            return panel;
        }

        private static Text CreateUIText(Transform parent, string name, string content, Font font, int size, Color color, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rt = textObj.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Text text = textObj.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = content;

            // Outline for arcade pop
            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            return text;
        }

        #endregion

        #region Build Settings

        private static void RegisterBuildScene()
        {
            var scenes = EditorBuildSettings.scenes;
            bool found = false;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == GameSceneFile)
                {
                    scenes[i].enabled = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
                newScenes[0] = new EditorBuildSettingsScene(GameSceneFile, true);
                for (int i = 0; i < scenes.Length; i++)
                {
                    newScenes[i + 1] = scenes[i];
                }
                EditorBuildSettings.scenes = newScenes;
            }

            Debug.Log("[GameSceneBuilder] Registered " + GameSceneFile + " as default build scene.");
        }

        #endregion
    }
}
#endif
