using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum HomeSnakeDirection
{
    Right,
    Down,
    Left,
    Up
}

public enum HomeSnakeEdge
{
    Left,
    Top,
    Right,
    Bottom
}

/// <summary>
/// 游戏主页：纯黑背景、三个纯图标入口，以及不参与玩法的装饰动画。
/// 主页只负责进入正式关卡和打开现有设置界面，不复制关卡或玩法规则。
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class HomeScreen : MonoBehaviour
{
    public const string LevelSceneName = "REAL FINAL MAP";

    enum HomeAction
    {
        NewGame,
        LoadGame,
        Settings,
        Quit
    }

    sealed class SnakeSegment
    {
        public Vector2Int cell;
        public float age;
        public float startAlpha;
    }

    sealed class BorderSnake
    {
        public readonly List<SnakeSegment> body = new List<SnakeSegment>();
        public readonly List<SnakeSegment> fading = new List<SnakeSegment>();
        public Vector2Int headCell;
        public HomeSnakeDirection direction;
        public float moveAccumulator;
        public float lifeRemaining;
        public bool despawning;
    }

    sealed class FloatingArt
    {
        public Texture2D texture;
        public Vector2 position;
        public Vector2 size;
        public float rotation;
        public float rotationSpeed;
        public float moveSpeed;
    }

    sealed class PendingArt
    {
        public Texture2D texture;
        public float scale;
        public float moveSpeed;
        public float rotationSpeed;
        public float height;
        public float dueAt;
    }

    static readonly string[] ButtonIconNames =
    {
        "KeyBindings",
        "Resolution",
        "Settings",
        "Exit"
    };

    static readonly Color SnakeColor = new Color(0.36f, 0.89f, 1f, 1f);
    static readonly Color ButtonFill = new Color(0.015f, 0.025f, 0.035f, 0.88f);
    static readonly Color ButtonBorder = new Color(0.36f, 0.89f, 1f, 0.82f);
    static readonly Color DisabledBorder = new Color(0.36f, 0.89f, 1f, 0.25f);
    static readonly Color HoverBorder = new Color(0.93f, 0.98f, 1f, 1f);
    static readonly Color ArtTint = new Color(1f, 1f, 1f, 0.72f);

    readonly List<Texture2D> artPool = new List<Texture2D>();
    readonly List<BorderSnake> borderSnakes = new List<BorderSnake>();
    readonly List<FloatingArt> floatingArt = new List<FloatingArt>();
    readonly List<PendingArt> pendingArt = new List<PendingArt>();
    readonly Texture2D[] buttonIcons = new Texture2D[ButtonIconNames.Length];
    readonly float[] hoverTimes = new float[ButtonIconNames.Length];

    LabSettingsUI settingsUI;
    LabCheckpointStore.Data savedGame;
    System.Random random;
    Texture2D fallbackTexture;
    bool ready;
    bool navigating;
    bool confirmingNewGame;
    int hoveredButton = -1;
    int lastScreenWidth;
    int lastScreenHeight;
    float visualClock;
    float nextBatchAt;
    float nextSnakePairAt;
    float transitionElapsed;

    bool SettingsOpen => settingsUI != null && settingsUI.IsOpen;
    bool MotionPaused => SettingsOpen || navigating || confirmingNewGame;
    bool CanLoadGame => savedGame != null && Application.CanStreamedLevelBeLoaded(savedGame.scene);

    void Awake()
    {
        EnsureCamera();
        EnsureSettingsUI();
        LoadIcons();
        savedGame = LabCheckpointStore.Load();
        random = new System.Random(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
        RebuildBorderSnakes();
        ready = true;
    }

    void Start()
    {
        // 让主页也应用已保存的音量/窗口设置；设置界面随后只负责修改和保存。
        settingsUI.Bind(null);
        nextBatchAt = 0f;
    }

    void Update()
    {
        if (!ready) return;

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            RebuildBorderSnakes();
        }

        // 覆盖确认弹窗打开时 Esc = 取消；设置组件已禁用，不会抢这个 Esc。
        if (confirmingNewGame)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CancelNewGameConfirm();
        }

        bool blocked = MotionPaused;
        int nextHover = blocked ? -1 : FindHoveredButton(MouseGuiPosition());
        if (nextHover != hoveredButton)
        {
            hoveredButton = nextHover;
            for (int i = 0; i < hoverTimes.Length; i++) hoverTimes[i] = 0f;
        }

        if (blocked) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        visualClock += dt;
        if (hoveredButton >= 0) hoverTimes[hoveredButton] += dt;

        UpdateBorderSnakes(dt);
        UpdatePendingArt();
        UpdateFloatingArt(dt);

        if (visualClock >= nextBatchAt)
        {
            ScheduleBatch();
            nextBatchAt = visualClock + HomeScreenSpec.NextBatchDelay(random);
        }
    }

    void OnDestroy()
    {
        if (fallbackTexture != null) Destroy(fallbackTexture);
    }

    void OnGUI()
    {
        if (!ready) return;

        DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), Color.black);
        for (int i = 0; i < borderSnakes.Count; i++) DrawBorderSnake(borderSnakes[i]);

        for (int i = 0; i < floatingArt.Count; i++) DrawFloatingArt(floatingArt[i]);

        if (!navigating)
        {
            Rect[] rects = ButtonRects();
            for (int i = 0; i < rects.Length; i++) DrawButton((HomeAction)i, rects[i]);
        }

        if (confirmingNewGame) DrawNewGameConfirm();
        if (navigating) DrawCloseTransition();
    }

    void DrawBorderSnake(BorderSnake snake)
    {
        for (int i = 0; i < snake.fading.Count; i++)
        {
            SnakeSegment segment = snake.fading[i];
            if (!IsSnakeCellVisible(segment.cell)) continue;
            DrawSnakeSegment(segment.cell,
                HomeScreenSpec.FadingSnakeAlpha(segment.age, segment.startAlpha));
        }

        for (int i = 0; i < snake.body.Count; i++)
        {
            SnakeSegment segment = snake.body[i];
            if (!IsSnakeCellVisible(segment.cell)) continue;
            DrawSnakeSegment(segment.cell, HomeScreenSpec.ActiveSnakeAlpha(segment.age));
        }
    }

    static void DrawSnakeSegment(Vector2Int cell, float alpha)
    {
        if (alpha <= 0f) return;
        Color color = SnakeColor;
        color.a *= Mathf.Clamp01(alpha);
        Rect rect = SnakeCellRect(cell);
        DrawOutline(rect, HomeScreenSpec.BorderSnakeCellStroke, color);
    }

    void DrawButton(HomeAction action, Rect baseRect)
    {
        bool interactive = !SettingsOpen && !navigating && !confirmingNewGame;
        bool actionEnabled = action != HomeAction.LoadGame || CanLoadGame;
        bool hovered = interactive && actionEnabled && hoveredButton == (int)action;
        float breath = hovered ? HomeScreenSpec.ButtonBreathScale(hoverTimes[(int)action]) : 1f;
        Rect buttonRect = ScaleRect(baseRect, breath);

        DrawSolid(buttonRect, hovered ? new Color(0.025f, 0.045f, 0.06f, 0.96f) : ButtonFill);
        DrawOutline(buttonRect, 2f, !actionEnabled ? DisabledBorder : hovered ? HoverBorder : ButtonBorder);

        Texture2D icon = buttonIcons[(int)action] != null ? buttonIcons[(int)action] : fallbackTexture;
        if (icon != null)
        {
            Rect iconRect = new Rect(
                buttonRect.center.x - HomeScreenSpec.IconSize * 0.5f,
                buttonRect.center.y - HomeScreenSpec.IconSize * 0.5f,
                HomeScreenSpec.IconSize,
                HomeScreenSpec.IconSize);

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            if (hovered)
            {
                float flip = HomeScreenSpec.IconFlipScale(hoverTimes[(int)action]);
                GUIUtility.ScaleAroundPivot(new Vector2(flip, 1f), iconRect.center);
            }
            GUI.color = hovered ? Color.white
                : actionEnabled ? new Color(0.88f, 0.94f, 0.96f, 1f)
                : new Color(0.88f, 0.94f, 0.96f, 0.3f);
            GUI.DrawTexture(iconRect, icon, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        bool wasEnabled = GUI.enabled;
        GUI.enabled = interactive && actionEnabled;
        if (GUI.Button(baseRect, GUIContent.none, GUIStyle.none)) Activate(action);
        GUI.enabled = wasEnabled;
    }

    void Activate(HomeAction action)
    {
        if (!HomeScreenSpec.CanStartNavigation(SettingsOpen, navigating) || confirmingNewGame) return;

        if (action == HomeAction.Settings)
        {
            hoveredButton = -1;
            if (HomeScreenSpec.CanOpenSettings(navigating)) settingsUI.Open();
            return;
        }

        if (action == HomeAction.Quit)
        {
            LabSettingsUI.QuitGame();
            return;
        }

        if (action == HomeAction.LoadGame)
        {
            if (!CanLoadGame) return;
            hoveredButton = -1;
            LabCheckpointStore.PendingContinue = savedGame.checkpoint;
            StartCoroutine(CloseAndLoadLevel(savedGame.scene));
            return;
        }

        // 新游戏：已有存档先弹覆盖确认，确认后才删档进关。
        hoveredButton = -1;
        if (savedGame != null)
        {
            confirmingNewGame = true;
            if (settingsUI != null) settingsUI.enabled = false;
            return;
        }
        StartNewGame();
    }

    void StartNewGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(LevelSceneName))
        {
            Debug.LogError($"HomeScreen: 构建场景列表中没有可加载的场景“{LevelSceneName}”。");
            return;
        }
        StartCoroutine(CloseAndLoadLevel(LevelSceneName));
    }

    void ConfirmNewGame()
    {
        confirmingNewGame = false;
        if (settingsUI != null) settingsUI.enabled = true;
        LabCheckpointStore.Delete();
        savedGame = null;
        StartNewGame();
    }

    void CancelNewGameConfirm()
    {
        confirmingNewGame = false;
        if (settingsUI != null) settingsUI.enabled = true;
    }

    IEnumerator CloseAndLoadLevel(string sceneName)
    {
        if (!HomeScreenSpec.CanStartNavigation(SettingsOpen, navigating)) yield break;

        navigating = true;
        // 转场期间连 Esc 也不能把设置面板重新打开。
        if (settingsUI != null) settingsUI.enabled = false;
        transitionElapsed = 0f;
        while (transitionElapsed < HomeScreenSpec.TransitionDuration)
        {
            transitionElapsed = Mathf.Min(
                HomeScreenSpec.TransitionDuration,
                transitionElapsed + Time.unscaledDeltaTime);
            yield return null;
        }

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
            yield break;
        }

        Debug.LogError($"HomeScreen: 无法加载场景“{sceneName}”。");
        navigating = false;
        if (settingsUI != null) settingsUI.enabled = true;
        transitionElapsed = 0f;
    }

    /// <summary>覆盖存档确认：深色面板 + 青色描边 + 像素字，和主页按钮同一套语言。</summary>
    void DrawNewGameConfirm()
    {
        DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.62f));

        float panelWidth = Mathf.Min(520f, Screen.width - 48f);
        const float panelHeight = 232f;
        var panel = new Rect(
            Mathf.Round((Screen.width - panelWidth) * 0.5f),
            Mathf.Round((Screen.height - panelHeight) * 0.5f),
            panelWidth, panelHeight);
        DrawSolid(panel, ButtonFill);
        DrawOutline(panel, 2f, ButtonBorder);

        var textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            font = LabPixelFont.Font,
            fontSize = LabPixelFont.SnapSize(LabPixelFont.UiSize),
        };
        textStyle.normal.textColor = HoverBorder;
        GUI.Label(new Rect(panel.x + 24f, panel.y + 28f, panel.width - 48f, 96f),
            "已有存档。\n开始新游戏将覆盖现有存档，是否继续？", textStyle);

        const float buttonWidth = 168f;
        const float buttonHeight = 48f;
        const float gap = 32f;
        float rowY = panel.yMax - 28f - buttonHeight;
        var confirm = new Rect(panel.center.x - gap * 0.5f - buttonWidth, rowY, buttonWidth, buttonHeight);
        var cancel = new Rect(panel.center.x + gap * 0.5f, rowY, buttonWidth, buttonHeight);
        if (DrawDialogButton(confirm, "覆盖存档")) ConfirmNewGame();
        if (DrawDialogButton(cancel, "取消")) CancelNewGameConfirm();
    }

    bool DrawDialogButton(Rect rect, string label)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        DrawSolid(rect, hover ? new Color(0.025f, 0.045f, 0.06f, 0.96f) : ButtonFill);
        DrawOutline(rect, 2f, hover ? HoverBorder : ButtonBorder);

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            font = LabPixelFont.Font,
            fontSize = LabPixelFont.SnapSize(LabPixelFont.UiSize),
        };
        style.normal.textColor = hover ? Color.white : new Color(0.88f, 0.94f, 0.96f, 1f);
        GUI.Label(rect, label, style);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    void DrawCloseTransition()
    {
        float width = Screen.width;
        float height = Screen.height;
        Rect aperture = HomeScreenSpec.TransitionAperture(transitionElapsed, width, height);
        if (aperture.width <= 0f || aperture.height <= 0f)
        {
            DrawSolid(new Rect(0f, 0f, width, height), Color.black);
            return;
        }

        DrawSolid(new Rect(0f, 0f, width, aperture.y), Color.black);
        DrawSolid(new Rect(0f, aperture.yMax, width, height - aperture.yMax), Color.black);
        DrawSolid(new Rect(0f, aperture.y, aperture.x, aperture.height), Color.black);
        DrawSolid(new Rect(aperture.xMax, aperture.y,
            width - aperture.xMax, aperture.height), Color.black);
    }

    void DrawFloatingArt(FloatingArt art)
    {
        if (art.texture == null) return;

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        Rect rect = new Rect(
            art.position.x - art.size.x * 0.5f,
            art.position.y - art.size.y * 0.5f,
            art.size.x,
            art.size.y);

        GUIUtility.RotateAroundPivot(art.rotation, art.position);
        GUI.color = ArtTint;
        GUI.DrawTexture(rect, art.texture, ScaleMode.StretchToFill, true);
        GUI.color = oldColor;
        GUI.matrix = oldMatrix;
    }

    void UpdateBorderSnakes(float dt)
    {
        for (int i = borderSnakes.Count - 1; i >= 0; i--)
        {
            BorderSnake snake = borderSnakes[i];
            UpdateSnakeFades(snake, dt);

            if (!snake.despawning)
            {
                snake.lifeRemaining -= dt;
                if (snake.lifeRemaining <= 0f)
                {
                    BeginSnakeDespawn(snake);
                }
                else
                {
                    snake.moveAccumulator += dt;
                    while (snake.moveAccumulator >= HomeScreenSpec.BorderSnakeStepSeconds &&
                           !snake.despawning)
                    {
                        snake.moveAccumulator -= HomeScreenSpec.BorderSnakeStepSeconds;
                        StepBorderSnake(snake);
                    }
                }
            }

            if (snake.despawning && snake.fading.Count == 0)
            {
                borderSnakes.RemoveAt(i);
            }
        }

        if (borderSnakes.Count == 0 && visualClock >= nextSnakePairAt)
            SpawnSnakePair();
    }

    void StepBorderSnake(BorderSnake snake)
    {
        HomeSnakeDirection nextDirection =
            HomeScreenSpec.NextSnakeDirection(snake.direction, random);
        Vector2Int nextCell = snake.headCell + HomeScreenSpec.SnakeDirectionDelta(nextDirection);
        if (!IsSnakeCellInGrid(nextCell))
        {
            BeginSnakeDespawn(snake);
            return;
        }

        snake.direction = nextDirection;
        snake.headCell = nextCell;

        SnakeSegment tail = snake.body[snake.body.Count - 1];
        snake.body.RemoveAt(snake.body.Count - 1);
        if (IsSnakeCellVisible(tail.cell))
        {
            snake.fading.Add(new SnakeSegment
            {
                cell = tail.cell,
                age = 0f,
                startAlpha = HomeScreenSpec.ActiveSnakeAlpha(tail.age)
            });
        }

        snake.body.Insert(0, new SnakeSegment
        {
            cell = nextCell,
            age = IsSnakeCellVisible(nextCell) ? 0f : -1f
        });
    }

    static void UpdateSnakeFades(BorderSnake snake, float dt)
    {
        for (int i = 0; i < snake.body.Count; i++)
        {
            SnakeSegment segment = snake.body[i];
            if (segment.age < 0f)
            {
                if (IsSnakeCellVisible(segment.cell)) segment.age = 0f;
            }
            else
            {
                segment.age = Mathf.Min(
                    HomeScreenSpec.BorderSnakeFadeSeconds,
                    segment.age + dt);
            }
        }

        for (int i = snake.fading.Count - 1; i >= 0; i--)
        {
            SnakeSegment segment = snake.fading[i];
            segment.age += dt;
            if (segment.age >= HomeScreenSpec.BorderSnakeFadeSeconds)
                snake.fading.RemoveAt(i);
        }
    }

    static void BeginSnakeDespawn(BorderSnake snake)
    {
        if (snake.despawning) return;
        snake.despawning = true;
        for (int i = 0; i < snake.body.Count; i++)
        {
            SnakeSegment segment = snake.body[i];
            if (!IsSnakeCellVisible(segment.cell)) continue;
            snake.fading.Add(new SnakeSegment
            {
                cell = segment.cell,
                age = 0f,
                startAlpha = HomeScreenSpec.ActiveSnakeAlpha(segment.age)
            });
        }
        snake.body.Clear();
    }

    void SpawnSnakePair()
    {
        int columns = Mathf.Max(1,
            Mathf.CeilToInt(Screen.width / HomeScreenSpec.BorderSnakeCellSize));
        int rows = Mathf.Max(1,
            Mathf.CeilToInt(Screen.height / HomeScreenSpec.BorderSnakeCellSize));

        for (int i = 0; i < HomeScreenSpec.BorderSnakeCount; i++)
        {
            var edge = (HomeSnakeEdge)random.Next(4);
            Vector2Int head = HomeScreenSpec.SnakeStartCell(edge, columns, rows, random);
            HomeSnakeDirection direction = HomeScreenSpec.SnakeStartDirection(edge);
            Vector2Int backward = HomeScreenSpec.SnakeDirectionDelta(direction) * -1;
            int length = HomeScreenSpec.NextBorderSnakeLength(random);
            var snake = new BorderSnake
            {
                headCell = head,
                direction = direction,
                lifeRemaining = HomeScreenSpec.NextBorderSnakeLifetime(random)
            };

            for (int segmentIndex = 0; segmentIndex < length; segmentIndex++)
            {
                Vector2Int cell = head + backward * segmentIndex;
                snake.body.Add(new SnakeSegment
                {
                    cell = cell,
                    age = IsSnakeCellVisible(cell) ? 0f : -1f
                });
            }
            borderSnakes.Add(snake);
        }
    }

    void UpdatePendingArt()
    {
        for (int i = pendingArt.Count - 1; i >= 0; i--)
        {
            var pending = pendingArt[i];
            if (pending.dueAt > visualClock) continue;

            floatingArt.Add(new FloatingArt
            {
                texture = pending.texture,
                size = new Vector2(
                    Mathf.Max(1f, pending.texture.width * pending.scale),
                    Mathf.Max(1f, pending.texture.height * pending.scale)),
                position = new Vector2(
                    -pending.texture.width * pending.scale * 0.5f - RandomRange(0f, 48f),
                    pending.height),
                rotation = RandomRange(0f, 360f),
                rotationSpeed = pending.rotationSpeed,
                moveSpeed = pending.moveSpeed
            });
            pendingArt.RemoveAt(i);
        }
    }

    void UpdateFloatingArt(float dt)
    {
        for (int i = floatingArt.Count - 1; i >= 0; i--)
        {
            var art = floatingArt[i];
            art.position.x += art.moveSpeed * dt;
            art.rotation += art.rotationSpeed * dt;
            if (art.position.x - art.size.x * 0.5f > Screen.width + 24f)
                floatingArt.RemoveAt(i);
        }
    }

    void ScheduleBatch()
    {
        if (artPool.Count == 0) return;

        int count = HomeScreenSpec.NextBatchCount(random);
        for (int i = 0; i < count; i++)
        {
            Texture2D texture = artPool[random.Next(artPool.Count)];
            float scale = HomeScreenSpec.NextArtScale(random);
            float halfHeight = texture.height * scale * 0.5f;
            float minHeight = Mathf.Min(Screen.height * 0.5f, halfHeight + 12f);
            float maxHeight = Mathf.Max(minHeight, Screen.height - halfHeight - 12f);
            pendingArt.Add(new PendingArt
            {
                texture = texture,
                scale = scale,
                moveSpeed = HomeScreenSpec.NextArtMoveSpeed(random),
                rotationSpeed = HomeScreenSpec.NextArtRotationSpeed(random) *
                    (random.Next(2) == 0 ? -1f : 1f),
                height = RandomRange(minHeight, maxHeight),
                dueAt = visualClock + HomeScreenSpec.NextArtStagger(random)
            });
        }
    }

    void LoadIcons()
    {
        // 统一走 LabSettingsUI.Icon：带缓存，Exit 是代码绘制图，不在 Resources 里。
        for (int i = 0; i < ButtonIconNames.Length; i++)
            buttonIcons[i] = LabSettingsUI.Icon(ButtonIconNames[i]);

        var mapArt = Resources.LoadAll<Texture2D>("HuiArt");
        var uiArt = Resources.LoadAll<Texture2D>("SettingsIcons");
        AddArt(mapArt);
        AddArt(uiArt);
        artPool.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (artPool.Count == 0)
        {
            fallbackTexture = MakeFallbackTexture();
            artPool.Add(fallbackTexture);
            Debug.LogWarning("HomeScreen: 未找到主页漂浮素材，已使用占位图。");
        }
        else
        {
            fallbackTexture = MakeFallbackTexture();
        }
    }

    void AddArt(Texture2D[] textures)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            if (texture != null && !artPool.Contains(texture)) artPool.Add(texture);
        }
    }

    static Texture2D MakeFallbackTexture()
    {
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            name = "HomeFallback",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[16 * 16];
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            pixels[y * 16 + x] = (x < 2 || y < 2 || x > 13 || y > 13)
                ? new Color32(92, 227, 255, 255)
                : new Color32(8, 24, 32, 210);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    void RebuildBorderSnakes()
    {
        lastScreenWidth = Mathf.Max(1, Screen.width);
        lastScreenHeight = Mathf.Max(1, Screen.height);
        borderSnakes.Clear();
        nextSnakePairAt = visualClock;
        SpawnSnakePair();
    }

    static Rect SnakeCellRect(Vector2Int cell)
    {
        return new Rect(
            cell.x * HomeScreenSpec.BorderSnakeCellSize,
            cell.y * HomeScreenSpec.BorderSnakeCellSize,
            HomeScreenSpec.BorderSnakeCellSize,
            HomeScreenSpec.BorderSnakeCellSize);
    }

    static bool IsSnakeCellVisible(Vector2Int cell)
    {
        Rect rect = SnakeCellRect(cell);
        return rect.xMax > 0f && rect.xMin < Screen.width &&
               rect.yMax > 0f && rect.yMin < Screen.height;
    }

    static bool IsSnakeCellInGrid(Vector2Int cell)
    {
        int columns = Mathf.Max(1,
            Mathf.CeilToInt(Screen.width / HomeScreenSpec.BorderSnakeCellSize));
        int rows = Mathf.Max(1,
            Mathf.CeilToInt(Screen.height / HomeScreenSpec.BorderSnakeCellSize));
        return cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows;
    }

    Rect[] ButtonRects()
    {
        float width = Screen.width;
        float height = Screen.height;
        int count = ButtonIconNames.Length;
        float rowWidth = HomeScreenSpec.ButtonSize * count + HomeScreenSpec.ButtonGap * (count - 1);
        float left = Mathf.Max(12f, (width - rowWidth) * 0.5f);
        float top = Mathf.Max(12f, (height - HomeScreenSpec.ButtonSize) * 0.5f);
        var result = new Rect[count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new Rect(
                left + i * (HomeScreenSpec.ButtonSize + HomeScreenSpec.ButtonGap),
                top,
                HomeScreenSpec.ButtonSize,
                HomeScreenSpec.ButtonSize);
        }
        return result;
    }

    int FindHoveredButton(Vector2 pointer)
    {
        Rect[] rects = ButtonRects();
        for (int i = 0; i < rects.Length; i++)
        {
            if (!rects[i].Contains(pointer)) continue;
            if (i == (int)HomeAction.LoadGame && !CanLoadGame) continue;
            return i;
        }
        return -1;
    }

    static Vector2 MouseGuiPosition()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return new Vector2(-1f, -1f);
        Vector2 screen = mouse.position.ReadValue();
        return new Vector2(screen.x, Screen.height - screen.y);
    }

    float RandomRange(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    void EnsureSettingsUI()
    {
        settingsUI = GetComponent<LabSettingsUI>();
        if (settingsUI == null) settingsUI = gameObject.AddComponent<LabSettingsUI>();
    }

    static void EnsureCamera()
    {
        if (Camera.main != null) return;

        var cameraObject = new GameObject("Home Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.cullingMask = 0;
        cameraObject.AddComponent<AudioListener>();
    }

    static Rect ScaleRect(Rect rect, float scale)
    {
        Vector2 center = rect.center;
        float width = rect.width * scale;
        float height = rect.height * scale;
        return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
    }

    static void DrawOutline(Rect rect, float stroke, Color color)
    {
        DrawSolid(new Rect(rect.x, rect.y, rect.width, stroke), color);
        DrawSolid(new Rect(rect.x, rect.yMax - stroke, rect.width, stroke), color);
        DrawSolid(new Rect(rect.x, rect.y, stroke, rect.height), color);
        DrawSolid(new Rect(rect.xMax - stroke, rect.y, stroke, rect.height), color);
    }

    static void DrawSolid(Rect rect, Color color)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }
}

