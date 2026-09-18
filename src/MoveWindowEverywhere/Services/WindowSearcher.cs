using MoveWindowEverywhere.Models;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 窗口搜索筛选：按窗口标题或进程名匹配，支持按空格分隔的多个关键词（需全部命中）。
/// 纯逻辑，无 Win32 调用，便于单元测试。
/// </summary>
public static class WindowSearcher
{
    public static IReadOnlyList<WindowInfo> Search(IEnumerable<WindowInfo> source, string? query)
    {
        string[] terms = SplitTerms(query);

        var ranked = new List<(int Score, int Index, WindowInfo Item)>();
        int index = 0;
        foreach (WindowInfo window in source)
        {
            int score = Score(window, terms);
            if (score >= 0)
            {
                ranked.Add((score, index, window));
            }

            index++;
        }

        ranked.Sort(static (a, b) =>
        {
            int byScore = a.Score.CompareTo(b.Score);
            return byScore != 0 ? byScore : a.Index.CompareTo(b.Index);
        });

        var results = new List<WindowInfo>(ranked.Count);
        foreach ((int _, int _, WindowInfo item) in ranked)
        {
            results.Add(item);
        }

        return results;
    }

    public static string[] SplitTerms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<string>();
        }

        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>返回匹配得分，越小越靠前；-1 表示不匹配。</summary>
    private static int Score(WindowInfo window, string[] terms)
    {
        if (terms.Length == 0)
        {
            return 0;
        }

        int total = 0;
        foreach (string term in terms)
        {
            int termScore = ScoreTerm(window, term);
            if (termScore < 0)
            {
                return -1;
            }

            total += termScore;
        }

        return total;
    }

    private static int ScoreTerm(WindowInfo window, string term)
    {
        string title = window.Title ?? string.Empty;
        string process = window.ProcessName ?? string.Empty;

        if (title.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (title.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        if (process.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        if (process.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return 30;
        }

        return -1;
    }
}
