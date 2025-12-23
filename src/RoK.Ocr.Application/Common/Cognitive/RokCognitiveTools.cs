using System;

namespace RoK.Ocr.Application.Common.Cognitive;

public static class RokCognitiveTools
{
    /// <summary>
    /// Calculates similarity between two strings using Levenshtein distance.
    /// Returns a value between 0.0 and 1.0.
    /// </summary>
    public static double CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0.0;
        source = source.ToLower();
        target = target.ToLower();
        int dist = LevenshteinDistance(source, target);
        int maxLen = Math.Max(source.Length, target.Length);
        return 1.0 - (double)dist / maxLen;
    }

    /// <summary>
    /// Calculates the Euclidean distance between two points.
    /// </summary>
    public static double CalculateDistance(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];
        if (n == 0) return m;
        if (m == 0) return n;
        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}