/// <summary>主页随机范围与动画曲线的单一事实来源，便于测试和后续调参。</summary>
public static class HomeScreenSpec
{
    public const int BorderSnakeCount = 2;
    public const int BorderSnakeLengthMin = 3;
    public const int BorderSnakeLengthMax = 11;
    public const float BorderSnakeCellSize = 64f;
    public const float BorderSnakeCellStroke = 8f;
    public const float BorderSnakeStepSeconds = 0.15f;
    public const float BorderSnakeFadeSeconds = 0.10f;
    public const float BorderSnakeTurnChance = 1f / 30f;
    public const float BorderSnakeLifetimeMin = 15f;
    public const float BorderSnakeLifetimeMax = 25f;

    public const float ButtonSize = 144f;
    public const float IconSize = 60f;
    public const float ButtonGap = 54f;
    public const float ButtonBreathDuration = 1.2f;
    public const float ButtonBreathMaxScale = 1.08f;
    public const float IconFlipDuration = 0.75f;
    public const float IconFlipHoldDuration = 0.75f;

    public const int ArtBatchCountMin = 1;
    public const int ArtBatchCountMax = 7;
    public const float ArtBatchDelayMin = 0.9f;
    public const float ArtBatchDelayMax = 3.6f;
    public const float ArtScaleMin = 2f;
    public const float ArtScaleMax = 2.5f;
    public const float ArtMoveSpeedMin = 80f;
    public const float ArtMoveSpeedMax = 220f;
    public const float ArtRotationSpeedMin = 12f;
    public const float ArtRotationSpeedMax = 45f;
    public const float ArtStaggerMin = 0f;
    public const float ArtStaggerMax = 0.18f;

