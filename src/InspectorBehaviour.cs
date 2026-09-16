using BepInEx;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TSKAtkInspector;

public sealed class InspectorBehaviour : MonoBehaviour
{
    private static InspectorBehaviour? _instance;
    private static TSKBattleLeaderParameter? _observed;
    private TSKBattleLeaderParameter? _leader;
    private TSKBattleNote? _selected;
    private GameObject? _canvasObject;
    private GameObject? _hitObject;
    private GameObject? _criticalHudObject;
    private GameObject? _exGaugeHudObject;
    private IntPtr _hitText;
    private UnityAction? _hitAction;
    private Font? _font;
    private Text? _criticalHudText;
    private Text? _exGaugeHudText;
    private IntPtr _exGaugeValueText;
    private Text? _title, _summary, _details, _modeLabel, _hint, _enemyTargetTitle;
    private Image? _attackTabImage, _criticalTabImage, _enemyTabImage;
    private RectTransform? _content;
    private ScrollRect? _scroll;
    private bool _open, _allEffects;
    private InspectorSection _section = InspectorSection.Attack;
    private int _enemyIndex;
    private float _refreshAt, _hudRefreshAt, _retryAt;
    private BattleSnapshot? _snapshot;
    private string _lastError = "";
    // Keep managed actions alive for native UnityEvent delegates.
    private readonly List<UnityAction> _actions = new();
    private readonly List<Button> _enemyTargetButtons = new();
    private readonly List<Image> _enemyTargetImages = new();
    private readonly EnemyDamageHud _enemyDamageHud = new();

    public InspectorBehaviour(IntPtr pointer) : base(pointer) { _instance = this; }

    [HideFromIl2Cpp]
    internal static void Observe(TSKBattleLeaderParameter leader) => _observed = leader;

    [HideFromIl2Cpp]
    internal static void ResetBattle()
    {
        _observed = null;
        if (_instance == null) return;
        _instance._leader = null;
        _instance._selected = null;
        _instance._section = InspectorSection.Attack;
        _instance._enemyIndex = 0;
        _instance.Close();
        if (_instance._hitObject != null) Object.Destroy(_instance._hitObject);
        if (_instance._criticalHudObject != null) Object.Destroy(_instance._criticalHudObject);
        if (_instance._exGaugeHudObject != null) Object.Destroy(_instance._exGaugeHudObject);
        _instance._hitObject = null;
        _instance._criticalHudObject = null;
        _instance._exGaugeHudObject = null;
        _instance._criticalHudText = null;
        _instance._exGaugeHudText = null;
        _instance._exGaugeValueText = IntPtr.Zero;
        _instance._hitText = IntPtr.Zero;
        _instance._hitAction = null;
        _instance._hudRefreshAt = 0;
        _instance._enemyDamageHud.Clear();
    }

