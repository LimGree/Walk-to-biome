using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// В режиме стройки: объёмные стрелки I/O на соседней клетке.
/// Оранжевая — вход (к зданию), зелёная — выход (от здания).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GridSystem))]
public class BuildIoArrowVisualizer : MonoBehaviour
{
    static readonly Color InputColor = new Color(1f, 0.5f, 0.1f, 0.42f);
    static readonly Color OutputColor = new Color(0.2f, 0.88f, 0.35f, 0.42f);

    const int PoolSize = 96;
    const float YOffset = 0.04f;

    struct SocketHint
    {
        public bool input;
        public Vector3 localPos;
        public Vector3 localOut;
    }

    PlayerBuilder builder;
    GridSystem grid;
    Mesh arrowMesh;
    Material inputMat;
    Material outputMat;
    readonly List<MeshRenderer> arrows = new List<MeshRenderer>(PoolSize);
    readonly List<Transform> transforms = new List<Transform>(PoolSize);
    readonly Dictionary<int, List<SocketHint>> prefabHints = new Dictionary<int, List<SocketHint>>();

    void Awake()
    {
        grid = GetComponent<GridSystem>();
        builder = FindFirstObjectByType<PlayerBuilder>();
        arrowMesh = CreateArrowMesh();
        inputMat = RuntimeMaterials.Create(InputColor);
        inputMat.renderQueue = 3200;
        outputMat = RuntimeMaterials.Create(OutputColor);
        outputMat.renderQueue = 3200;

        for (int i = 0; i < PoolSize; i++)
        {
            GameObject go = new GameObject("IoArrow_" + i);
            go.transform.SetParent(transform, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = arrowMesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.sharedMaterial = outputMat;
            go.SetActive(false);
            arrows.Add(renderer);
            transforms.Add(go.transform);
        }
    }

    void LateUpdate()
    {
        if (builder == null)
            builder = FindFirstObjectByType<PlayerBuilder>();
        if (builder == null || grid == null || !builder.isBuildMode)
        {
            HideAll();
            return;
        }

        int used = 0;
        GameObject ghost = builder.CurrentGhost;
        BuildingBase[] buildings = FindObjectsByType<BuildingBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingBase building = buildings[i];
            if (ghost != null && building != null && building.transform.IsChildOf(ghost.transform))
                continue;
            used = DrawBuilding(building, used);
        }

        used = DrawGhost(used);

        for (int i = used; i < transforms.Count; i++)
        {
            if (transforms[i].gameObject.activeSelf)
                transforms[i].gameObject.SetActive(false);
        }
    }

    int DrawGhost(int used)
    {
        GameObject ghost = builder != null ? builder.CurrentGhost : null;
        if (ghost == null || !ghost.activeInHierarchy)
            return used;

        BuildingData data = builder.CurrentBuildingData;
        if (data != null && data.IsConveyor)
            return used;
        if (IsLogisticsOnly(ghost))
            return used;

        List<SocketHint> hints = GetHints(data != null ? data.prefab : null);
        if (hints == null || hints.Count == 0)
        {
            BuildingSocket[] sockets = ghost.GetComponentsInChildren<BuildingSocket>(true);
            for (int i = 0; i < sockets.Length; i++)
                used = DrawSocket(sockets[i], used);
            return used;
        }

        Transform root = ghost.transform;
        for (int i = 0; i < hints.Count; i++)
        {
            SocketHint hint = hints[i];
            Vector3 worldPos = root.TransformPoint(hint.localPos);
            Vector3 worldOut = root.TransformDirection(hint.localOut);
            used = DrawHint(worldPos, worldOut, hint.input, used);
        }

        return used;
    }

    List<SocketHint> GetHints(GameObject prefab)
    {
        if (prefab == null)
            return null;

        int id = prefab.GetInstanceID();
        if (prefabHints.TryGetValue(id, out List<SocketHint> cached))
            return cached;

        var hints = new List<SocketHint>(4);
        CollectHints(prefab, hints);
        if (hints.Count == 0)
        {
            GameObject sample = Instantiate(prefab);
            sample.name = "IoHintSample";
            sample.hideFlags = HideFlags.HideAndDontSave;
            sample.transform.position = new Vector3(0f, -1000f, 0f);
            var renderers = sample.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;
            CollectHints(sample, hints);
            Destroy(sample);
        }

        prefabHints[id] = hints;
        return hints;
    }

    static void CollectHints(GameObject root, List<SocketHint> hints)
    {
        if (root == null)
            return;

        Transform rootTf = root.transform;
        BuildingBase building = root.GetComponent<BuildingBase>();
        if (building is Conveyor)
            return;

        CollectArray(rootTf, building != null ? building.inputSockets : null, true, hints);
        CollectArray(rootTf, building != null ? building.outputSockets : null, false, hints);

        if (hints.Count > 0)
            return;

        BuildingSocket[] sockets = root.GetComponentsInChildren<BuildingSocket>(true);
        for (int i = 0; i < sockets.Length; i++)
        {
            BuildingSocket socket = sockets[i];
            if (socket == null || socket.Owner is Conveyor)
                continue;
            hints.Add(ToHint(rootTf, socket, socket.socketType == SocketType.Input));
        }
    }

    static void CollectArray(Transform root, BuildingSocket[] sockets, bool input, List<SocketHint> hints)
    {
        if (sockets == null)
            return;
        for (int i = 0; i < sockets.Length; i++)
        {
            if (sockets[i] == null)
                continue;
            hints.Add(ToHint(root, sockets[i], input));
        }
    }

    static SocketHint ToHint(Transform root, BuildingSocket socket, bool input)
    {
        Vector3 outward = socket.GetOutward();
        return new SocketHint
        {
            input = input,
            localPos = root.InverseTransformPoint(socket.transform.position),
            localOut = root.InverseTransformDirection(outward).normalized
        };
    }

