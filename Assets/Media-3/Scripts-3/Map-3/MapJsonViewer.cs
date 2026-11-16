
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class MapJsonData
{
    public string version;
    public string id;
    public string name;
    public string mission;
    public float target_time_s;
    public string ambient;
    public float cell_m;
    public string[] rows;
}

[Serializable]
public class PropLayoutData
{
    public PropEntry[] objects;
}

[Serializable]
public class PropEntry
{
    public string id;
    public string asset;
    public int x;
    public int y;
    public bool blocks_movement;
}

public class MapJsonViewer : MonoBehaviour
{
    [Header("JSON 파일")]
    [SerializeField] private TextAsset mapJson;
    [SerializeField] private TextAsset propLayoutJson;

    [Header("맵 설정")]
    [SerializeField] private bool buildOnStart = true;
    [SerializeField] private bool addWallCollider = true;
    [SerializeField] private bool addPropCollider = true;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float cameraPadding = 1.1f;

    [Header("가구 크기 설정")]
    [SerializeField] private float propCellScale = 1f;

    [Header("색상")]
    [SerializeField] private Color floorColor = new Color32(75, 82, 86, 255);
    [SerializeField] private Color wallColor = new Color32(30, 35, 40, 255);
    [SerializeField] private Color doorColor = new Color32(145, 105, 65, 255);
    [SerializeField] private Color windowColor = new Color32(75, 155, 190, 255);
    [SerializeField] private Color playerColor = new Color32(80, 210, 120, 255);
    [SerializeField] private Color targetColor = new Color32(240, 200, 65, 255);
    [SerializeField] private Color exitColor = new Color32(70, 190, 220, 255);
    [SerializeField] private Color enemyColor = new Color32(210, 65, 65, 255);

    private Transform mapRoot;
    private Sprite tileSprite;

    private readonly Dictionary<string, Sprite> propSpriteCache =
        new Dictionary<string, Sprite>();

    private void Start()
    {
        if (buildOnStart)
            BuildMap();
    }

