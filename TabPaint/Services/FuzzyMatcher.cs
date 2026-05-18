using System;
using System.Collections.Generic;
using System.Linq;
using TabPaint.Controls;

namespace TabPaint.Services
{
    public class FuzzyMatcher
    {
        public static List<(SearchItem Item, double Score)> Match(
            IEnumerable<SearchItem> candidates, string query, int maxResults = 8)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<(SearchItem, double)>();

            query = query.Trim();
            string queryLower = query.ToLowerInvariant();

            var scored = new List<(SearchItem Item, double Score)>();

            foreach (var item in candidates)
            {
                double bestScore = 0;

                foreach (var term in item.SearchTerms)
                {
                    if (string.IsNullOrEmpty(term)) continue;
                    string termLower = term.ToLowerInvariant();

                    double score = CalculateScore(queryLower, termLower);
                    if (score > bestScore)
                        bestScore = score;
                }

                if (bestScore > 0)
                    scored.Add((item, bestScore));
            }

            return scored
                .OrderByDescending(x => x.Score)
                .Take(maxResults)
                .ToList();
        }

        private static double CalculateScore(string query, string target)
        {
            if (query == target)
                return 1.0;

            if (target.StartsWith(query))
                return 0.95;

            if (target.Contains(query))
            {
                int idx = target.IndexOf(query);
                double positionBonus = 1.0 - (double)idx / target.Length * 0.3;
                return 0.85 * positionBonus;
            }

            double acronymScore = AcronymMatchScore(query, target);
            if (acronymScore > 0)
                return 0.75 * acronymScore;

            double subseqScore = SubsequenceScore(query, target);
            if (subseqScore > 0)
                return 0.6 * subseqScore;

            double editScore = EditDistanceScore(query, target);
            if (editScore > 0)
                return 0.4 * editScore;

            return 0;
        }

        private static double AcronymMatchScore(string query, string target)
        {
            var initials = new List<char>();
            bool newWord = true;
            for (int i = 0; i < target.Length; i++)
            {
                char c = target[i];
                if (c == ' ' || c == '_' || c == '-' || c == '/')
                {
                    newWord = true;
                    continue;
                }
                if (newWord || (i > 0 && char.IsLower(target[i - 1]) && char.IsUpper(c)))
                {
                    initials.Add(char.ToLowerInvariant(c));
                    newWord = false;
                }
                else
                {
                    newWord = false;
                }
            }

            if (initials.Count == 0) return 0;

            int qi = 0;
            for (int ii = 0; ii < initials.Count && qi < query.Length; ii++)
            {
                if (query[qi] == initials[ii])
                    qi++;
            }

            if (qi == query.Length)
                return (double)query.Length / initials.Count;

            return 0;
        }

        private static double SubsequenceScore(string query, string target)
        {
            if (query.Length > target.Length) return 0;
            if (query.Length == 0) return 0;

            int[] matchPositions = new int[query.Length];
            int qi = 0;

            for (int ti = 0; ti < target.Length && qi < query.Length; ti++)
            {
                if (query[qi] == target[ti])
                {
                    matchPositions[qi] = ti;
                    qi++;
                }
            }

            if (qi < query.Length) return 0;

            int span = matchPositions[query.Length - 1] - matchPositions[0] + 1;
            double compactness = (double)query.Length / span;
            double coverage = (double)query.Length / target.Length;
            double positionBonus = 1.0 - (double)matchPositions[0] / target.Length * 0.5;

            int consecutive = 0;
            for (int i = 1; i < matchPositions.Length; i++)
            {
                if (matchPositions[i] == matchPositions[i - 1] + 1)
                    consecutive++;
            }
            double continuityBonus = query.Length > 1
                ? (double)consecutive / (query.Length - 1)
                : 1.0;

            return compactness * 0.4
                 + coverage * 0.2
                 + positionBonus * 0.2
                 + continuityBonus * 0.2;
        }

        private static double EditDistanceScore(string query, string target)
        {
            if (query.Length <= 1) return 0;

            int bestDist = int.MaxValue;
            int windowLen = query.Length;
            int maxAllowed = query.Length <= 3 ? 1 : (query.Length <= 6 ? 2 : 3);

            for (int start = 0; start <= target.Length - windowLen; start++)
            {
                string window = target.Substring(start, windowLen);
                int dist = LevenshteinDistance(query, window);
                if (dist < bestDist)
                    bestDist = dist;
                if (bestDist == 0) break;
            }

            if (Math.Abs(target.Length - query.Length) <= maxAllowed)
            {
                int fullDist = LevenshteinDistance(query, target);
                if (fullDist < bestDist)
                    bestDist = fullDist;
            }

            if (bestDist > maxAllowed) return 0;

            return 1.0 - (double)bestDist / Math.Max(query.Length, 1);
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            var prev = new int[m + 1];
            var curr = new int[m + 1];

            for (int j = 0; j <= m; j++)
                prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }
                var temp = prev;
                prev = curr;
                curr = temp;
            }

            return prev[m];
        }
    }
}
