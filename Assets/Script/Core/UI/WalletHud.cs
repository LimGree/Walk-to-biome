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
    TextMeshProUGUI shopCoins;
    TextMeshProUGUI shopRubies;
    TextMeshProUGUI offer1;
    TextMeshProUGUI offer5;
    TextMeshProUGUI offerAll;
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
        GameObject canvasGo = OverlayUi.CreateCanvas(transform, "WalletHud", 80);

        Image chip = UiTheme.AddImage(canvasGo.transform, "Chip", new Vector2(250f, 86f), UiTheme.Chip);
        RectTransform chipRt = chip.rectTransform;
        chipRt.anchorMin = new Vector2(0f, 1f);
        chipRt.anchorMax = new Vector2(0f, 1f);
        chipRt.pivot = new Vector2(0f, 1f);
        chipRt.anchoredPosition = new Vector2(24f, -18f);

        OverlayUi.CreateSprite(chip.transform, "CoinIcon", GameHudIcons.Coin, new Vector2(32f, 32f));
        Place(chip.transform.Find("CoinIcon") as RectTransform, 16f, -12f, 32f, 32f);
        coinsText = UiTheme.AddText(chip.transform, "Coins", "0", 24f, UiTheme.Warn);
        coinsText.fontStyle = FontStyles.Bold;
        Place(coinsText.rectTransform, 56f, -10f, 180f, 36f);

        OverlayUi.CreateSprite(chip.transform, "RubyIcon", GameHudIcons.Ruby, new Vector2(30f, 30f));
        Place(chip.transform.Find("RubyIcon") as RectTransform, 16f, -48f, 30f, 30f);
        rubiesText = UiTheme.AddText(chip.transform, "Rubies", "0", 22f, UiTheme.Accent);
        Place(rubiesText.rectTransform, 56f, -46f, 180f, 34f);

        shopRoot = new GameObject("ShopOverlay", typeof(RectTransform));
        shopRoot.transform.SetParent(canvasGo.transform, false);
        StretchFull(shopRoot.GetComponent<RectTransform>());
        shopRoot.SetActive(false);

        OverlayUi.CreateDim(shopRoot.transform);
        GameObject panel = OverlayUi.CreatePanel(shopRoot.transform);
        OverlayUi.CreateHeader(panel.transform, GameHudIcons.Ruby, "Магазин", () => SetShopOpen(false));
        Transform body = OverlayUi.CreateBody(panel.transform);
        VerticalLayoutGroup bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyLayout.spacing = 14f;
        bodyLayout.childAlignment = TextAnchor.UpperCenter;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;

        GameObject balances = Card(body, "Balances", 150f);
        shopCoins = BalanceBlock(balances.transform, "Coins", GameHudIcons.Coin, 0.08f, 0.48f);
        shopRubies = BalanceBlock(balances.transform, "Rubies", GameHudIcons.Ruby, 0.52f, 0.92f);

        GameObject rate = Card(body, "Rate", 110f);
        RateRow(rate.transform);

        offer1 = OfferCard(body, 1, () => Exchange(1));
        offer5 = OfferCard(body, 5, () => Exchange(5));
        offerAll = OfferCard(body, 0, () =>
        {
            if (PlayerWallet.Instance != null)
                Exchange(PlayerWallet.Instance.Rubies);
        });
    }

    public void ToggleShop()
    {
        SetShopOpen(!IsShopOpen);
    }

    public void SetShopOpen(bool open)
    {
        if (shopRoot != null)
            shopRoot.SetActive(open);
        if (open && SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            SelectionActionsUI.Instance.SetOpen(false);
        Refresh();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
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
        if (shopCoins != null)
            shopCoins.text = coins + " монет";
        if (shopRubies != null)
            shopRubies.text = rubies + " рубинов";
        if (offer1 != null)
            offer1.text = "1 рубин  →  " + Economy.CoinsPerRuby + " монет";
        if (offer5 != null)
            offer5.text = "5 рубинов  →  " + (5 * Economy.CoinsPerRuby) + " монет";
        if (offerAll != null)
            offerAll.text = rubies <= 0
                ? "Нет рубинов"
                : "Все " + rubies + " руб.  →  " + (rubies * Economy.CoinsPerRuby) + " монет";
    }

    static GameObject Card(Transform parent, string name, float height)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        card.transform.SetParent(parent, false);
        UiTheme.StyleImage(card.GetComponent<Image>(), UiTheme.Card);
        OverlayUi.LayoutHeight(card.GetComponent<LayoutElement>(), height);
        return card;
    }

    static TextMeshProUGUI BalanceBlock(Transform parent, string name, Sprite icon, float x0, float x1)
    {
        OverlayUi.CreateSprite(parent, name + "Icon", icon, new Vector2(64f, 64f));
        RectTransform iconRt = parent.Find(name + "Icon") as RectTransform;
        iconRt.anchorMin = new Vector2(x0, 0.5f);
        iconRt.anchorMax = new Vector2(x0, 0.5f);
        iconRt.anchoredPosition = new Vector2(40f, 0f);
        iconRt.sizeDelta = new Vector2(64f, 64f);

        TextMeshProUGUI text = UiTheme.AddText(parent, name + "Text", "0", 28f, UiTheme.Text);
        text.fontStyle = FontStyles.Bold;
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = new Vector2(x0, 0f);
        textRt.anchorMax = new Vector2(x1, 1f);
        textRt.offsetMin = new Vector2(90f, 16f);
        textRt.offsetMax = new Vector2(0f, -16f);
        return text;
    }

    static void RateRow(Transform parent)
    {
        OverlayUi.CreateSprite(parent, "Ruby", GameHudIcons.Ruby, new Vector2(56f, 56f));
        PlaceCenter(parent.Find("Ruby") as RectTransform, -160f, 56f);
        TextMeshProUGUI one = UiTheme.AddText(parent, "One", "1", 28f, UiTheme.Text);
        one.fontStyle = FontStyles.Bold;
        one.alignment = TextAlignmentOptions.Center;
        PlaceCenter(one.rectTransform, -96f, 48f);
        TextMeshProUGUI arrow = UiTheme.AddText(parent, "Arrow", "→", 36f, UiTheme.Accent);
        arrow.alignment = TextAlignmentOptions.Center;
        PlaceCenter(arrow.rectTransform, 0f, 60f);
        OverlayUi.CreateSprite(parent, "Coin", GameHudIcons.Coin, new Vector2(56f, 56f));
        PlaceCenter(parent.Find("Coin") as RectTransform, 96f, 56f);
        TextMeshProUGUI fifty = UiTheme.AddText(parent, "Fifty", Economy.CoinsPerRuby.ToString(), 28f, UiTheme.Warn);
        fifty.fontStyle = FontStyles.Bold;
        fifty.alignment = TextAlignmentOptions.Center;
        PlaceCenter(fifty.rectTransform, 168f, 48f);
    }

    static TextMeshProUGUI OfferCard(Transform parent, int rubies, System.Action onClick)
    {
        GameObject card = Card(parent, "Offer" + rubies, 108f);
        Button button = card.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();
        button.onClick.AddListener(() => onClick?.Invoke());

        OverlayUi.CreateSprite(card.transform, "Ruby", GameHudIcons.Ruby, new Vector2(48f, 48f));
        RectTransform rubyRt = card.transform.Find("Ruby") as RectTransform;
        rubyRt.anchorMin = new Vector2(0f, 0.5f);
        rubyRt.anchorMax = new Vector2(0f, 0.5f);
        rubyRt.anchoredPosition = new Vector2(48f, 0f);
        rubyRt.sizeDelta = new Vector2(48f, 48f);

        OverlayUi.CreateSprite(card.transform, "Coin", GameHudIcons.Coin, new Vector2(48f, 48f));
        RectTransform coinRt = card.transform.Find("Coin") as RectTransform;
        coinRt.anchorMin = new Vector2(1f, 0.5f);
        coinRt.anchorMax = new Vector2(1f, 0.5f);
        coinRt.anchoredPosition = new Vector2(-48f, 0f);
        coinRt.sizeDelta = new Vector2(48f, 48f);

        TextMeshProUGUI text = UiTheme.AddText(card.transform, "Label", "", 24f, UiTheme.Accent);
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(90f, 8f);
        textRt.offsetMax = new Vector2(-90f, -8f);
        return text;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        if (rt == null)
            return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void PlaceCenter(RectTransform rt, float x, float size)
    {
        if (rt == null)
            return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(size, size);
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