    [ContextMenu("Build Map")]
    public void BuildMap()
    {
        if (mapJson == null)
        {
            Debug.LogError("맵 JSON을 지정하세요.");
            return;
        }

        ClearMap();

        MapJsonData data = JsonUtility.FromJson<MapJsonData>(mapJson.text);

        if (data == null || data.rows == null || data.rows.Length == 0)
        {
            Debug.LogError("맵 JSON에 rows가 없습니다.");
            return;
        }

        int width = data.rows[0].Length;
        int height = data.rows.Length;

        for (int y = 0; y < height; y++)
        {
            if (string.IsNullOrEmpty(data.rows[y]) ||
                data.rows[y].Length != width)
            {
                Debug.LogError((y + 1) + "번째 행의 길이가 다릅니다.");
                return;
            }
        }

        if (data.cell_m <= 0)
        {
            Debug.LogError("cell_m 값이 잘못되었습니다.");
            return;
        }

        GameObject root = new GameObject("Generated_Map");
        mapRoot = root.transform;
        mapRoot.SetParent(transform, false);

        CreateTileSprite();

        float cell = data.cell_m;
        float startX = -(width * cell) / 2f + cell / 2f;
        float startY = (height * cell) / 2f - cell / 2f;

        // 1. 맵 타일 생성
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char symbol = data.rows[y][x];

                if (symbol == ' ' || symbol == ',')
                    continue;

                Color color;
                bool isWall = false;

                switch (symbol)
                {
                    case '#': color = wallColor; isWall = true; break;
                    case '.': color = floorColor; break;
                    case 'D': color = doorColor; break;
                    case 'W': color = windowColor; break;
                    case 'P': color = playerColor; break;
                    case 'T': color = targetColor; break;
                    case 'X': color = exitColor; break;
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6': color = enemyColor; break;

                    default:
                        Debug.LogWarning("알 수 없는 기호: " + symbol);
                        continue;
                }

                Vector3 position = new Vector3(
                    startX + x * cell,
                    startY - y * cell,
                    0
                );

                CreateTile(symbol, position, cell, color, isWall);
            }
        }

        // 2. 가구 생성
        BuildProps(width, height, cell, startX, startY);

        // 3. 카메라 조정
        AdjustCamera(width, height, cell);

        Debug.Log("맵 생성 완료: " + width + " x " + height);
    }

    // ============================================================
    // 가구 배치
    // ============================================================
    private void BuildProps(
        int width, int height, float cell,
        float startX, float startY)
    {
        if (propLayoutJson == null)
        {
            Debug.LogWarning("가구 JSON이 지정되지 않았습니다.");
            return;
        }

        PropLayoutData layout =
            JsonUtility.FromJson<PropLayoutData>(propLayoutJson.text);

        if (layout == null || layout.objects == null)
        {
            Debug.LogError("가구 JSON의 objects 데이터를 확인하세요.");
            return;
        }

        int placedCount = 0;

        foreach (PropEntry item in layout.objects)
        {
            if (item == null)
                continue;

            if (item.x < 0 || item.x >= width ||
                item.y < 0 || item.y >= height)
            {
                Debug.LogWarning("맵 범위 밖 가구: " + item.id);
                continue;
            }

            if (string.IsNullOrEmpty(item.asset))
            {
                Debug.LogWarning("가구 경로가 비어 있습니다: " + item.id);
                continue;
            }

            string path = item.asset
                .Replace("\\", "/")
                .Replace(".png", "");

            Sprite sprite = FindPropSprite(path);

            if (sprite == null)
            {
                Debug.LogError(
                    "가구 이미지를 찾지 못했습니다.\n" +
                    "ID: " + item.id + "\n" +
                    "경로: " + path
                );
                continue;
            }

            GameObject obj = new GameObject(item.id);
            obj.transform.SetParent(mapRoot, false);

            obj.transform.position = new Vector3(
                startX + item.x * cell,
                startY - item.y * cell,
                -0.1f
            );

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;

            // 타일과 겹쳐도 가구가 표시되도록 렌더링 순서 설정
            sr.sortingOrder = 5;

            // 원본 가구 비율을 유지하면서 크기 조정
            float spriteWidth = sprite.bounds.size.x;
            float spriteHeight = sprite.bounds.size.y;

            if (spriteWidth > 0 && spriteHeight > 0)
            {
                float scaleX = cell / spriteWidth;
                float scaleY = cell / spriteHeight;

                float scale = Mathf.Min(scaleX, scaleY);

                obj.transform.localScale = new Vector3(
                    scale * propCellScale,
                    scale * propCellScale,
                    1f
                );
            }

            if (addPropCollider && item.blocks_movement)
            {
                BoxCollider2D collider =
                    obj.AddComponent<BoxCollider2D>();

                collider.size = sprite.bounds.size;
            }

            placedCount++;
        }

        Debug.Log(
            "가구 배치 완료: " + placedCount +
            " / " + layout.objects.Length + "개"
        );
    }

    // ============================================================
    // 가구 에셋 검색
    // ============================================================
    private Sprite FindPropSprite(string assetPath)
    {
#if UNITY_EDITOR
        string fileName =
            Path.GetFileNameWithoutExtension(assetPath);

        if (string.IsNullOrEmpty(fileName))
            return null;

        if (propSpriteCache.TryGetValue(fileName, out Sprite cached))
            return cached;

        string[] guids = AssetDatabase.FindAssets(
            "t:Sprite",
            new[] { "Assets/Media-3" }
        );

        // Sprite 에셋 검색
        foreach (string guid in guids)
        {
            string candidatePath = AssetDatabase.GUIDToAssetPath(guid);

            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(candidatePath);

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is Sprite sprite && sprite.name == fileName)
                {
                    propSpriteCache[fileName] = sprite;
                    return sprite;
                }
            }
        }

        // Texture2D 이미지 검색
        string[] textureGuids = AssetDatabase.FindAssets(
            "t:Texture2D",
            new[] { "Assets/Media-3" }
        );

        foreach (string guid in textureGuids)
        {
            string candidatePath = AssetDatabase.GUIDToAssetPath(guid);

            string candidateName =
                Path.GetFileNameWithoutExtension(candidatePath);

            if (candidateName != fileName)
                continue;

            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(candidatePath);

            if (texture == null)
                continue;

            float pixelsPerUnit = 100f;

            TextureImporter importer =
                AssetImporter.GetAtPath(candidatePath) as TextureImporter;

            if (importer != null)
                pixelsPerUnit = importer.spritePixelsPerUnit;

            Sprite createdSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );

            createdSprite.name = fileName;

            propSpriteCache[fileName] = createdSprite;
            return createdSprite;
        }

        Debug.LogWarning("에셋 검색 실패: " + fileName);
#endif

        return null;
    }

    private void CreateTile(
        char symbol, Vector3 position,
        float cell, Color color, bool isWall)
    {
        GameObject tile = new GameObject("Tile_" + symbol);
        tile.transform.SetParent(mapRoot, false);
        tile.transform.localPosition = position;
        tile.transform.localScale = new Vector3(cell, cell, 1);

        SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
        sr.sprite = tileSprite;
        sr.color = color;
        sr.sortingOrder = isWall ? 2 : 0;

        if (isWall && addWallCollider)
            tile.AddComponent<BoxCollider2D>();
    }

    private void CreateTileSprite()
    {
        if (tileSprite != null)
            return;

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        tileSprite = Sprite.Create(
            texture,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            1
        );
    }

    private void AdjustCamera(int width, int height, float cell)
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        cam.orthographic = true;
        cam.transform.position = new Vector3(
            transform.position.x,
            transform.position.y,
            -10
        );

        float mapWidth = width * cell;
        float mapHeight = height * cell;

        cam.orthographicSize = Mathf.Max(
            mapHeight / 2f,
            mapWidth / (2f * cam.aspect)
        ) * cameraPadding;
    }

    [ContextMenu("Clear Map")]
    public void ClearMap()
    {
        Transform oldRoot = transform.Find("Generated_Map");

        if (oldRoot != null)
        {
            if (Application.isPlaying)
                Destroy(oldRoot.gameObject);
            else
                DestroyImmediate(oldRoot.gameObject);
        }

        mapRoot = null;
    }
}
