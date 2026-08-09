using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GridSystem))]
public class BuildGridVisualizer : MonoBehaviour
{
    [Header("References")]
    public PlayerBuilder playerBuilder;

    [Header("Appearance")]
    public int radiusInCells = 12;
    public float yOffset = 0.03f;
    public Color gridLineColor = new Color(1f, 1f, 1f, 0.22f);
    public Color validCellColor = new Color(0.25f, 1f, 0.4f, 0.32f);
    public Color invalidCellColor = new Color(1f, 0.3f, 0.25f, 0.32f);

    private GridSystem grid;
    private Transform gridPlane;
    private Transform cellHighlight;
    private Material gridMaterial;
    private Material highlightMaterial;
    private Texture2D gridTexture;
    private int lastDiameter = -1;

    void Awake()
    {
        grid = GetComponent<GridSystem>();

        if (playerBuilder == null)
            playerBuilder = FindFirstObjectByType<PlayerBuilder>();

        CreateVisuals();
        SetVisible(false);
    }

    void LateUpdate()
    {
        if (playerBuilder == null || grid == null)
        {
            SetVisible(false);
            return;
        }

        if (!playerBuilder.IsBuildModeActive)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        UpdateVisuals(
            playerBuilder.CurrentPlacementPosition,
            playerBuilder.CanPlaceCurrent
        );
    }

    void CreateVisuals()
    {
        gridPlane = CreatePlaneChild("BuildGridPlane", gridLineColor, out gridMaterial);
        cellHighlight = CreatePlaneChild("BuildGridHighlight", validCellColor, out highlightMaterial);

        gridTexture = CreateCellBorderTexture(64);
        gridMaterial.mainTexture = gridTexture;
        gridMaterial.color = Color.white;

        highlightMaterial.mainTexture = null;
    }

    Transform CreatePlaneChild(string objectName, Color color, out Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = objectName;
        go.transform.SetParent(transform, false);

        Destroy(go.GetComponent<Collider>());

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        material = CreateTransparentMaterial(color);
        material.renderQueue = 3100;
        renderer.sharedMaterial = material;

        return go.transform;
    }

    static Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Diffuse");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    static Texture2D CreateCellBorderTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color line = new Color(1f, 1f, 1f, 1f);
        int border = Mathf.Max(1, size / 32);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder =
                    x < border ||
                    y < border ||
                    x >= size - border ||
                    y >= size - border;

                texture.SetPixel(x, y, isBorder ? line : clear);
            }
        }

        texture.Apply();
        return texture;
    }

    void UpdateVisuals(Vector3 placementPosition, bool canPlace)
    {
        Vector2Int cell = grid.WorldToCell(placementPosition);
        float surfaceY = placementPosition.y + yOffset;
        int diameter = radiusInCells * 2 + 1;
        float worldSize = diameter * grid.cellSize;
        float planeScale = worldSize / 10f;

        if (diameter != lastDiameter)
        {
            gridMaterial.mainTextureScale = new Vector2(diameter, diameter);
            lastDiameter = diameter;
        }

        Vector3 gridCenter = grid.GetCellCenter(cell, surfaceY);
        gridPlane.position = gridCenter;
        gridPlane.localScale = new Vector3(planeScale, 1f, planeScale);

        cellHighlight.position = grid.GetCellCenter(cell, surfaceY + 0.001f);
        cellHighlight.localScale = new Vector3(
            grid.cellSize / 10f * 0.96f,
            1f,
            grid.cellSize / 10f * 0.96f
        );

        highlightMaterial.color = canPlace ? validCellColor : invalidCellColor;
    }

    void SetVisible(bool visible)
    {
        if (gridPlane != null)
            gridPlane.gameObject.SetActive(visible);

        if (cellHighlight != null)
            cellHighlight.gameObject.SetActive(visible);
    }

    void OnDestroy()
    {
        if (gridMaterial != null)
            Destroy(gridMaterial);

        if (highlightMaterial != null)
            Destroy(highlightMaterial);

        if (gridTexture != null)
            Destroy(gridTexture);
    }
}
