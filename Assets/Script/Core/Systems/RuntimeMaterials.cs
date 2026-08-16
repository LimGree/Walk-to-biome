using UnityEngine;

/// <summary>
/// Materials that work in a player build. Shader.Find for built-in
/// Unlit/Sprites shaders returns null after Unity strips unused shaders.
/// </summary>
public static class RuntimeMaterials
{
    const string ResourceName = "WalkToBiomeUnlit";
    static Shader cached;

    public static Shader Unlit
    {
        get
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<Shader>(ResourceName);
            if (cached == null)
                cached = Shader.Find("Hidden/WalkToBiome/Unlit");
            if (cached == null)
                cached = Shader.Find("Sprites/Default");
            if (cached == null)
                cached = Shader.Find("Unlit/Color");
            if (cached == null)
                cached = Shader.Find("Hidden/InternalErrorShader");
            return cached;
        }
    }

    public static Material Create(Color color)
    {
        return Create(Texture2D.whiteTexture, color);
    }

    public static Material Create(Texture texture, Color color)
    {
        Shader shader = Unlit;
        if (shader == null)
        {
            Debug.LogError("RuntimeMaterials: no unlit shader in the build.");
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        var mat = new Material(shader);
        if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", texture != null ? texture : Texture2D.whiteTexture);
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        mat.color = color;
        return mat;
    }
}