    public const float TransitionCollapseDuration = 0.40f;
    public const float TransitionHoldDuration = 0.12f;
    public const float TransitionStretchDuration = 0.18f;
    public const float TransitionDuration =
        TransitionCollapseDuration + TransitionHoldDuration + TransitionStretchDuration;

    public static bool CanOpenSettings(bool navigating) => !navigating;

    public static bool CanStartNavigation(bool settingsOpen, bool navigating)
    {
        return !settingsOpen && !navigating;
    }

    public static int NextBorderSnakeLength(System.Random random)
    {
        return random.Next(BorderSnakeLengthMin, BorderSnakeLengthMax + 1);
    }

    public static float NextBorderSnakeLifetime(System.Random random)
    {
        return Range(random, BorderSnakeLifetimeMin, BorderSnakeLifetimeMax);
    }

    public static Vector2Int SnakeStartCell(
        HomeSnakeEdge edge,
        int columns,
        int rows,
        System.Random random)
    {
        int column = Mathf.Max(1, columns);
        int row = Mathf.Max(1, rows);
        switch (edge)
        {
            case HomeSnakeEdge.Top:
                return new Vector2Int(random.Next(column), 0);
            case HomeSnakeEdge.Right:
                return new Vector2Int(column - 1, random.Next(row));
            case HomeSnakeEdge.Bottom:
                return new Vector2Int(random.Next(column), row - 1);
            default:
                return new Vector2Int(0, random.Next(row));
        }
    }

