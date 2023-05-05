using System;

namespace MoogleEngine;

class SequenceMatcher {
    /* Taken from difflib.py
     * 
     * */
    public string a;
    public string b;
    Dictionary<char, List<int>> bindex;

    public SequenceMatcher(string a="", string b="") {
        SetSeq1(a);
        SetSeq2(b);
    }

    private static double _CalculateRatio(int matches, int length) {
        if (length <= 0) {
            return 1.0;
        }
        return matches * 2.0 / length;
    }

    public static Dictionary<char, List<int>> String2Index(string s) {
        Dictionary<char, List<int>> result = new  Dictionary<char, List<int>>();
        char letter;
        for (int i = 0; i < s.Length; i++) {
            letter = s[i];
            if (!result.ContainsKey(letter)) {
                result[letter] = new List<int>();
            }
            result[letter].Add(i);
        }
        return result;
    }

    public void SetSeq1(string a) {
        this.a = a;
    }

    public void SetSeq2(string b) {
        this.b = b;
        this.bindex = String2Index(b);
    }

    public int[] FindLongestMatch(int alo=0, int ahi=-1, int blo=0, int bhi=-1) {
        /* Find longest match in a[alo:ahi] and b[blo:bhi]
         */
        string a = this.a;
        string b = this.b;
        if (ahi == -1) {
            ahi = a.Length;
        }
        if (bhi == -1) {
            bhi = b.Length;
        }
        int besti = alo, bestj = blo, bestsize = 0, k, temp_j;
        Dictionary<int, int> j2len = new Dictionary<int, int>(), newj2len = new Dictionary<int, int>();
        // During the iteration we save the value of j in a dict (j2len)
        // to count the longest match ending with a[i-1] and b[j]

        for (int i = alo; i < ahi; i++) {
            if (!this.bindex.ContainsKey(a[i]))  {
                continue;
            }
            foreach(int j in this.bindex[a[i]]) {
                // a[i] == b[j]
                if (j < blo) {
                    // outside bounds
                    continue;
                }
                if (j >= bhi) {
                    // outside bounds
                    // they are sorted so we end it here
                    break;
                }
                temp_j = 0;
                if (j2len.ContainsKey(j-1)) {
                    temp_j = j2len[j-1];
                }
                temp_j += 1;
                k = newj2len[j] = temp_j;
                if (k > bestsize) {
                    besti = i - k + 1;
                    bestj = j - k + 1;
                    bestsize = k;
                }
            }
            j2len = newj2len;
        }
        
        // lookbehind
        while ((besti > alo) && (bestj > blo) && (a[besti-1] == b[bestj-1])) {
            besti -= 1;
            bestj -= 1;
            bestsize += 1;
        }

        // lookahead
        while ((besti+bestsize < ahi) && (bestj+bestsize < bhi) && (a[besti+bestsize] == b[bestj+bestsize])) {
            bestsize += 1;
        }

        return new int[3] {besti, bestj, bestsize};
    }

    private void SortMatches(List<int[]> matches) {
        int[] temp;
        for(int i = 0; i < matches.Count; i++) {
            for (int j = 0; j < matches.Count - i - 1; j++) {
                if (matches[j][2] < matches[j+1][2]) {
                    temp = matches[j+1];
                    matches[j+1] = matches[j];
                    matches[j] = temp;
                }
            }
        }
    }


    private List<int[]> _GetMatchingBlocks(int alo, int ahi, int blo, int bhi) {
        List<int[]> matching_blocks = new List<int[]>();
        int[] x = this.FindLongestMatch(alo, ahi, blo, bhi);
        int i = x[0];
        int j = x[1];
        int k = x[2];

        if (k != 0) { // k == 0 => no match
            matching_blocks.Add(x);
            // a[alo:i] vs b[blo:j] unknown
            // a[i:i+k] same as b[j:j+k]
            // a[i+k:ahi] vs b[j+k:bhi] unknown
            if ((alo < i) && (blo < j)) {
                // extend
                List<int[]> matches = _GetMatchingBlocks(alo, i, blo, j);
                foreach(int[] match in matches) {
                    matching_blocks.Add(match);
                }
            }
            if ((i+k < ahi) && (j+k < bhi)) {
                // extend
                List<int[]> matches = _GetMatchingBlocks(i+k, ahi,j+k, bhi);
                foreach(int[] match in matches) {
                    matching_blocks.Add(match);
                }
            }
        }
        return matching_blocks;
    }

    public List<int[]> GetMatchingBlocks() {
        /* Return three-tuples 
         * 
         * Let i, j, n be any tuple of the List we return.
         * Every Match tuple means a[i, j+n] == b[i, j+n].
         *
         * The last tuple is a dummy. len(a), len(b), 0.
         *
         * */
        int la = this.a.Length, lb = this.b.Length;
        List<int[]> matching = _GetMatchingBlocks(0, la, 0, lb);
        SortMatches(matching);

        return matching;
    }

    public double Ratio() {
        int matches = 0;
        List<int[]> triples = this.GetMatchingBlocks();
        foreach(int[] triple in triples) {
            matches += triple[2];
        }
        return _CalculateRatio(matches, this.a.Length + this.b.Length);
    }

    public double RealQuickRatio() {
        // can't have more matches than the number of elements in the
        // shorter sequence
        return _CalculateRatio(Math.Min(this.a.Length, this.b.Length), this.a.Length + this.b.Length);
    }
}

class Utils {

    public static TopRanks GetCloseMatches(string word, List<string> possibilities, int n=1, double cutoff=0.6) {
        /* Return the best matches for the word */
        if (n <= 0) {
            Console.WriteLine("ERROR: N can't be 0");
        }
        if (cutoff < 0.0 || cutoff > 1.0) {
            Console.WriteLine("ERROR: Cutoff was outside valid range");
        }

        // this can save a cache but we don't really need it riht now
        TopRanks result = new TopRanks("difflib", n);
        SequenceMatcher s = new SequenceMatcher();
        s.SetSeq2(word);
        double ratio = 0.0;
        Console.WriteLine("Looking for close match for {0} in {1} possibilities", word, possibilities.Count);
        foreach(string x in possibilities) {
            s.SetSeq1(x);
            if (s.RealQuickRatio() >= cutoff) {
                ratio = s.Ratio();
                if (ratio >= cutoff) {
                    result.Add(word, x, (float) ratio);
                }
            }
        }
        return result;
    }
}
