using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    Image fill;
    TextMeshProUGUI status;
    TextMeshProUGUI percent;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        BuildUi();
        StartCoroutine(LoadGame());
    }

    IEnumerator LoadGame()
    {
        SetProgress(0.05f, "Загрузка мира…");
        yield return null;

        AsyncOperation op = SceneManager.LoadSceneAsync(MainMenu.GameSceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            SetProgress(Mathf.Lerp(0.05f, 0.7f, op.progress / 0.9f), "Загрузка сцены…");
            yield return null;
        }

        SetProgress(0.72f, "Запуск мира…");
        op.allowSceneActivation = true;
        while (!op.isDone)
            yield return null;

        yield return null;
        SetProgress(0.82f, "Биомы…");
        yield return WaitReady(20f, () => WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsReady);
        if (WorldBiomeMap.Instance == null || !WorldBiomeMap.Instance.IsReady)
        {
            yield return FailToMenu("Мир не собрал биомы.");
            yield break;
        }

        SetProgress(0.9f, "Ресурсы…");
        yield return WaitReady(20f, () => WorldResourceScatterer.Instance != null && WorldResourceScatterer.Instance.IsScattered);
        if (WorldResourceScatterer.Instance == null || !WorldResourceScatterer.Instance.IsScattered)
        {
            yield return FailToMenu("Мир не разбросал ресурсы.");
            yield break;
        }

        SetProgress(0.96f, "Сохранение…");
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.LoadGame();

        SetProgress(1f, "Готово");
        yield return null;
        Destroy(gameObject);
    }

    IEnumerator WaitReady(float seconds, System.Func<bool> ready)
    {
        float timeout = Time.unscaledTime + seconds;
        while (!ready())
        {
            if (Time.unscaledTime > timeout)
                yield break;
            yield return null;
        }
    }

    IEnumerator FailToMenu(string reason)
    {
        SetProgress(0f, reason + "  Возврат в меню…");
        if (percent != null)
            percent.text = "";
        yield return new WaitForSecondsRealtime(2.2f);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(MainMenu.MenuSceneName);
        Destroy(gameObject);
    }

    void SetProgress(float value, string text)
    {
        if (fill != null)
            fill.fillAmount = Mathf.Clamp01(value);
        if (percent != null)
            percent.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        if (status != null)
            status.text = text;
    }

    void BuildUi()
    {
        GameObject canvasGo = new GameObject("LoadingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject bg = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.04f, 0.10f, 0.07f, 1f);

        TextMeshProUGUI title = UiTheme.AddText(canvasGo.transform, "Title", "WALK TO BIOME", 52f, UiTheme.Accent);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.1f, 0.58f);
        titleRt.anchorMax = new Vector2(0.9f, 0.72f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        status = UiTheme.AddText(canvasGo.transform, "Status", "Загрузка…", 24f, UiTheme.Text);
        status.alignment = TextAlignmentOptions.Center;
        RectTransform stRt = status.rectTransform;
        stRt.anchorMin = new Vector2(0.15f, 0.44f);
        stRt.anchorMax = new Vector2(0.85f, 0.52f);
        stRt.offsetMin = Vector2.zero;
        stRt.offsetMax = Vector2.zero;

        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bar.transform.SetParent(canvasGo.transform, false);
        RectTransform barRt = bar.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.22f, 0.36f);
        barRt.anchorMax = new Vector2(0.78f, 0.41f);
        barRt.offsetMin = Vector2.zero;
        barRt.offsetMax = Vector2.zero;
        UiTheme.StyleImage(bar.GetComponent<Image>(), UiTheme.Chip);

        fill = UiTheme.AddImage(bar.transform, "Fill", Vector2.zero, UiTheme.Accent);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        RectTransform fillRt = fill.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(4f, 4f);
        fillRt.offsetMax = new Vector2(-4f, -4f);

        percent = UiTheme.AddText(canvasGo.transform, "Percent", "0%", 22f, UiTheme.TextDim);
        percent.alignment = TextAlignmentOptions.Center;
        RectTransform pRt = percent.rectTransform;
        pRt.anchorMin = new Vector2(0.3f, 0.28f);
        pRt.anchorMax = new Vector2(0.7f, 0.35f);
        pRt.offsetMin = Vector2.zero;
        pRt.offsetMax = Vector2.zero;
    }
}