    static bool IsLogisticsOnly(GameObject go)
    {
        if (go == null)
            return false;
        BuildingBase[] bases = go.GetComponents<BuildingBase>();
        bool hasMachine = false;
        bool hasBelt = false;
        for (int i = 0; i < bases.Length; i++)
        {
            if (bases[i] == null)
                continue;
            if (bases[i] is Conveyor)
                hasBelt = true;
            else
                hasMachine = true;
        }

        if (hasMachine)
            return false;
        return hasBelt || go.GetComponent<Conveyor>() != null;
    }

    int DrawBuilding(BuildingBase building, int used)
    {
        if (building == null || building is Conveyor)
            return used;

        used = DrawSockets(building.inputSockets, true, used);
        used = DrawSockets(building.outputSockets, false, used);
        return used;
    }

    int DrawSockets(BuildingSocket[] sockets, bool input, int used)
    {
        if (sockets == null)
            return used;
        for (int i = 0; i < sockets.Length; i++)
            used = DrawSocket(sockets[i], used, input);
        return used;
    }

    int DrawSocket(BuildingSocket socket, int used, bool? inputOverride = null)
    {
        if (socket == null || used >= transforms.Count)
            return used;

        BuildingBase owner = socket.Owner;
        if (owner is Conveyor)
            return used;

        bool input = inputOverride ?? socket.socketType == SocketType.Input;
        return DrawHint(socket.transform.position, socket.GetOutward(), input, used);
    }

    int DrawHint(Vector3 socketWorldPos, Vector3 outwardWorld, bool input, int used)
    {
        if (used >= transforms.Count || grid == null)
            return used;

        Vector3 outward = BuildingLinker.CardinalToWorld(BuildingLinker.ToCardinal(outwardWorld));
        Vector3 dir = input ? -outward : outward;
        if (dir.sqrMagnitude < 0.0001f)
            dir = input ? -Vector3.forward : Vector3.forward;

        float cell = GridFootprint.CellSize;
        Vector3 probe = socketWorldPos + outward * (cell * 0.55f);
        Vector2Int cellCoord = BuildingLinker.WorldToCell(probe);

        Transform t = transforms[used];
        t.gameObject.SetActive(true);
        t.position = grid.GetCellCenter(cellCoord, socketWorldPos.y + YOffset);
        t.rotation = Quaternion.LookRotation(dir, Vector3.up);
        float scale = grid.cellSize;
        t.localScale = new Vector3(scale, scale, scale);
        arrows[used].sharedMaterial = input ? inputMat : outputMat;
        return used + 1;
    }

    void HideAll()
    {
        for (int i = 0; i < transforms.Count; i++)
        {
            if (transforms[i] != null && transforms[i].gameObject.activeSelf)
                transforms[i].gameObject.SetActive(false);
        }
    }

    static Mesh CreateArrowMesh()
    {
        var verts = new List<Vector3>(48);
        var tris = new List<int>(96);

        float h = 0.28f;
        float shaftW = 0.09f;
        float shaftZ0 = -0.36f;
        float shaftZ1 = 0.02f;
        float headW = 0.22f;
        float tipZ = 0.44f;

        Vector3 slb = new Vector3(-shaftW, 0f, shaftZ0);
        Vector3 srb = new Vector3(shaftW, 0f, shaftZ0);
        Vector3 slt = new Vector3(-shaftW, h, shaftZ0);
        Vector3 srt = new Vector3(shaftW, h, shaftZ0);
        Vector3 flb = new Vector3(-shaftW, 0f, shaftZ1);
        Vector3 frb = new Vector3(shaftW, 0f, shaftZ1);
        Vector3 flt = new Vector3(-shaftW, h, shaftZ1);
        Vector3 frt = new Vector3(shaftW, h, shaftZ1);

        AddQuad(verts, tris, slb, srb, srt, slt);
        AddQuad(verts, tris, flb, slb, slt, flt);
        AddQuad(verts, tris, srb, frb, frt, srt);
        AddQuad(verts, tris, slt, srt, frt, flt);
        AddQuad(verts, tris, srb, slb, flb, frb);

        Vector3 hlb = new Vector3(-headW, 0f, shaftZ1);
        Vector3 hrb = new Vector3(headW, 0f, shaftZ1);
        Vector3 hlt = new Vector3(-headW, h, shaftZ1);
        Vector3 hrt = new Vector3(headW, h, shaftZ1);
        Vector3 tipB = new Vector3(0f, 0f, tipZ);
        Vector3 tipT = new Vector3(0f, h, tipZ);

        AddQuad(verts, tris, hlb, flb, flt, hlt);
        AddQuad(verts, tris, frb, hrb, hrt, frt);
        AddTri(verts, tris, hlb, hrb, tipB);
        AddTri(verts, tris, hlt, tipT, hrt);
        AddQuad(verts, tris, hlb, tipB, tipT, hlt);
        AddQuad(verts, tris, tipB, hrb, hrt, tipT);

        var mesh = new Mesh { name = "BuildIoArrow3D" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        AddTri(verts, tris, a, b, c);
        AddTri(verts, tris, a, c, d);
    }

    static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        int i = verts.Count;
        verts.Add(a);
        verts.Add(b);
        verts.Add(c);
        tris.Add(i);
        tris.Add(i + 1);
        tris.Add(i + 2);
        tris.Add(i);
        tris.Add(i + 2);
        tris.Add(i + 1);
    }

    void OnDestroy()
    {
        if (arrowMesh != null)
            Destroy(arrowMesh);
        if (inputMat != null)
            Destroy(inputMat);
        if (outputMat != null)
            Destroy(outputMat);
    }
}
