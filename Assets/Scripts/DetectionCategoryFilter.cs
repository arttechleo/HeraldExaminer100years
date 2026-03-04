using System;
using System.Collections.Generic;

/// <summary>
/// Centralized 3-category allowlist for AI detection. Only Human, Screen (laptop/TV/monitor), and Book
/// are considered allowed; everything else is filtered out before anchor resolve or overlay work.
/// Case-insensitive label matching. Optional class-ID mapping if the detector ever exposes numeric IDs.
/// </summary>
public static class DetectionCategoryFilter
{
    public enum Category
    {
        None,
        Human,
        Screen,
        Book,
        Chair,
        Desk,
        Keyboard,
        Mouse,
        Phone,
        Cup
    }

    private static readonly HashSet<string> HumanTokens = new(StringComparer.OrdinalIgnoreCase)
        { "person", "human" };

    private static readonly HashSet<string> ScreenTokens = new(StringComparer.OrdinalIgnoreCase)
        { "laptop", "monitor", "screen", "computer", "pc", "tv", "television" };

    private static readonly HashSet<string> BookTokens = new(StringComparer.OrdinalIgnoreCase)
        { "book", "books", "textbook", "notebook" };

    private static readonly HashSet<string> ChairTokens = new(StringComparer.OrdinalIgnoreCase)
        { "chair", "seat", "stool" };

    private static readonly HashSet<string> DeskTokens = new(StringComparer.OrdinalIgnoreCase)
        { "desk", "table" };

    private static readonly HashSet<string> KeyboardTokens = new(StringComparer.OrdinalIgnoreCase)
        { "keyboard" };

    private static readonly HashSet<string> MouseTokens = new(StringComparer.OrdinalIgnoreCase)
        { "mouse" };

    private static readonly HashSet<string> PhoneTokens = new(StringComparer.OrdinalIgnoreCase)
        { "phone", "cellphone", "mobile", "smartphone" };

    private static readonly HashSet<string> CupTokens = new(StringComparer.OrdinalIgnoreCase)
        { "cup", "mug", "bottle", "glass" };

    /// <summary>
    /// If the detector provides numeric class IDs and a label list, map class index -> Category here
    /// so we can filter by ID instead of string. Leave null to use label-based filtering only.
    /// </summary>
    private static Dictionary<int, Category> s_classIdToCategory;

    /// <summary>
    /// Classify a label token (e.g. from "label score" after splitting). Returns one of Human, Screen, Book, or None.
    /// Case-insensitive and robust to common variations.
    /// </summary>
    public static Category Classify(string baseLabel)
    {
        if (string.IsNullOrWhiteSpace(baseLabel)) return Category.None;
        string token = baseLabel.Trim().ToLowerInvariant();
        if (HumanTokens.Contains(token)) return Category.Human;
        if (ScreenTokens.Contains(token)) return Category.Screen;
        if (BookTokens.Contains(token) || token.Contains("book")) return Category.Book;
        if (ChairTokens.Contains(token)) return Category.Chair;
        if (DeskTokens.Contains(token)) return Category.Desk;
        if (KeyboardTokens.Contains(token)) return Category.Keyboard;
        if (MouseTokens.Contains(token)) return Category.Mouse;
        if (PhoneTokens.Contains(token) || token.Contains("phone")) return Category.Phone;
        if (CupTokens.Contains(token)) return Category.Cup;
        return Category.None;
    }

    /// <summary>Display hint for Screen category tokens (for overlays).</summary>
    public static readonly string ScreenTokensHint = "laptop, monitor, screen, computer, pc";

    /// <summary>
    /// True if the detection is in the allowlist (Human, Screen, or Book). Filter out when false.
    /// </summary>
    public static bool IsAllowed(Category category) => category != Category.None;

    /// <summary>
    /// Optional: set class-ID -> category mapping if your detector exposes numeric class IDs
    /// (e.g. from UnityInferenceEngineProvider's classLabelsAsset order). Then use ClassifyFromClassId.
    /// </summary>
    public static void SetClassIdMapping(IReadOnlyDictionary<int, Category> mapping)
    {
        s_classIdToCategory = mapping == null ? null : new Dictionary<int, Category>(mapping);
    }

    /// <summary>
    /// Classify by numeric class ID when SetClassIdMapping has been used. Returns None if no mapping.
    /// </summary>
    public static Category ClassifyFromClassId(int classId)
    {
        if (s_classIdToCategory != null && s_classIdToCategory.TryGetValue(classId, out var cat))
            return cat;
        return Category.None;
    }
}
