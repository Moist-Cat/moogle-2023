using System;

namespace MoogleEngine;

// 1. Load docs
// (this should be in a sqlite but whatever)
// 2. Create Dict[word, Arr<doc>[5]] (sort eagerly to avoid errors) to add the top ranks
// 3. Create Dict[doc, HT[word, count]] to vectorize the documents
// 4. Rank queries with tf-idf
// 5. Store it in another dict
// 6. ???
// 7. Profit!

public static class Moogle
{
    public static SearchResult Query(string query) {
        // reload caches
        Ranker rk = new Ranker();
        // we don't want duplicates
        Dictionary<string, float> candidates = new Dictionary<string, float>();
        List<Tuple<string, float>> suggestion_ranks;
        // XXX detect when we need to suggest because we only show bad words
        string word, suggestion="";

        // partition the terms of the query
        foreach(string _word in query.Split(" ")) {
            word = _word.ToLower();
            if (word.Length < 3) {
                // useless
                continue;
            }
            // XXX case Ranker.TopRanked.Get(word) == []
            foreach (Tuple<string, float> candidate in Ranker.TopRanked.Get(word)) {
                //word = candidate.Item1
                //tfidf = candidate.Item2;
                Console.WriteLine("INFO: Found candidate " + candidate.Item1);
                if (!candidates.ContainsKey(candidate.Item1)) {
                    // init
                    candidates[candidate.Item1] = 0;
                }
                // increase score to sort them later
                candidates[candidate.Item1] += candidate.Item2;
            };
            if (Ranker.TopRanked.Get(word).Count == 0) {
                // add suggestion
                Console.WriteLine("INFO: Bad word {0}. Computing suggestion...", word);
                suggestion_ranks = Utils.GetCloseMatches(word, Ranker.TopRanked.GetKeys()).Get(word);
                if (suggestion_ranks.Count > 0) {
                    suggestion += " " + suggestion_ranks[0].Item1;
                }
                else {
                    Console.WriteLine("ERROR: Failed to get sensible suggestion for {0}", word);
                }
            }
            
        }


        SearchItem[] items = new SearchItem[candidates.Count];

        int count = 0;
        foreach(KeyValuePair<string, float> file in candidates) {
            // where Key is the filename and Value the score
            items[count++] = new SearchItem(file.Key, Ranker.GetHighlight(file.Key, query.Split()), file.Value);
        }
        Console.WriteLine(suggestion);

        return new SearchResult(items, suggestion);
    }
}