    public static HomeSnakeDirection SnakeStartDirection(HomeSnakeEdge edge)
    {
        switch (edge)
        {
            case HomeSnakeEdge.Top:
                return HomeSnakeDirection.Down;
            case HomeSnakeEdge.Right:
                return HomeSnakeDirection.Left;
            case HomeSnakeEdge.Bottom:
                return HomeSnakeDirection.Up;
            default:
                return HomeSnakeDirection.Right;
        }
    }

    public static Vector2Int SnakeDirectionDelta(HomeSnakeDirection direction)
    {
        switch (direction)
        {
            case HomeSnakeDirection.Down:
                return new Vector2Int(0, 1);
            case HomeSnakeDirection.Left:
                return new Vector2Int(-1, 0);
            case HomeSnakeDirection.Up:
                return new Vector2Int(0, -1);
            default:
                return new Vector2Int(1, 0);
        }
    }

    public static HomeSnakeDirection NextSnakeDirection(
        HomeSnakeDirection current,
        System.Random random)
    {
        if (random.NextDouble() >= BorderSnakeTurnChance) return current;
        return random.Next(2) == 0 ? TurnRight(current) : TurnLeft(current);
    }

    public static float ActiveSnakeAlpha(float age)
    {
        if (age < 0f) return 0f;
        return Mathf.Clamp01(age / BorderSnakeFadeSeconds);
    }

