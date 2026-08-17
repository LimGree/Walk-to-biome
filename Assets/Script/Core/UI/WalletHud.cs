using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WalletHud : MonoBehaviour
{
    public static WalletHud Instance { get; private set; }

    TextMeshProUGUI coinsText;
    TextMeshProUGUI rubiesText;
    GameObject shopRoot;
    TextMeshProUGUI shopInfo;
    InputAction shopAction;
    bool bound;

    public bool IsShopOpen => shopRoot != null && shopRoot.activeSelf;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Build();
        BindInput();
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged += Refresh;
        Refresh();
    }

    void OnEnable()
    {
        BindInput();
    }

    void OnDisable()
    {
        UnbindInput();
    }

    void OnDestroy()
    {
        UnbindInput();
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged -= Refresh;
        if (Instance == this)
            Instance = null;
    }

    void BindInput()
    {
        if (bound)
            return;
        InputSystem_Actions actions = KeybindStore.Shared;
        shopAction = actions != null ? actions.asset.FindAction("Player/Shop", false) : null;
        if (shopAction != null)
            shopAction.performed += OnShopPerformed;
        bound = true;
    }

    void UnbindInput()
    {
        if (shopAction != null)
            shopAction.performed -= OnShopPerformed;
        shopAction = null;
        bound = false;
    }

    void Update()
    {
        if (shopAction != null)
            return;
        if (KeybindStore.BlocksGameplayInput)
            return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
            ToggleShop();
    }

    void OnShopPerformed(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        ToggleShop();
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

        Image chip = UiTheme.AddImage(canvasGo.transform, "Chip", new Vector2(280f, 78f), UiTheme.Chip);
        RectTransform chipRt = chip.rectTransform;
        chipRt.anchorMin = new Vector2(0f, 1f);
        chipRt.anchorMax = new Vector2(0f, 1f);
        chipRt.pivot = new Vector2(0f, 1f);
        chipRt.anchoredPosition = new Vector2(24f, -18f);

        Image coinIcon = AddSprite(chip.transform, "CoinIcon", GameHudIcons.Coin, new Vector2(28f, 28f));
        Place(coinIcon.rectTransform, 14f, -12f, 28f, 28f);
        coinsText = UiTheme.AddText(chip.transform, "Coins", "0", 22f, UiTheme.Warn);
        coinsText.fontStyle = FontStyles.Bold;
        RectTransform coinsRt = coinsText.rectTransform;
        coinsRt.anchorMin = new Vector2(0f, 0.5f);
        coinsRt.anchorMax = new Vector2(0f, 1f);
        coinsRt.pivot = new Vector2(0f, 0.5f);
        coinsRt.anchoredPosition = new Vector2(48f, 0f);
        coinsRt.sizeDelta = new Vector2(200f, 0f);

        Image rubyIcon = AddSprite(chip.transform, "RubyIcon", GameHudIcons.Ruby, new Vector2(26f, 26f));
        Place(rubyIcon.rectTransform, 14f, -44f, 26f, 26f);
        rubiesText = UiTheme.AddText(chip.transform, "Rubies", "0", 20f, UiTheme.Accent);
        RectTransform rubiesRt = rubiesText.rectTransform;
        rubiesRt.anchorMin = new Vector2(0f, 0f);
        rubiesRt.anchorMax = new Vector2(0f, 0.5f);
        rubiesRt.pivot = new Vector2(0f, 0.5f);
        rubiesRt.anchoredPosition = new Vector2(48f, 0f);
        rubiesRt.sizeDelta = new Vector2(220f, 0f);

        shopRoot = new GameObject("Shop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        shopRoot.transform.SetParent(canvasGo.transform, false);
        UiTheme.StylePanel(shopRoot, new Vector2(460f, 300f));
        RectTransform shopRt = shopRoot.GetComponent<RectTransform>();
        shopRt.anchorMin = new Vector2(0f, 1f);
        shopRt.anchorMax = new Vector2(0f, 1f);
        shopRt.pivot = new Vector2(0f, 1f);
        shopRt.anchoredPosition = new Vector2(24f, -110f);
        shopRoot.SetActive(false);

        TextMeshProUGUI title = UiTheme.AddText(shopRoot.transform, "Title", "Магазин  ·  H", 24f, UiTheme.Accent);
        title.fontStyle = FontStyles.Bold;
        Stretch(title.rectTransform, 0.08f, 0.92f, 0.8f, 0.94f);

        shopInfo = UiTheme.AddText(shopRoot.transform, "Info", "", 18f, UiTheme.Text);
        shopInfo.enableWordWrapping = true;
        Stretch(shopInfo.rectTransform, 0.08f, 0.92f, 0.54f, 0.78f);

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

    public void ToggleShop()
    {
        SetShopOpen(shopRoot == null || !shopRoot.activeSelf);
    }

    public void SetShopOpen(bool open)
    {
        if (shopRoot != null)
            shopRoot.SetActive(open);
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
            coinsText.text = coins.ToString();
        if (rubiesText != null)
            rubiesText.text = rubies.ToString();
        if (shopInfo != null)
            shopInfo.text = "1 рубин = " + Economy.CoinsPerRuby + " монет\nСейчас: " + rubies + " руб.  →  +" + (rubies * Economy.CoinsPerRuby) + " монет";
    }

    static Image AddSprite(Transform parent, string name, Sprite sprite, Vector2 size)
    {
        Image image = UiTheme.AddImage(parent, name, size, Color.white);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.sprite = sprite;
        image.enabled = sprite != null;
        return image;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
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
