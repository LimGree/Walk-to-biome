using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class WalletHud : MonoBehaviour
{
    public static WalletHud Instance { get; private set; }

    VisualElement shop;
    Label coinsText;
    Label rubiesText;
    Label shopCoins;
    Label shopRubies;
    Label offer1;
    Label offer5;
    Label offerAll;
    InputAction shopAction;

    bool shopOpen;
    public bool IsShopOpen => shopOpen;

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
        if (shopAction != null)
            shopAction.performed -= OnShopPerformed;
        shopAction = null;
    }

    void OnDestroy()
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged -= Refresh;
        if (Instance == this)
            Instance = null;
    }

    void BindInput()
    {
        if (shopAction != null)
            return;
        InputSystem_Actions actions = KeybindStore.Shared;
        shopAction = actions != null ? actions.asset.FindAction("Player/Shop", false) : null;
        if (shopAction != null)
            shopAction.performed += OnShopPerformed;
    }

    void Update()
    {
        if (shopAction != null)
            return;
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
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
        VisualElement root = IndustryUi.Mount(this, 80);
        var chip = IndustryUi.El("Chip", "hud-chip", "col");
        var coinLine = IndustryUi.El("Coins", "hud-line");
        coinLine.Add(IndustryUi.Icon(GameHudIcons.Coin, "icon-24"));
        coinsText = IndustryUi.Text("C", "0", "gold");
        coinLine.Add(coinsText);
        var rubyLine = IndustryUi.El("Rubies", "hud-line");
        rubyLine.Add(IndustryUi.Icon(GameHudIcons.Ruby, "icon-24"));
        rubiesText = IndustryUi.Text("R", "0", "ruby");
        rubyLine.Add(rubiesText);
        chip.Add(coinLine);
        chip.Add(rubyLine);
        root.Add(chip);

        shop = IndustryUi.OverlayPanel("Магазин", GameHudIcons.Ruby, () => SetShopOpen(false));
        IndustryUi.Show(shop, false);
        VisualElement panel = IndustryUi.PanelOf(shop);
        var balances = IndustryUi.El("Bal", "card");
        balances.Add(IndustryUi.Icon(GameHudIcons.Coin, "icon-48"));
        shopCoins = IndustryUi.Text("SC", "0 монет", "body-text", "grow");
        balances.Add(shopCoins);
        balances.Add(IndustryUi.Icon(GameHudIcons.Ruby, "icon-48"));
        shopRubies = IndustryUi.Text("SR", "0 рубинов", "body-text");
        balances.Add(shopRubies);
        panel.Add(balances);

        var rate = IndustryUi.El("Rate", "card");
        rate.Add(IndustryUi.Icon(GameHudIcons.Ruby, "icon-32"));
        rate.Add(IndustryUi.Text("One", "1", "gold"));
        rate.Add(IndustryUi.Text("Arr", "  →  ", "title"));
        rate.Add(IndustryUi.Icon(GameHudIcons.Coin, "icon-32"));
        rate.Add(IndustryUi.Text("Val", Economy.CoinsPerRuby.ToString(), "gold"));
        panel.Add(rate);

        offer1 = AddOffer(panel, () => Exchange(1));
        offer5 = AddOffer(panel, () => Exchange(5));
        offerAll = AddOffer(panel, () =>
        {
            if (PlayerWallet.Instance != null)
                Exchange(PlayerWallet.Instance.Rubies);
        });
        root.Add(shop);
    }

    static Label AddOffer(VisualElement panel, System.Action onClick)
    {
        var card = IndustryUi.El("Offer", "card");
        card.Add(IndustryUi.Icon(GameHudIcons.Ruby, "icon-32"));
        var label = IndustryUi.Text("L", "", "body-text", "grow");
        card.Add(label);
        card.Add(IndustryUi.Icon(GameHudIcons.Coin, "icon-32"));
        card.Add(IndustryUi.Btn("Обменять", onClick, "btn-small", "btn-primary"));
        panel.Add(card);
        return label;
    }

    public void ToggleShop()
    {
        SetShopOpen(!IsShopOpen);
    }

    public void SetShopOpen(bool open)
    {
        shopOpen = open;
        IndustryUi.Show(shop, open);
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
        if (coinsText != null) coinsText.text = coins.ToString();
        if (rubiesText != null) rubiesText.text = rubies.ToString();
        if (shopCoins != null) shopCoins.text = coins + " монет";
        if (shopRubies != null) shopRubies.text = rubies + " рубинов";
        if (offer1 != null) offer1.text = "1 рубин  →  " + Economy.CoinsPerRuby + " монет";
        if (offer5 != null) offer5.text = "5 рубинов  →  " + (5 * Economy.CoinsPerRuby) + " монет";
        if (offerAll != null)
            offerAll.text = rubies <= 0 ? "Нет рубинов" : "Все " + rubies + "  →  " + (rubies * Economy.CoinsPerRuby) + " монет";
    }
}