    public static float FadingSnakeAlpha(float age, float startAlpha)
    {
        return Mathf.Clamp01(startAlpha) *
               (1f - Mathf.Clamp01(age / BorderSnakeFadeSeconds));
    }

    public static int NextBatchCount(System.Random random)
    {
        return random.Next(ArtBatchCountMin, ArtBatchCountMax + 1);
    }

    public static float NextBatchDelay(System.Random random)
    {
        return Range(random, ArtBatchDelayMin, ArtBatchDelayMax);
    }

    public static float NextArtScale(System.Random random)
    {
        return Range(random, ArtScaleMin, ArtScaleMax);
    }

    public static float NextArtMoveSpeed(System.Random random)
    {
        return Range(random, ArtMoveSpeedMin, ArtMoveSpeedMax);
    }

    public static float NextArtRotationSpeed(System.Random random)
    {
        return Range(random, ArtRotationSpeedMin, ArtRotationSpeedMax);
    }

    public static float NextArtStagger(System.Random random)
    {
        return Range(random, ArtStaggerMin, ArtStaggerMax);
    }

    public static float ButtonBreathScale(float hoverTime)
    {
        if (hoverTime <= 0f) return 1f;
        float phase = Mathf.Repeat(hoverTime, ButtonBreathDuration) / ButtonBreathDuration;
        float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
        return Mathf.Lerp(1f, ButtonBreathMaxScale, wave);
    }