    public void Update()
    {
        try
        {
            if (Time.unscaledTime < _retryAt) return;
            if (_observed != null)
            {
                _leader = _observed;
                EnsureHitTarget();
                EnsureExGaugeHud();
            }
            if (_leader != null && _leader.noteData != null && Time.unscaledTime >= _hudRefreshAt)
            {
                if (LeaderVisible()) RefreshInlineCriticalDamage();
                RefreshInlineExGauge();
                _enemyDamageHud.Refresh(_leader.noteData);
                _hudRefreshAt = Time.unscaledTime + .25f;
            }
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (_open) Close();
                else if (LeaderVisible()) Open();
            }
            if (!_open) return;
            if (_selected == null || _selected.UnitData == null || !_selected.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }
            if (Time.unscaledTime >= _refreshAt)
            {
                Refresh();
                _refreshAt = Time.unscaledTime + .25f;
            }
        }
        catch (Exception e)
        {
            _retryAt = Time.unscaledTime + 2f;
            if (_details != null && _open) _details.text = "讀取戰鬥資料失敗，稍後會重試。\n" + Presentation.Safe(e.Message);
            LogError(e);
        }
    }

    [HideFromIl2Cpp]
    private bool LeaderVisible() => _leader != null && _leader.gameObject.activeInHierarchy &&
        _leader.attackText != null && _leader.attackText.gameObject.activeInHierarchy && _leader.noteData != null;

    [HideFromIl2Cpp]
    private void EnsureHitTarget()
    {
        if (!LeaderVisible()) return;
        var text = _leader!.attackText;
        if (_hitObject != null && _criticalHudObject != null && _hitText == text.Pointer) return;
        if (_hitObject != null) Object.Destroy(_hitObject);
        if (_criticalHudObject != null) Object.Destroy(_criticalHudObject);
        var rt = Rect("ATK Inspector Click", text.rectTransform);
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(1, .5f);
        // Follows the real ATK text transform at any resolution. Include the label to its left.
        rt.sizeDelta = new Vector2(Mathf.Max(text.rectTransform.rect.width, text.rectTransform.rect.height * 4.3f), 8);
        rt.anchoredPosition = Vector2.zero;
        var background = rt.gameObject.AddComponent<Image>();
        background.color = Color.clear;
        background.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        // This listener is bound to this particular UI instance, not a later selected leader.
        var owner = _leader;
        _hitAction = (UnityAction)(() =>
        {
            try { if (owner != null) { _leader = owner; Open(); } }
            catch (Exception e) { LogError(e); }
        });
        button.onClick.AddListener(_hitAction);
        _hitObject = rt.gameObject;
        _hitText = text.Pointer;
        BuildInlineCriticalDamage(text);
        RefreshInlineCriticalDamage();
        Plugin.Logger.LogInfo("ATK click target attached to " + text.gameObject.name);
    }

    [HideFromIl2Cpp]
    private void BuildInlineCriticalDamage(Text attackText)
    {
        var rt = Rect("ATK Inspector Critical", attackText.rectTransform);
        rt.anchorMin = rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(0, 4);
        rt.sizeDelta = new Vector2(
            Mathf.Max(attackText.rectTransform.rect.width * 3.2f, attackText.rectTransform.rect.height * 7f),
            Mathf.Max(24, attackText.rectTransform.rect.height * .7f));

        var label = rt.gameObject.AddComponent<Text>();
        label.font = attackText.font;
        label.fontSize = Mathf.Max(14, Mathf.RoundToInt(attackText.fontSize * .48f));
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(1f, .84f, .36f, 1f);
        label.alignment = TextAnchor.MiddleRight;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.text = "Cri 0% CriDmg 150%";

        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.015f, .02f, .05f, .95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        _criticalHudObject = rt.gameObject;
        _criticalHudText = label;
    }

    [HideFromIl2Cpp]
    private void RefreshInlineCriticalDamage()
    {
        if (_criticalHudText == null || !LeaderVisible()) return;
        var note = _leader!.noteData;
        _criticalHudText.text = Presentation.InlineCriticalSummary(
            note.GetCritical(), BattleReader.CurrentCriticalDamage(note));
    }

    [HideFromIl2Cpp]
    private void EnsureExGaugeHud()
    {
        var note = _leader?.noteData;
        var exValueText = note?.Team?.teamView?.compEx?.exValueText;
        if (exValueText == null) return;
        if (_exGaugeHudObject != null && _exGaugeValueText == exValueText.Pointer) return;
        if (_exGaugeHudObject != null) Object.Destroy(_exGaugeHudObject);

        var rt = Rect("ATK Inspector EX Gain", exValueText.rectTransform);
        // Keep the panel's right edge on the native EX number's right edge.
        // The fixed-width panel therefore grows leftward without covering PLAYER.
        rt.anchorMin = rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(0, 6);
        rt.sizeDelta = new Vector2(560, 36);
        var background = rt.gameObject.AddComponent<Image>();
        background.color = new Color(.015f, .02f, .035f, .9f);
        background.raycastTarget = false;

        var labelRt = Rect("Label", rt);
        Fill(labelRt);
        labelRt.offsetMin = new Vector2(10, 2);
        labelRt.offsetMax = new Vector2(0, -2);
        var label = labelRt.gameObject.AddComponent<Text>();
        label.font = exValueText.font;
        label.fontSize = Mathf.Max(15, Mathf.RoundToInt(exValueText.fontSize * .62f));
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleRight;
        label.supportRichText = true;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.text = "EX上昇 0 - 通常 27 - Charge 34";

        var outline = labelRt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, .95f);
        outline.effectDistance = new Vector2(1.25f, -1.25f);
        outline.useGraphicAlpha = true;

        _exGaugeHudObject = rt.gameObject;
        _exGaugeHudText = label;
        _exGaugeValueText = exValueText.Pointer;
        RefreshInlineExGauge();
    }

    [HideFromIl2Cpp]
    private void RefreshInlineExGauge()
    {
        if (_exGaugeHudText == null || _leader?.noteData == null) return;
        var status = BattleReader.CurrentExGauge(_leader.noteData);
        _exGaugeHudText.text = Presentation.InlineExGaugeSummary(status.BaseRate,
            status.BattleRate, status.NormalGain, status.ChargeGain,
            status.HasBattleRateEffect, status.IsCharge);
    }

    [HideFromIl2Cpp]
    private void Open()
    {
        if (!LeaderVisible()) return;
        _selected = _leader!.noteData; // Pin the inspected actor if auto battle advances.
        try { BuildWindow(); }
        catch
        {
            if (_canvasObject != null) Object.Destroy(_canvasObject);
            _canvasObject = null;
            _actions.Clear();
            throw;
        }
        UpdateSectionButtons();
        UpdateModeLabel();
        _open = true;
        _canvasObject!.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Refresh();
        _scroll!.verticalNormalizedPosition = 1;
        _refreshAt = Time.unscaledTime + .25f;
        Plugin.Logger.LogInfo("Battle buff detail opened: " + Presentation.Safe(_snapshot?.Summary));
    }

    [HideFromIl2Cpp]
    private void Close()
    {
        _open = false;
        _selected = null;
        if (_canvasObject != null) _canvasObject.SetActive(false);
    }

    [HideFromIl2Cpp]
    private void Refresh()
    {
        if (_selected == null) return;
        if (_section == InspectorSection.EnemyDebuff)
        {
            int count = BattleReader.EnemyCount(_selected);
            if (count == 0) _enemyIndex = 0;
            else if (_enemyIndex >= count) _enemyIndex = count - 1;
            UpdateEnemyTargetButtons(count);
        }
        _snapshot = BattleReader.Read(_selected, _section, _allEffects, _enemyIndex);
        _title!.text = _snapshot.Title;
        _summary!.text = _snapshot.Summary;
        if (_details!.text == _snapshot.Details) return;
        _details.text = _snapshot.Details;
        _content!.sizeDelta = new Vector2(0, Mathf.Max(_scroll!.viewport.rect.height, _details.preferredHeight + 36));
    }

    [HideFromIl2Cpp]
    private void BuildWindow()
    {
        if (_canvasObject != null) return;
        _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Yu Gothic", "Arial" }, 24);
        var root = Rect("ATK Inspector Canvas", null);
        _canvasObject = root.gameObject;
        Object.DontDestroyOnLoad(_canvasObject);
        var canvas = _canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = _canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        _canvasObject.AddComponent<GraphicRaycaster>();

        var shade = Rect("Backdrop", root);
        Fill(shade);
        var shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.color = new Color(0, .01f, .04f, .78f);
        var dismiss = shade.gameObject.AddComponent<Button>();
        dismiss.targetGraphic = shadeImage;
        dismiss.transition = Selectable.Transition.None;
        Bind(dismiss, Close);

        var panel = Rect("Panel", root);
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
        panel.sizeDelta = new Vector2(1400, 930);
        panel.gameObject.AddComponent<Image>().color = new Color(.055f, .072f, .13f, .995f);

        _title = Label("Title", panel, "ATK 加成明細", 34, new Color(.7f, .96f, .92f));
        Place(_title.rectTransform, 36, 26, 1070, 54);
        ButtonAt("Close", panel, "關閉  ×", 1190, 25, 174, Close);

        _summary = Label("Summary", panel, "", 28, Color.white);
        Place(_summary.rectTransform, 36, 93, 1328, 60);
        _hint = Label("Hint", panel, "即時讀取目前效果 · 下方切換分類 · 滾輪捲動 · F9 開關", 20, new Color(.63f, .7f, .8f));
        Place(_hint.rectTransform, 36, 158, 1328, 34);
        _enemyTargetTitle = Label("Enemy Target Hint", panel, "敵方目標：", 20, new Color(.72f, .78f, .88f));
        Place(_enemyTargetTitle.rectTransform, 36, 160, 124, 40);
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            var button = ButtonAt("Enemy Target " + (i + 1), panel, "敵 " + (i + 1), 170 + i * 118, 155, 106, () => SelectEnemyTarget(index));
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(106, 46);
            _enemyTargetButtons.Add(button);
            _enemyTargetImages.Add(button.GetComponent<Image>());
        }

        var view = Rect("Scroll View", panel);
        Place(view, 28, 206, 1344, 616);
        view.gameObject.AddComponent<Image>().color = new Color(.08f, .105f, .175f, 1);
        view.gameObject.AddComponent<RectMask2D>();
        _scroll = view.gameObject.AddComponent<ScrollRect>();
        _scroll.viewport = view;
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 55;
        _scroll.inertia = false;
        _content = Rect("Content", view);
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(.5f, 1);
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = new Vector2(0, 616);
        _scroll.content = _content;
        _details = Label("Effects", _content, "", 23, new Color(.87f, .9f, .96f));
        Fill(_details.rectTransform);
        _details.rectTransform.offsetMin = new Vector2(20, 16);
        _details.rectTransform.offsetMax = new Vector2(-20, -16);
        _details.lineSpacing = 1.05f;
        _details.horizontalOverflow = HorizontalWrapMode.Wrap;
        _details.verticalOverflow = VerticalWrapMode.Overflow;

        var attackTab = ButtonAt("Attack Detail", panel, "ATK 明細", 36, 847, 155, () => SelectSection(InspectorSection.Attack));
        _attackTabImage = attackTab.GetComponent<Image>();
        var criticalTab = ButtonAt("Critical Damage Detail", panel, "爆擊傷害明細", 204, 847, 220, () => SelectSection(InspectorSection.CriticalDamage));
        _criticalTabImage = criticalTab.GetComponent<Image>();
        var enemyTab = ButtonAt("Enemy Debuff Detail", panel, "敵方 DEBUFF", 437, 847, 220, () => SelectSection(InspectorSection.EnemyDebuff));
        _enemyTabImage = enemyTab.GetComponent<Image>();
        var mode = ButtonAt("All Effects", panel, "顯示全部效果／原始參數", 670, 847, 300, () =>
        {
            _allEffects = !_allEffects;
            UpdateModeLabel();
            Refresh();
            _scroll.verticalNormalizedPosition = 1;
        });
        _modeLabel = mode.GetComponentInChildren<Text>();
        ButtonAt("Save", panel, "儲存明細", 983, 847, 170, () =>
        {
            if (_snapshot == null) return;
            string path = Path.Combine(Paths.BepInExRootPath, "AtkInspector-last.txt");
            File.WriteAllText(path, _snapshot.Title + "\n" + Presentation.Safe(_snapshot.Summary) + "\n\n" + Presentation.Safe(_snapshot.Details));
            Plugin.Logger.LogInfo("Battle buff detail saved: " + path);
        });
        var footer = Label("Footer", panel, "關閉後可換角色。", 20, new Color(.63f, .7f, .8f));
        Place(footer.rectTransform, 1168, 859, 196, 42);
        UpdateSectionButtons();
    }

    [HideFromIl2Cpp]
    private void SelectSection(InspectorSection section)
    {
        if (_section == section) return;
        _section = section;
        UpdateSectionButtons();
        UpdateModeLabel();
        Refresh();
        _scroll!.verticalNormalizedPosition = 1;
    }

    [HideFromIl2Cpp]
    private void SelectEnemyTarget(int index)
    {
        if (_selected == null || index < 0 || index >= BattleReader.EnemyCount(_selected)) return;
        _enemyIndex = index;
        UpdateEnemyTargetButtons(BattleReader.EnemyCount(_selected));
        Refresh();
        _scroll!.verticalNormalizedPosition = 1;
    }

    [HideFromIl2Cpp]
    private void UpdateSectionButtons()
    {
        var selected = new Color(.11f, .43f, .43f, 1);
        var normal = new Color(.13f, .22f, .32f, 1);
        if (_attackTabImage != null) _attackTabImage.color = _section == InspectorSection.Attack ? selected : normal;
        if (_criticalTabImage != null) _criticalTabImage.color = _section == InspectorSection.CriticalDamage ? selected : normal;
        if (_enemyTabImage != null) _enemyTabImage.color = _section == InspectorSection.EnemyDebuff ? selected : normal;
        bool enemy = _section == InspectorSection.EnemyDebuff;
        if (_hint != null) _hint.gameObject.SetActive(!enemy);
        if (_enemyTargetTitle != null) _enemyTargetTitle.gameObject.SetActive(enemy);
        foreach (var button in _enemyTargetButtons) button.gameObject.SetActive(enemy);
        if (enemy) UpdateEnemyTargetButtons(_selected == null ? 0 : BattleReader.EnemyCount(_selected));
    }

    [HideFromIl2Cpp]
    private void UpdateEnemyTargetButtons(int count)
    {
        var selected = new Color(.11f, .43f, .43f, 1);
        var normal = new Color(.13f, .22f, .32f, 1);
        var unavailable = new Color(.08f, .1f, .14f, 1);
        for (int i = 0; i < _enemyTargetButtons.Count; i++)
        {
            bool available = i < count;
            _enemyTargetButtons[i].interactable = available;
            _enemyTargetImages[i].color = !available ? unavailable : i == _enemyIndex ? selected : normal;
        }
    }

    [HideFromIl2Cpp]
    private void UpdateModeLabel()
    {
        if (_modeLabel == null) return;
        string related = _section switch
        {
            InspectorSection.Attack => "ATK",
            InspectorSection.CriticalDamage => "爆擊傷害",
            _ => "敵方傷害倍率"
        };
        _modeLabel.text = _allEffects ? $"僅顯示 {related} 相關效果" : "顯示全部效果／原始參數";
    }

    [HideFromIl2Cpp]
    private Text Label(string name, Transform parent, string value, int size, Color color)
    {
        var rt = Rect(name, parent);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.supportRichText = true;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    [HideFromIl2Cpp]
    private Button ButtonAt(string name, Transform parent, string caption, float x, float y, float width, Action action)
    {
        var rt = Rect(name, parent);
        Place(rt, x, y, width, 54);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = new Color(.13f, .22f, .32f, 1);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Bind(button, action);
        var label = Label("Label", rt, caption, 22, Color.white);
        Fill(label.rectTransform);
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    [HideFromIl2Cpp]
    private void Bind(Button button, Action action)
    {
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        UnityAction handler = (UnityAction)(() => { try { action(); } catch (Exception e) { LogError(e); } });
        _actions.Add(handler);
        button.onClick.AddListener(handler);
    }

    [HideFromIl2Cpp]
    private static RectTransform Rect(string name, Transform? parent)
    {
        var go = new GameObject(name, new[] { Il2CppType.Of<RectTransform>() });
        var rt = go.GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        return rt;
    }

    [HideFromIl2Cpp]
    private static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    [HideFromIl2Cpp]
    private static void Place(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    [HideFromIl2Cpp]
    private void LogError(Exception e)
    {
        if (e.ToString() == _lastError) return;
        _lastError = e.ToString();
        Plugin.Logger.LogError(e);
    }

    public void OnDestroy()
    {
        if (_canvasObject != null) Object.Destroy(_canvasObject);
        if (_hitObject != null) Object.Destroy(_hitObject);
        if (_criticalHudObject != null) Object.Destroy(_criticalHudObject);
        if (_exGaugeHudObject != null) Object.Destroy(_exGaugeHudObject);
        _enemyDamageHud.Clear();
        if (_font != null) Object.Destroy(_font);
        _instance = null;
        _actions.Clear();
    }
}
