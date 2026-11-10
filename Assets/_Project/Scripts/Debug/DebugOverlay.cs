using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace GameAi
{
    // F3 디버그(8.2, 10장): 적 ID·상태·게이지·의심도·긴장도, 시야 부채꼴, A* 경로선, 소리 반경 원, 칸별 밝기 히트맵,
    // 피아 색 원, FPS·AI ms, Qwen 통계. 적을 클릭하면 마지막 상황 입력 JSON·출력·지연·결과 코드
    public sealed class DebugOverlay
    {
        const int MaxLines = 160;
        const float NoiseShowSeconds = 1f;

        readonly Transform root;
        readonly Material material;
        readonly List<LineRenderer> lines = new List<LineRenderer>();
        readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
        readonly RectTransform labelRoot;
        readonly RectTransform canvasRect;
        readonly TextMeshProUGUI stats;
        readonly TextMeshProUGUI detail;
        readonly SpriteRenderer heatmap;
        readonly Texture2D heatTexture;
        readonly AssetCatalog catalog;
        readonly List<(Vector2 pos, float radius, float time)> noises = new List<(Vector2, float, float)>();
        int used;
        int labelUsed;
        int selectedEnemy = -1;

        public bool Visible { get; private set; }

        public DebugOverlay(Transform parent, UiRoot ui, AssetCatalog catalog, GridMap grid)
        {
            this.catalog = catalog;
            root = new GameObject("DebugOverlay").transform;
            root.SetParent(parent, false);
            material = catalog.SpriteUnlit != null ? catalog.SpriteUnlit : new Material(Shader.Find("Sprites/Default"));
            heatTexture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            heatmap = new GameObject("Heatmap", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            heatmap.transform.SetParent(root, false);
            heatmap.sprite = Sprite.Create(heatTexture, new Rect(0, 0, grid.Width, grid.Height), Vector2.zero, 1f / grid.CellSize);
            heatmap.sharedMaterial = material;
            heatmap.sortingOrder = SortOrder.Fog + 1;
            canvasRect = ui.Rect;
            labelRoot = UiRoot.CreateRect("DebugLabels", ui.Rect);
            Widgets.Stretch(labelRoot);
            stats = Widgets.Label("DebugStats", labelRoot, catalog, "", 24f, TextAlignmentOptions.TopRight);
            Widgets.Place(stats.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -60f), new Vector2(900f, 400f));
            stats.textWrappingMode = TextWrappingModes.Normal;
            detail = Widgets.Label("DebugDetail", labelRoot, catalog, "", 24f, TextAlignmentOptions.TopLeft);
            Widgets.Place(detail.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -100f), new Vector2(1100f, 400f));
            detail.textWrappingMode = TextWrappingModes.Normal;
            SetVisible(false);
        }

        public void OnNoise(NoiseEvent e) { noises.Add((e.position, e.radius, GameClock.Time)); }

        public void SetVisible(bool visible)
        {
            Visible = visible;
            root.gameObject.SetActive(visible);
            labelRoot.gameObject.SetActive(visible);
        }

        LineRenderer Line(Color color, float width = 0.04f)
        {
            LineRenderer lr;
            if (used < lines.Count) lr = lines[used];
            else
            {
                if (lines.Count >= MaxLines) return null;
                lr = new GameObject("L", typeof(LineRenderer)).GetComponent<LineRenderer>();
                lr.transform.SetParent(root, false);
                lr.sharedMaterial = material;
                lr.useWorldSpace = true;
                lr.sortingOrder = SortOrder.Fog + 2;
                lines.Add(lr);
            }
            used++;
            lr.enabled = true;
            lr.startColor = lr.endColor = color;
            lr.startWidth = lr.endWidth = width;
            return lr;
        }

        void Circle(Vector2 c, float r, Color color, int segments = 32)
        {
            LineRenderer lr = Line(color);
            if (lr == null) return;
            lr.loop = true;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f));
            }
        }

        void Cone(Vector2 eye, Vector2 facing, float fov, float range, Color color)
        {
            LineRenderer lr = Line(color, 0.03f);
            if (lr == null) return;
            lr.loop = true;
            const int arc = 12;
            lr.positionCount = arc + 2;
            lr.SetPosition(0, eye);
            float baseAngle = MathUtil.AngleDeg(facing);
            for (int i = 0; i <= arc; i++)
            {
                float a = (baseAngle - fov / 2f + fov * i / arc) * Mathf.Deg2Rad;
                lr.SetPosition(i + 1, eye + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * range);
            }
        }

        void Path(GridMap grid, IReadOnlyList<Vector2Int> path, int from, Vector2 start, Color color)
        {
            if (path.Count == 0 || from >= path.Count) return;
            LineRenderer lr = Line(color, 0.05f);
            if (lr == null) return;
            lr.loop = false;
            lr.positionCount = path.Count - from + 1;
            lr.SetPosition(0, start);
            for (int i = from; i < path.Count; i++) lr.SetPosition(i - from + 1, grid.CellCenter(path[i]));
        }

        TextMeshProUGUI Label()
        {
            if (labelUsed < labels.Count) return labels[labelUsed++];
            TextMeshProUGUI t = Widgets.Label("E", labelRoot, catalog, "", 24f, TextAlignmentOptions.Bottom);
            t.rectTransform.sizeDelta = new Vector2(400f, 80f);
            labels.Add(t);
            labelUsed++;
            return t;
        }

        void RefreshHeatmap(GridMap grid, LightGrid light)
        {
            var pixels = new Color32[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    float l = light != null ? light.At(x, y) : 1f;
                    byte a = grid.GetTile(x, y) == TileType.Wall ? (byte)0 : (byte)110;
                    pixels[y * grid.Width + x] = new Color32((byte)(l * 255), (byte)(l * 200), (byte)((1f - l) * 255), a);
                }
            heatTexture.SetPixels32(pixels);
            heatTexture.Apply();
        }

        public void Tick(bool toggle, GameManager game, Camera camera, PerfProbe perf, PerfProbe aiPerf, QwenBarkSource qwen)
        {
            if (toggle) SetVisible(!Visible);
            noises.RemoveAll(n => GameClock.Time - n.time > NoiseShowSeconds);
            if (!Visible) return;
            used = 0;
            labelUsed = 0;
            GridMap grid = game.Grid;
            if (Time.frameCount % 15 == 0 || toggle) RefreshHeatmap(grid, game.Light);

            if (Input.GetMouseButtonDown(1))
            {
                Vector2 world = camera.ScreenToWorldPoint(Input.mousePosition);
                selectedEnemy = -1;
                foreach (EnemyController enemy in game.Enemies)
                    if (!enemy.IsDead && Vector2.Distance(enemy.Position, world) < 0.8f) selectedEnemy = enemy.Id;
            }

            foreach (EnemyController enemy in game.Enemies)
            {
                if (enemy.IsDead) continue;
                Color stateColor = enemy.State == EnemyState.Engage || enemy.State == EnemyState.Chase ? new Color(1f, 0.25f, 0.25f) :
                    enemy.State == EnemyState.Investigate || enemy.State == EnemyState.Search ? new Color(1f, 0.85f, 0.2f) : new Color(0.6f, 0.9f, 1f);
                Cone(enemy.Position, enemy.Facing, enemy.Fov, enemy.ViewRange, new Color(stateColor.r, stateColor.g, stateColor.b, 0.6f));
                Circle(enemy.Position, 0.45f, new Color(0.9f, 0.2f, 0.2f), 12);
                Path(grid, enemy.CurrentPath, enemy.PathIndex, enemy.Position, new Color(0.3f, 1f, 0.4f));
                TextMeshProUGUI label = Label();
                Vector3 screen = camera.WorldToScreenPoint(enemy.Position + Vector2.up * 1.6f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
                label.rectTransform.anchoredPosition = local;
                label.text = "#" + enemy.Id + " " + enemy.State + "\n게이지 " + enemy.Gauge.ToString("F0") + " 의심 " + enemy.Suspicion.Value.ToString("F0") + " " + SuspicionMeter.TensionKo(enemy.Tension);
                label.color = stateColor;
            }
            Circle(game.Player.Position, 0.45f, new Color(0.3f, 0.6f, 1f), 12);
            foreach (var n in noises) Circle(n.pos, n.radius, new Color(1f, 0.9f, 0.3f, 0.5f), 40);
            for (int i = used; i < lines.Count; i++) lines[i].enabled = false;
            for (int i = labelUsed; i < labels.Count; i++) labels[i].text = "";

            var sb = new StringBuilder();
            sb.Append("FPS ").Append((1000f / Mathf.Max(0.01f, perf.AverageFrameMs)).ToString("F0"))
              .Append("  프레임 ").Append(perf.AverageFrameMs.ToString("F1")).Append("ms (최대 ").Append(perf.MaxFrameMs.ToString("F1")).Append(")\n");
            sb.Append("AI ").Append(aiPerf.AverageSectionMs.ToString("F2")).Append("ms (최대 ").Append(aiPerf.MaxSectionMs.ToString("F2")).Append(")  경로 ").Append(game.Director.PathsThisFrame).Append("건/프레임\n");
            sb.Append("전역 경계 ").Append(game.Director.GlobalAlertLevel).Append("  돌풍 ").Append(game.Director.GustActive ? "예" : "아니오").Append('\n');
            if (qwen != null)
            {
                sb.Append("Qwen ").Append(qwen.Status).Append("  요청 ").Append(qwen.Requests).Append(" 성공 ").Append(qwen.Successes)
                  .Append(" 캐시 ").Append(qwen.CacheHits).Append(" 평균 ").Append(qwen.AverageLatencyMs.ToString("F0")).Append("ms 대기 ").Append(qwen.QueueCount).Append('\n');
                foreach (var kv in qwen.FailCounts) sb.Append("  ").Append(kv.Key).Append(' ').Append(kv.Value).Append('\n');
            }
            sb.Append("(적을 오른쪽 클릭하면 마지막 상황 입력)");
            stats.text = sb.ToString();
            detail.text = "";
            if (selectedEnemy >= 0 && qwen != null)
            {
                QwenLogEntry? entry = qwen.LastFor(selectedEnemy);
                detail.text = entry.HasValue
                    ? "#" + selectedEnemy + " " + entry.Value.Code + " → " + entry.Value.Result + " " + entry.Value.LatencyMs.ToString("F0") + "ms\n입력 " + entry.Value.Input + "\n출력 " + entry.Value.Raw
                    : "#" + selectedEnemy + " Qwen 기록 없음";
            }
        }
    }
}