    public static float IconFlipScale(float hoverTime)
    {
        if (hoverTime <= 0f) return 1f;
        float phase = Mathf.Repeat(hoverTime, IconFlipDuration + IconFlipHoldDuration);
        if (phase >= IconFlipDuration) return 1f;
        return Mathf.Cos(phase / IconFlipDuration * Mathf.PI * 6f);
    }

    public static Rect TransitionAperture(float elapsed, float screenWidth, float screenHeight)
    {
        float width = Mathf.Max(0f, screenWidth);
        float height = Mathf.Max(0f, screenHeight);
        float narrowWidth = width * 0.08f;
        float narrowHeight = Mathf.Min(3f, height);

        float apertureWidth;
        float apertureHeight;
        if (elapsed <= TransitionCollapseDuration)
        {
            float t = SmoothStep01(elapsed / TransitionCollapseDuration);
            apertureWidth = Mathf.Lerp(width, narrowWidth, t);
            apertureHeight = Mathf.Lerp(height, narrowHeight, t);
        }
        else if (elapsed <= TransitionCollapseDuration + TransitionHoldDuration)
        {
            apertureWidth = narrowWidth;
            apertureHeight = narrowHeight;
        }
        else
        {
            float t = Mathf.Clamp01(
                (elapsed - TransitionCollapseDuration - TransitionHoldDuration) /
                TransitionStretchDuration);
            apertureWidth = Mathf.Lerp(narrowWidth, Mathf.Min(width, narrowWidth * 10f), t);
            apertureHeight = narrowHeight;
        }

        return new Rect(
            (width - apertureWidth) * 0.5f,
            (height - apertureHeight) * 0.5f,
            apertureWidth,
            apertureHeight);
    }

    static float Range(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    static HomeSnakeDirection TurnRight(HomeSnakeDirection direction)
    {
        return (HomeSnakeDirection)(((int)direction + 1) % 4);
    }

    static HomeSnakeDirection TurnLeft(HomeSnakeDirection direction)
    {
        return (HomeSnakeDirection)(((int)direction + 3) % 4);
    }

    static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
