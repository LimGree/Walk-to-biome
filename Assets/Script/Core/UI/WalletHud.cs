using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WalletHud : MonoBehaviour
{
    TextMeshProUGUI coinsText;
    TextMeshProUGUI rubiesText;
    GameObject shopRoot;
    TextMeshProUGUI shopInfo;

    void Start()
    {
        Build();
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged -= Refresh;
    }

    void Build()
    {
        GameObject canvasGo = new GameObject("WalletHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image chip = UiTheme.AddImage(canvasGo.transform, "Chip", new Vector2(320f, 78f), UiTheme.Chip);
        RectTransform chipRt = chip.rectTransform;
        chipRt.anchorMin = new Vector2(0f, 1f);
        chipRt.anchorMax = new Vector2(0f, 1f);
        chipRt.pivot = new Vector2(0f, 1f);
        chipRt.anchoredPosition = new Vector2(24f, -18f);

        coinsText = UiTheme.AddText(chip.transform, "Coins", "0", 22f, UiTheme.Warn);
        Stretch(coinsText.rectTransform, 0.08f, 0.52f, 0.08f, 0.92f);
        rubiesText = UiTheme.AddText(chip.transform, "Rubies", "0", 20f, UiTheme.Accent);
        Stretch(rubiesText.rectTransform, 0.08f, 0.52f, 0.08f, 0.48f);

        Button shopBtn = CreateTextButton(chip.transform, "ShopBtn", "Магазин", new Vector2(0.56f, 0.18f), new Vector2(0.94f, 0.82f));
        shopBtn.onClick.AddListener(ToggleShop);

        shopRoot = new GameObject("Shop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        shopRoot.transform.SetParent(canvasGo.transform, false);
        UiTheme.StylePanel(shopRoot, new Vector2(420f, 260f));
        RectTransform shopRt = shopRoot.GetComponent<RectTransform>();
        shopRt.anchorMin = new Vector2(0f, 1f);
        shopRt.anchorMax = new Vector2(0f, 1f);
        shopRt.pivot = new Vector2(0f, 1f);
        shopRt.anchoredPosition = new Vector2(24f, -110f);
        shopRoot.SetActive(false);

        TextMeshProUGUI title = UiTheme.AddText(shopRoot.transform, "Title", "Обмен рубинов", 24f, UiTheme.Accent);
        title.fontStyle = FontStyles.Bold;
        Stretch(title.rectTransform, 0.08f, 0.92f, 0.78f, 0.94f);

        shopInfo = UiTheme.AddText(shopRoot.transform, "Info", "", 18f, UiTheme.Text);
        Stretch(shopInfo.rectTransform, 0.08f, 0.92f, 0.54f, 0.76f);

        Button one = CreateTextButton(shopRoot.transform, "One", "1 рубин", new Vector2(0.08f, 0.28f), new Vector2(0.48f, 0.5f));
        one.onClick.AddListener(() => Exchange(1));
        Button five = CreateTextButton(shopRoot.transform, "Five", "5 рубинов", new Vector2(0.52f, 0.28f), new Vector2(0.92f, 0.5f));
        five.onClick.AddListener(() => Exchange(5));
        Button all = CreateTextButton(shopRoot.transform, "All", "Все рубины", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.24f));
        all.onClick.AddListener(() =>
        {
            if (PlayerWallet.Instance != null)
                Exchange(PlayerWallet.Instance.Rubies);
        });
    }

    void ToggleShop()
    {
        if (shopRoot != null)
            shopRoot.SetActive(!shopRoot.activeSelf);
        Refresh();
    }

    void Exchange(int rubies)
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.TryExchangeRubies(rubies);
        Refresh();
    }

    void Refresh()
    {
        PlayerWallet wallet = PlayerWallet.Instance;
        int coins = wallet != null ? wallet.Coins : 0;
        int rubies = wallet != null ? wallet.Rubies : 0;
        if (coinsText != null)
            coinsText.text = "Монеты  " + coins;
        if (rubiesText != null)
            rubiesText.text = "Рубины  " + rubies;
        if (shopInfo != null)
            shopInfo.text = "1 рубин = " + Economy.CoinsPerRuby + " монет\nСейчас: " + rubies + " руб.  →  +" + (rubies * Economy.CoinsPerRuby) + " монет";
    }

    static Button CreateTextButton(Transform parent, string name, string label, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        UiTheme.StyleImage(go.GetComponent<Image>(), UiTheme.Card);
        Button button = go.GetComponent<Button>();
        TextMeshProUGUI text = UiTheme.AddText(go.transform, "Label", label, 16f, UiTheme.Text);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform, 0f, 1f, 0f, 1f);
        return button;
    }

    static void Stretch(RectTransform rt, float xMin, float xMax, float yMin, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
