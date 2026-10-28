// SPDX-License-Identifier: MIT
namespace Stitchidex.Core.Services;

/// <summary>Checkbox / Ctrl / Shift multi-selection state shared by grid cards and table rows.</summary>
public sealed class PatternSelectionModel<T> where T : notnull
{
    readonly List<T> _items = new();
    public event Action? Changed;
    public IReadOnlyList<T> Items => _items;
    public int Count => _items.Count;
    public bool IsSelected(T item) => _items.Contains(item);
    public string TagSelectedLabel => TagLabel(Count);
    public string SendToUsbLabel => UsbLabel(Count);
    public static string TagLabel(int n) => $"🏷️ Tag Selected ({n})";
    public static string UsbLabel(int n) => n > 1 ? $"💾 Send to USB ({n})" : "💾 Send to USB";

    /// <summary>Checkbox click: adds when unselected, removes when selected. Returns new state.</summary>
    public bool TogglePatternSelection(T item)
    {
        bool now = !_items.Remove(item);
        if (now) _items.Add(item);
        Changed?.Invoke();
        return now;
    }
    public void Set(T item, bool selected)
    {
        if (selected == _items.Contains(item)) return;
        if (selected) _items.Add(item); else _items.Remove(item);
        Changed?.Invoke();
    }
    /// <summary>Replace with the control's selection (Ctrl/Shift-click path).</summary>
    public void Reset(IEnumerable<T> items) { _items.Clear(); _items.AddRange(items.Distinct()); Changed?.Invoke(); }
    public void Clear() => Reset(Array.Empty<T>());
}
