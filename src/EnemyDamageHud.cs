using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TSKAtkInspector;

internal sealed class EnemyDamageHud
{
    private readonly Dictionary<IntPtr, Badge> _badges = new();

    internal void Refresh(TSKBattleNote attacker)
    {
        var enemies = attacker.EnemyTeam?.NoteList;
        if (enemies == null) { Clear(); return; }

        var present = new HashSet<IntPtr>();
        int count = Math.Min(5, enemies.Count);
        for (int i = 0; i < count; i++)
        {
            var target = enemies[i];
            if (target == null || target.UnitData == null || target.noteView == null) continue;
            present.Add(target.Pointer);

            var view = target.noteView;
            if (!_badges.TryGetValue(target.Pointer, out var badge) || badge.ViewPointer != view.Pointer)
            {
                if (badge != null) Object.Destroy(badge.Root);
                badge = Build(target, view);
                if (badge == null) continue;
                _badges[target.Pointer] = badge;
            }

            bool visible = !target.isDefeat && target.gameObject.activeInHierarchy && view.gameObject.activeInHierarchy;
            badge.Root.SetActive(visible);
            if (!visible) continue;

            var rates = BattleReader.CurrentEnemyDamageRates(attacker, target);
            badge.Text.text = Presentation.EnemyDamageBadge(rates.ERate, rates.FRate);
            Place(badge.Rect, view.rectTransform,
                TimelineCtMath.EnemyDamageBadgeBelow(target.GetNoteCount));
            badge.Rect.SetAsLastSibling();
        }

        foreach (var pointer in _badges.Keys.Where(pointer => !present.Contains(pointer)).ToArray())
        {
            Object.Destroy(_badges[pointer].Root);
            _badges.Remove(pointer);
        }
    }

    internal void Clear()
    {
        foreach (var badge in _badges.Values) if (badge.Root != null) Object.Destroy(badge.Root);
        _badges.Clear();
    }

    private static Badge? Build(TSKBattleNote target, TSKBattleNoteView view)
    {
        var sourceText = view.outCount;
        var anchor = view.rectTransform;
        if (sourceText == null || sourceText.font == null || anchor == null) return null;

        var go = new GameObject("ATK Inspector Enemy E F " + target.Pointer,
            new[] { Il2CppType.Of<RectTransform>() });
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(anchor, false);
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(Mathf.Max(82, anchor.rect.width * .95f), 52);

        var background = go.AddComponent<Image>();
        background.color = new Color(.015f, .02f, .045f, .82f);
        background.raycastTarget = false;

        var textObject = new GameObject("Values", new[] { Il2CppType.Of<RectTransform>() });
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rt, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObject.AddComponent<Text>();
        text.font = sourceText.font;
        text.fontSize = Mathf.Max(16, Mathf.RoundToInt(sourceText.fontSize * .65f));
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.supportRichText = true;
        text.lineSpacing = .86f;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, .95f);
        outline.effectDistance = new Vector2(1.25f, -1.25f);
        outline.useGraphicAlpha = true;

        return new Badge(view.Pointer, go, rt, text);
    }

    private static void Place(RectTransform badge, RectTransform icon, bool below)
    {
        float halfHeight = Mathf.Max(18, icon.rect.height * .5f);
        badge.pivot = below ? new Vector2(.5f, 1) : new Vector2(.5f, 0);
        badge.anchoredPosition = new Vector2(0, below ? -halfHeight - 7 : halfHeight + 7);
    }

    private sealed record Badge(IntPtr ViewPointer, GameObject Root, RectTransform Rect, Text Text);
}
