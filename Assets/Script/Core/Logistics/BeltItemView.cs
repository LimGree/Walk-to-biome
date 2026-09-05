using UnityEngine;

public static class BeltItemView
{
    public static Transform Create(ItemData item, float itemScale)
    {
        GameObject root = new GameObject(item != null ? "BeltItem_" + item.id : "BeltItem");
        if (!TryAttachWorldModel(root, item, itemScale) && !TryAttachIcon(root, item, itemScale))
            AttachFallbackCube(root, itemScale);

        DisableColliders(root);
        ApplyWorldCullLayer(root);
        return root.transform;
    }

    public static void Prepare(Transform visual)
    {
        if (visual == null)
            return;
        visual.SetParent(null, true);
        DisableColliders(visual.gameObject);
        ApplyWorldCullLayer(visual.gameObject);
    }

    public static void Update(Transform visual, Vector3 position, Vector3 look)
    {
        if (visual == null)
            return;

        visual.position = position;

        SpriteRenderer sprite = visual.GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            Camera cam = WorldView.Cam;
            if (cam != null)
            {
                Vector3 toCam = visual.position - cam.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    visual.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }
            return;
        }

        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            visual.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
    }

    public static void Destroy(Transform visual)
    {
        if (visual != null)
            Object.Destroy(visual.gameObject);
    }

    static bool TryAttachWorldModel(GameObject root, ItemData item, float itemScale)
    {
        if (item == null || item.worldPrefab == null)
            return false;

        GameObject model = Object.Instantiate(item.worldPrefab, root.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        DisableColliders(model);

        if (!FitChildToSize(root.transform, model.transform, itemScale))
        {
            Object.Destroy(model);
            return false;
        }

        return true;
    }

    static bool TryAttachIcon(GameObject root, ItemData item, float itemScale)
    {
        if (item == null || item.icon == null)
            return false;

        SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = item.icon;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;

        float maxDim = Mathf.Max(item.icon.bounds.size.x, item.icon.bounds.size.y, 0.001f);
        root.transform.localScale = Vector3.one * (itemScale / maxDim);
        return true;
    }

    static void AttachFallbackCube(GameObject root, float itemScale)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cube";
        cube.transform.SetParent(root.transform, false);
        cube.transform.localScale = Vector3.one * itemScale;
        DisableColliders(cube);
    }

    static bool FitChildToSize(Transform root, Transform child, float targetSize)
    {
        Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return false;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDim < 0.0001f)
            return false;

        child.localScale *= targetSize / maxDim;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        child.position += root.position - bounds.center;
        return true;
    }

    static void DisableColliders(GameObject go)
    {
        if (go == null)
            return;

        Collider[] cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }
    }

    public static void ApplyWorldCullLayer(GameObject go)
    {
        int layer = LayerMask.NameToLayer("buildings");
        if (layer >= 0)
            SetLayer(go, layer);
    }

    static void SetLayer(GameObject go, int layer)
    {
        if (go == null)
            return;
        go.layer = layer;
        Transform[] kids = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < kids.Length; i++)
        {
            if (kids[i] != null)
                kids[i].gameObject.layer = layer;
        }
    }
}
