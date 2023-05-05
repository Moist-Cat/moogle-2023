using System;

namespace MoogleEngine;

class SequenceMatcher {
    /* Taken from difflib.py
     * 
     * */
    string a;
    string b;

    public SequenceMatcher(string a="", string b="") {
        this.a = a;
        this.b = b;
    }

    private double _CalculateRatio(int matches, int length) {
        if (length <= 0) {
            return 1.0;
        }
        return matches * 2.0 / length;
    }

    public void SetSeq1(string a) {
        this.a = a;
    }
    public void SetSeq2(string b) {
        this.b = b;
    }

    public double Ratio() {
        int sum = 0;
        //for triple in this.GetMatchingB
        return 1.0;
    }

    public double QuickRatio() {
        return 1.0;
    }

    public double RealQuickRatio() {
        return 1.0;
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
        double ratio = 0.0;
        foreach(string x in possibilities) {
            s.SetSeq1(x);
            if (s.RealQuickRatio() >= cutoff && s.QuickRatio() >= cutoff) {
                ratio = s.Ratio();
                if (ratio >= cutoff) {
                    result.Add(word, x, (float) ratio);
                }
            }
        }
        return result;
    }
}
