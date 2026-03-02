using System.Text.RegularExpressions;

namespace AiAgent.Infrastructure.Tools;

/// <summary>
/// 語言檢測助手 - 自動識別用戶輸入的語言
/// </summary>
public static class LanguageDetectionHelper
{
    /// <summary>
    /// 語言代碼
    /// </summary>
    public enum Language
    {
        Chinese,
        English,
        Japanese,
        Korean,
        Spanish,
        French,
        German,
        Portuguese,
        Russian,
        Arabic,
        Unknown
    }

    /// <summary>
    /// 檢測文本的語言
    /// </summary>
    public static Language DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Language.Unknown;

        var trimmedText = text.Trim();

        // 檢測中文（CJK 統一表意文字）
        if (ContainsChineseCharacters(trimmedText))
            return Language.Chinese;

        // 檢測日文（平假名、片假名）
        if (ContainsJapaneseCharacters(trimmedText))
            return Language.Japanese;

        // 檢測韓文（韓字）
        if (ContainsKoreanCharacters(trimmedText))
            return Language.Korean;

        // 檢測阿拉伯文
        if (ContainsArabicCharacters(trimmedText))
            return Language.Arabic;

        // 檢測俄文
        if (ContainsRussianCharacters(trimmedText))
            return Language.Russian;

        // 檢測西班牙文、葡萄牙文、法文、德文
        var detectedLatin = DetectLatinLanguage(trimmedText);
        if (detectedLatin != Language.Unknown)
            return detectedLatin;

        // 預設為英文（拉丁字母）
        return Language.English;
    }

    /// <summary>
    /// 取得語言顯示名稱
    /// </summary>
    public static string GetLanguageName(Language language) =>
        language switch
        {
            Language.Chinese => "繁體中文 (zh-TW)",
            Language.English => "English (en)",
            Language.Japanese => "日本語 (ja)",
            Language.Korean => "한국어 (ko)",
            Language.Spanish => "Español (es)",
            Language.French => "Français (fr)",
            Language.German => "Deutsch (de)",
            Language.Portuguese => "Português (pt)",
            Language.Russian => "Русский (ru)",
            Language.Arabic => "العربية (ar)",
            _ => "Unknown"
        };

    /// <summary>
    /// 取得語言代碼（ISO 639-1）
    /// </summary>
    public static string GetLanguageCode(Language language) =>
        language switch
        {
            Language.Chinese => "zh-TW",
            Language.English => "en",
            Language.Japanese => "ja",
            Language.Korean => "ko",
            Language.Spanish => "es",
            Language.French => "fr",
            Language.German => "de",
            Language.Portuguese => "pt",
            Language.Russian => "ru",
            Language.Arabic => "ar",
            _ => "unknown"
        };

    /// <summary>
    /// 取得系統提示詞中的語言指示
    /// </summary>
    public static string GetLanguageInstructions(Language language) =>
        language switch
        {
            Language.Chinese => "使用繁體中文回覆。",
            Language.English => "Reply in English.",
            Language.Japanese => "日本語で返答してください。",
            Language.Korean => "한국어로 답변해주세요.",
            Language.Spanish => "Responde en español.",
            Language.French => "Répondez en français.",
            Language.German => "Antworten Sie auf Deutsch.",
            Language.Portuguese => "Responda em português.",
            Language.Russian => "Ответьте на русском языке.",
            Language.Arabic => "الرجاء الرد باللغة العربية.",
            _ => ""
        };

    // ── 私有辅助方法 ──

    private static bool ContainsChineseCharacters(string text)
    {
        // CJK 統一表意文字：\u4e00-\u9fff
        // 帶拼音符號的中文
        return Regex.IsMatch(text, @"[\u4e00-\u9fff\u3400-\u4dbf]");
    }

    private static bool ContainsJapaneseCharacters(string text)
    {
        // 平假名：\u3040-\u309f
        // 片假名：\u30a0-\u30ff
        return Regex.IsMatch(text, @"[\u3040-\u309f\u30a0-\u30ff]");
    }

    private static bool ContainsKoreanCharacters(string text)
    {
        // 韓文字母（韓字）：\uac00-\ud7af
        // 補充韓文：\u1100-\u11ff
        return Regex.IsMatch(text, @"[\uac00-\ud7af\u1100-\u11ff]");
    }

    private static bool ContainsArabicCharacters(string text)
    {
        // 阿拉伯文：\u0600-\u06ff
        return Regex.IsMatch(text, @"[\u0600-\u06ff]");
    }

    private static bool ContainsRussianCharacters(string text)
    {
        // 西里爾字母：\u0400-\u04ff
        return Regex.IsMatch(text, @"[\u0400-\u04ff]");
    }

    private static Language DetectLatinLanguage(string text)
    {
        // 西班牙文特徵字
        if (Regex.IsMatch(text, @"\b(el|la|los|las|que|para|con|esta|está|hola)\b", RegexOptions.IgnoreCase))
            return Language.Spanish;

        // 法文特徵字
        if (Regex.IsMatch(text, @"\b(le|la|les|des|qui|que|pour|est|bonjour)\b", RegexOptions.IgnoreCase))
            return Language.French;

        // 德文特徵字（德文通常有大寫名詞）
        if (Regex.IsMatch(text, @"\b([A-Z]\w+|der|die|das|und|sein|haben)\b"))
            return Language.German;

        // 葡萄牙文特徵字
        if (Regex.IsMatch(text, @"\b(o|a|os|as|que|para|ã|õ|ç)\b", RegexOptions.IgnoreCase))
            return Language.Portuguese;

        return Language.Unknown;
    }
}
