using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class LoadingScreen : MonoBehaviour
{
    VisualElement fill;
    Label status;
    Label percent;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        GameAudio.Ensure();
        GameAudio.PlayMusic("music/mus_menu");
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
        float t = Mathf.Clamp01(value);
        if (fill != null)
            fill.style.width = Length.Percent(t * 100f);
        if (percent != null)
            percent.text = Mathf.RoundToInt(t * 100f) + "%";
        if (status != null)
            status.text = text;
    }

    void BuildUi()
    {
        VisualElement root = IndustryUi.Mount(this, 800);
        var screen = IndustryUi.El("Bg", "bg-menu");
        screen.style.justifyContent = Justify.Center;
        screen.style.alignItems = Align.Center;
        var box = IndustryUi.El("Box", "col");
        box.style.width = 720;
        box.Add(IndustryUi.Text("Title", "WALK OF INDUSTRY", "display"));
        box.Add(IndustryUi.Text("Tag", GameBranding.Tagline, "tagline"));
        status = IndustryUi.Text("Status", "Загрузка…", "body-text");
        box.Add(status);
        var track = IndustryUi.El("Track", "progress-track");
        track.style.marginTop = 18;
        fill = IndustryUi.El("Fill", "progress-fill");
        fill.style.width = Length.Percent(0);
        track.Add(fill);
        box.Add(track);
        percent = IndustryUi.Text("Pct", "0%", "muted");
        percent.style.marginTop = 10;
        box.Add(percent);
        screen.Add(box);
        root.Add(screen);
    }
}
