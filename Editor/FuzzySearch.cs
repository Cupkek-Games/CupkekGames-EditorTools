using System;
using System.Collections.Generic;

namespace CupkekGames.EditorTools
{
    /// <summary>
    /// Generic fuzzy search with split-word support.
    /// Each space-separated word in the query is fuzzy-matched independently against the candidate.
    /// All words must match for the candidate to pass. Results are scored and ranked.
    /// </summary>
    public static class FuzzySearch
    {
        public struct Result<T>
        {
            public T Item;
            public int Score;
        }

        /// <summary>
        /// Filters and ranks items by fuzzy-matching the query against each item's text.
        /// Query is split by spaces — all words must fuzzy-match for the item to be included.
        /// Results are sorted best-match-first (highest score).
        /// </summary>
        /// <param name="query">Search query (split by spaces into words).</param>
        /// <param name="items">Items to search through.</param>
        /// <param name="getText">Function to extract searchable text from an item.</param>
        /// <returns>Matching items sorted by relevance (best first).</returns>
        public static List<Result<T>> Search<T>(string query, IReadOnlyList<T> items, Func<T, string> getText)
        {
            List<Result<T>> results = new List<Result<T>>();

            if (string.IsNullOrWhiteSpace(query))
            {
                for (int i = 0; i < items.Count; i++)
                    results.Add(new Result<T> { Item = items[i], Score = 0 });
                return results;
            }

            string[] words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < items.Count; i++)
            {
                string text = getText(items[i]);
                if (string.IsNullOrEmpty(text))
                    continue;

                int totalScore = 0;
                bool allMatch = true;

                for (int w = 0; w < words.Length; w++)
                {
                    int wordScore = ScoreFuzzy(words[w], text);
                    if (wordScore < 0)
                    {
                        allMatch = false;
                        break;
                    }
                    totalScore += wordScore;
                }

                if (allMatch)
                    results.Add(new Result<T> { Item = items[i], Score = totalScore });
            }

            results.Sort((a, b) => b.Score.CompareTo(a.Score));
            return results;
        }

        /// <summary>
        /// Scores how well a single word fuzzy-matches against a candidate string.
        /// Returns -1 if no match. Higher score = better match.
        /// </summary>
        /// <remarks>
        /// Scoring:
        /// - Exact substring match (case-insensitive): 1000 bonus
        /// - Starts-with match: 500 bonus
        /// - Per matched character: +10
        /// - Consecutive matched characters: +5 bonus each
        /// - Match at start of candidate: +3 bonus
        /// - Match at camelCase boundary (uppercase after lowercase): +3 bonus
        /// - Gap penalty: -1 per skipped character
        /// </remarks>
        public static int ScoreFuzzy(string word, string candidate)
        {
            if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(candidate))
                return -1;

            // Exact substring bonus
            int substringIndex = candidate.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (substringIndex >= 0)
            {
                int bonus = 1000;
                if (substringIndex == 0)
                    bonus += 500;
                // Case-exact match bonus
                if (candidate.IndexOf(word, StringComparison.Ordinal) >= 0)
                    bonus += 200;
                return bonus + word.Length * 10;
            }

            // Fuzzy character-by-character matching
            int score = 0;
            int candidateIndex = 0;
            int prevMatchIndex = -2; // track consecutive matches

            for (int wi = 0; wi < word.Length; wi++)
            {
                char wc = char.ToLowerInvariant(word[wi]);
                bool found = false;

                while (candidateIndex < candidate.Length)
                {
                    char cc = char.ToLowerInvariant(candidate[candidateIndex]);

                    if (cc == wc)
                    {
                        score += 10; // base match

                        // Consecutive bonus
                        if (candidateIndex == prevMatchIndex + 1)
                            score += 5;

                        // Start-of-string bonus
                        if (candidateIndex == 0)
                            score += 3;

                        // CamelCase boundary bonus (uppercase char after lowercase)
                        if (candidateIndex > 0
                            && char.IsUpper(candidate[candidateIndex])
                            && char.IsLower(candidate[candidateIndex - 1]))
                            score += 3;

                        prevMatchIndex = candidateIndex;
                        candidateIndex++;
                        found = true;
                        break;
                    }

                    // Gap penalty
                    score -= 1;
                    candidateIndex++;
                }

                if (!found)
                    return -1;
            }

            return score;
        }
    }
}
