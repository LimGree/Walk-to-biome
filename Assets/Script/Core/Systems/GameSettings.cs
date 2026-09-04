using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class GameSettings
{
    public static event Action Changed;

    static float sunBase = -1f;
    static Color ambientBase;
    static bool ambientCaptured;
    static Light sun;
    static int appliedQuality = -1;
    static Material skyRuntime;

    public static float WorldLight
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat("GfxWorldLight", 1f), 0.15f, 2.5f);
        set { SetFloat("GfxWorldLight", Mathf.Clamp(value, 0.15f, 2.5f)); Apply(); }
    }

    public const float ObjectDistMin = 16f;
    public const float ObjectDistMax = 140f;
    public const float ObjectDistDefault = 32f;

    public static float RenderDistance
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat("GfxRenderDistance", ObjectDistDefault), ObjectDistMin, ObjectDistMax);
        set { SetFloat("GfxRenderDistance", Mathf.Clamp(value, ObjectDistMin, ObjectDistMax)); Apply(); }
    }

    public static bool FogEnabled
    {
        get => PlayerPrefs.GetInt("GfxFog", 1) != 0;
        set { SetInt("GfxFog", value ? 1 : 0); Apply(); }
    }

    public static float FogStart
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat("GfxFogStart", 1f), 1f, 2000f);
        set { SetFloat("GfxFogStart", Mathf.Clamp(value, 1f, 2000f)); Apply(); }
    }

    public static float FogEnd
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat("GfxFogEnd", 160f), 1f, 4000f);
        set { SetFloat("GfxFogEnd", Mathf.Clamp(value, 1f, 4000f)); Apply(); }
    }

    public static float Brightness
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat("GfxBrightness", 1f), 0.35f, 2f);
        set { SetFloat("GfxBrightness", Mathf.Clamp(value, 0.35f, 2f)); Apply(); }
    }

    public static int DisplayMode
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("GfxDisplayMode", DefaultDisplayMode()), 0, 2);
        set { SetInt("GfxDisplayMode", Mathf.Clamp(value, 0, 2)); ApplyDisplay(); }
    }

    public static int ResWidth
    {
        get => Mathf.Max(640, PlayerPrefs.GetInt("GfxResW", Screen.currentResolution.width));
        set => SetInt("GfxResW", Mathf.Max(640, value));
    }

    public static int ResHeight
    {
        get => Mathf.Max(480, PlayerPrefs.GetInt("GfxResH", Screen.currentResolution.height));
        set => SetInt("GfxResH", Mathf.Max(480, value));
    }

    public static bool VSync
    {
        get => PlayerPrefs.GetInt("GfxVSync", 1) != 0;
        set { SetInt("GfxVSync", value ? 1 : 0); Apply(); }
    }

    public static int FpsCap
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("GfxFpsCap", 0), 0, 360);
        set { SetInt("GfxFpsCap", Mathf.Clamp(value, 0, 360)); Apply(); }
    }

    public static int Quality
    {
        get
        {
            int max = Mathf.Max(0, QualitySettings.names.Length - 1);
            return Mathf.Clamp(PlayerPrefs.GetInt("GfxQuality", QualitySettings.GetQualityLevel()), 0, max);
        }
        set
        {
            int max = Mathf.Max(0, QualitySettings.names.Length - 1);
            SetInt("GfxQuality", Mathf.Clamp(value, 0, max));
            Apply();
        }
    }

    static void MigrateFogDistance()
    {
        if (PlayerPrefs.GetInt("GfxFogDistV3", 0) != 0)
            return;
        float start = PlayerPrefs.GetFloat("GfxFogStart", 1f);
        float end = PlayerPrefs.GetFloat("GfxFogEnd", 160f);
        bool oldFar = (Mathf.Approximately(start, 180f) && Mathf.Approximately(end, 900f))
            || (Mathf.Approximately(start, 50f) && Mathf.Approximately(end, 360f));
        if (oldFar)
        {
            PlayerPrefs.SetFloat("GfxFogStart", 1f);
            PlayerPrefs.SetFloat("GfxFogEnd", 160f);
        }
        PlayerPrefs.SetInt("GfxFogDistV3", 1);
        PlayerPrefs.Save();
    }

    static void MigrateObjectDistance()
    {
        if (PlayerPrefs.GetInt("GfxObjDistV1", 0) != 0)
            return;
        float v = PlayerPrefs.GetFloat("GfxRenderDistance", ObjectDistDefault);
        if (v > ObjectDistMax)
            PlayerPrefs.SetFloat("GfxRenderDistance", ObjectDistDefault);
        PlayerPrefs.SetInt("GfxObjDistV1", 1);
        PlayerPrefs.Save();
    }

    public static void Apply()
    {
        MigrateFogDistance();
        MigrateObjectDistance();
        int quality = Quality;
        if (QualitySettings.names != null && QualitySettings.names.Length > 0 && appliedQuality != quality)
        {
            sunBase = -1f;
            ambientCaptured = false;
            appliedQuality = quality;
            QualitySettings.SetQualityLevel(quality, true);
        }

        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FpsCap <= 0 ? -1 : FpsCap;

        CaptureDefaults();
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || light.type != LightType.Directional)
                continue;
            if (sunBase < 0f)
                sunBase = light.intensity;
            light.intensity = sunBase * WorldLight;
        }

        float lightMul = Mathf.Clamp(WorldLight * Brightness, 0.12f, 2.5f);
        Color sunColor = sun != null ? sun.color : Color.white;
        Color lightTint = sunColor * lightMul;
        lightTint.a = 1f;
        Shader.SetGlobalColor("_WalkLightTint", lightTint);

        if (ambientCaptured)
            RenderSettings.ambientLight = ambientBase * Brightness;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientIntensity = Brightness;
        RenderSettings.ambientSkyColor = lightTint * 0.85f;
        RenderSettings.ambientEquatorColor = lightTint * 0.55f;
        RenderSettings.ambientGroundColor = lightTint * 0.22f;

        float objects = RenderDistance;
        float far = Mathf.Clamp(Mathf.Max(240f, objects * 4f), 240f, 700f);
        float start = Mathf.Clamp(FogStart, 1f, Mathf.Max(2f, far - 4f));
        float end = Mathf.Clamp(Mathf.Max(start + 4f, FogEnd), start + 4f, far);
        Color fogColor = Color.Lerp(
            new Color(0.62f, 0.72f, 0.82f, 1f),
            lightTint,
            0.28f);
        fogColor *= Mathf.Lerp(0.45f, 1.15f, Mathf.InverseLerp(0.12f, 2f, lightMul));
        fogColor.a = 1f;
        RenderSettings.fog = FogEnabled;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = start;
        RenderSettings.fogEndDistance = end;
        RenderSettings.fogDensity = Mathf.Clamp(2.4f / end, 0.004f, 0.08f);
        if (FogEnabled)
        {
            Shader.EnableKeyword("FOG_LINEAR");
            Shader.DisableKeyword("FOG_EXP");
            Shader.DisableKeyword("FOG_EXP2");
        }
        else
        {
            Shader.DisableKeyword("FOG_LINEAR");
            Shader.DisableKeyword("FOG_EXP");
            Shader.DisableKeyword("FOG_EXP2");
        }

        ApplySky(fogColor, lightMul);
        QualitySettings.shadowDistance = Mathf.Clamp(objects * 0.8f, 20f, objects);
        Camera[] cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++)
        {
            Camera cam = cams[i];
            if (cam == null || cam.orthographic)
                continue;
            cam.farClipPlane = far;
            cam.backgroundColor = fogColor;
            if (cam.clearFlags == CameraClearFlags.SolidColor || cam.clearFlags == CameraClearFlags.Skybox)
                cam.clearFlags = skyRuntime != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            ApplyObjectCull(cam, objects);
        }

        Terrain[] terrains = Terrain.activeTerrains;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null)
                continue;
            terrain.treeDistance = objects;
            terrain.basemapDistance = objects;
            terrain.detailObjectDistance = Mathf.Clamp(objects * 0.5f, 16f, objects);
        }

        Changed?.Invoke();
    }

    static void ApplyObjectCull(Camera cam, float dist)
    {
        float[] distances = cam.layerCullDistances;
        if (distances == null || distances.Length != 32)
            distances = new float[32];
        int buildings = LayerMask.NameToLayer("buildings");
        int resources = LayerMask.NameToLayer("RESOURSES");
        if (buildings >= 0)
            distances[buildings] = dist;
        if (resources >= 0)
            distances[resources] = dist;
        cam.layerCullDistances = distances;
        cam.layerCullSpherical = true;
    }

    static void ApplySky(Color fogColor, float lightMul)
    {
        if (skyRuntime == null)
        {
            Shader shader = Resources.Load<Shader>("WalkToBiomeSky");
            if (shader == null)
                shader = Shader.Find("Hidden/WalkToBiome/Sky");
            if (shader != null)
            {
                skyRuntime = new Material(shader);
                skyRuntime.name = "WalkRuntimeSky";
            }
        }

        if (skyRuntime == null)
            return;

        float energy = Mathf.InverseLerp(0.12f, 1.8f, lightMul);
        Color sky = Color.Lerp(new Color(0.06f, 0.08f, 0.14f, 1f), new Color(0.42f, 0.66f, 0.98f, 1f), energy);
        Color horizon = Color.Lerp(new Color(0.12f, 0.14f, 0.18f, 1f), new Color(0.78f, 0.86f, 0.94f, 1f), energy);
        Color ground = Color.Lerp(new Color(0.08f, 0.07f, 0.06f, 1f), new Color(0.32f, 0.30f, 0.26f, 1f), energy);
        if (FogEnabled)
        {
            sky = Color.Lerp(sky, fogColor, 0.42f);
            horizon = fogColor;
            ground = Color.Lerp(ground, fogColor, 0.62f);
        }

        if (skyRuntime.HasProperty("_SkyColor"))
            skyRuntime.SetColor("_SkyColor", sky);
        if (skyRuntime.HasProperty("_HorizonColor"))
            skyRuntime.SetColor("_HorizonColor", horizon);
        if (skyRuntime.HasProperty("_GroundColor"))
            skyRuntime.SetColor("_GroundColor", ground);
        if (skyRuntime.HasProperty("_Exposure"))
            skyRuntime.SetFloat("_Exposure", Mathf.Clamp(lightMul, 0.35f, 1.6f));
        RenderSettings.skybox = skyRuntime;
    }

    public static void ApplyDisplay()
    {
        FullScreenMode mode = DisplayMode == 0
            ? FullScreenMode.ExclusiveFullScreen
            : DisplayMode == 1
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
        Screen.SetResolution(ResWidth, ResHeight, mode);
        Apply();
    }

    public static void SetResolution(int width, int height)
    {
        ResWidth = width;
        ResHeight = height;
        ApplyDisplay();
    }

    public static List<string> ResolutionChoices(out int selected)
    {
        var list = new List<string>();
        selected = 0;
        Resolution[] found = Screen.resolutions;
        string current = ResWidth + " × " + ResHeight;
        var seen = new HashSet<string>();
        for (int i = 0; i < found.Length; i++)
        {
            string label = found[i].width + " × " + found[i].height;
            if (!seen.Add(label))
                continue;
            if (label == current)
                selected = list.Count;
            list.Add(label);
        }
        if (list.Count == 0)
        {
            list.Add(current);
            selected = 0;
        }
        else if (!seen.Contains(current))
        {
            list.Insert(0, current);
            selected = 0;
        }
        return list;
    }

    public static bool TryParseResolution(string label, out int width, out int height)
    {
        width = ResWidth;
        height = ResHeight;
        if (string.IsNullOrEmpty(label))
            return false;
        string[] parts = label.Split('×');
        if (parts.Length != 2)
            parts = label.Split('x');
        if (parts.Length != 2)
            return false;
        return int.TryParse(parts[0].Trim(), out width) && int.TryParse(parts[1].Trim(), out height);
    }

    static int DefaultDisplayMode()
    {
        switch (Screen.fullScreenMode)
        {
            case FullScreenMode.ExclusiveFullScreen:
                return 0;
            case FullScreenMode.FullScreenWindow:
                return 1;
            default:
                return 2;
        }
    }

    static void CaptureDefaults()
    {
        if (!ambientCaptured)
        {
            ambientBase = RenderSettings.ambientLight;
            ambientCaptured = true;
        }

        if (sun == null)
            sun = RenderSettings.sun;
        if (sun != null && sun.Equals(null))
        {
            sun = null;
            sunBase = -1f;
        }
        if (sun == null)
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    sun = lights[i];
                    break;
                }
            }
        }
        if (sun != null && sunBase < 0f)
            sunBase = sun.intensity;
    }

    static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }

    static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }
}
