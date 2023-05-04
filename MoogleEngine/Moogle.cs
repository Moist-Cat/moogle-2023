using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;

namespace MoogleEngine;

// 1. Load docs
// (this should be in a sqlite but whatever)
// 2. Create Dict[word, Arr<doc>[5]] (sort eagerly to avoid errors) to add the top ranks
// 3. Create Dict[doc, HT[word, count]] to vectorize the documents
// 4. Rank queries with tf-idf
// 5. Store it in another dict
// 6. ???
// 7. Profit!

class TopRanks {
    /* Data structure containing only the top N values in a Dict<string, float>
     * given a criteria */


    private int MAX_RANKED_VALUES = 5;
    private Dictionary<string, List<Tuple<string, float>>> _dict = new Dictionary<string, List<Tuple<string, float>>>();
    static public FileInfo CACHE_TOP = new FileInfo(Path.Join(Ranker.BASE_DIR.ToString(), "cache_top.json"));

    public TopRanks() {
        this._dict = Load();
    }

    public void Add(string key, string val, float criteria) {
        /* 
         * Try to add a file to the list of files with highest *criteria*
         * 
         */

        if (!this._dict.ContainsKey(key)) {
            // the word is new so it's easier
            Console.WriteLine("INFO: New ranked value: {0}, key: {1}", val, key);
            this._dict[key] = new List<Tuple<string, float>> ();
            this._dict[key].Add(new Tuple<string, float>(val, criteria));
        }
        else {
            if (this._dict[key].Count() >= MAX_RANKED_VALUES) {
                // get last (it should be always sorted)
                Tuple<string, float> last_val = this._dict[key][MAX_RANKED_VALUES - 1];
                if (last_val.Item2 < criteria) {
                    // replace
                    Console.WriteLine(
                        "INFO: Replaced a ranked value {0} ({1}) in favor of {1} ({2})"
                    , last_val.Item1, last_val.Item2, val, criteria);
                    this._dict[key][MAX_RANKED_VALUES - 1] = new Tuple<string, float>(val, criteria);
                }
                else {
                    // we didn't do anything
                    // no need to sort
                    return;
                }
            }
            else {
                // simply add it
                this._dict[key].Add(new Tuple<string, float>(val, criteria));
            }
            // keep it sorted
            this._dict[key].Sort();
            this._dict[key].Reverse();
        }
    }

    public List<Tuple<string, float>> Get(string key) {
        if (!this._dict.ContainsKey(key)) {
            Console.WriteLine("ERROR: Key not found " + key);
            return new List<Tuple<string, float>>();
        }
        return this._dict[key];
    }

    public void Save() {
        string jfiles = JsonSerializer.Serialize(this._dict);
        File.WriteAllTextAsync(CACHE_TOP.ToString(), jfiles);
    }

    public Dictionary<string, List<Tuple<string, float>>> Load() {
        if (!CACHE_TOP.Exists) {
            this.Save();
        }
        try {
            return JsonSerializer.Deserialize<Dictionary<string, List<Tuple<string, float>>>>(File.ReadAllText(CACHE_TOP.ToString()));
        }
        catch (System.Text.Json.JsonException e) {
           // empty 
           // return new
           return new Dictionary<string, List<Tuple<string, float>>>();
        }
    }

}

class Ranker {
    /* Singleton
     *
     * Load files in memory
     * */

    // important directories
    static public DirectoryInfo BASE_DIR = new DirectoryInfo(
        Assembly.GetAssembly(typeof (Moogle)).Location
    ).Parent.Parent.Parent.Parent.Parent;
    static public DirectoryInfo DATA_DIR = new DirectoryInfo(Path.Join(BASE_DIR.ToString(), "Content"));

    // cache files
    static public FileInfo CACHE_FILE = new FileInfo(Path.Join(BASE_DIR.ToString(), "cache.json"));
    static public FileInfo CACHE_WC = new FileInfo(Path.Join(BASE_DIR.ToString(), "cache_wc.json"));
    static public FileInfo CACHE_VECTORS = new FileInfo(Path.Join(BASE_DIR.ToString(), "cache_vectors.json"));
    static public FileInfo CACHE_TFIDF = new FileInfo(Path.Join(BASE_DIR.ToString(), "cache_tfidf.json"));

    // other attributes
    static public string[] PATTERNS = new string[] {"*.txt", "*.cs", "*.py"};

    // filenames
    static private HashSet<string> FILES = LoadHSJSON(CACHE_FILE);

    // number of files containing a word
    static public Dictionary<string, int> WC = LoadDictJSON(CACHE_WC);
    static public Dictionary<string, Dictionary<string, int>> VECTORS = LoadNDictJSON(CACHE_VECTORS);
    static public Dictionary<string, Dictionary<string, float>> TFIDF = LoadFloaNtDictJSON(CACHE_TFIDF);
    static public TopRanks TopRanked = new TopRanks();

    static public int CACHE_TIMEOUT = 5; // minutes

    public Ranker() {
        // reload cache
        GetFiles(DATA_DIR);
    }

    private static HashSet<string> _GetFiles(DirectoryInfo dir) {
        /*
         * Get all the filenames from the BASE_DIR/files directory recursively 
         * 
         * */

        // ASCII text files: .txt .py .cs
        HashSet<string> files = new HashSet<string>();

        // it doesn't support regex only globs
        // this doesn't cost much so I won't waste time
        // optimizing it
        foreach(string pattern in PATTERNS) {
            Console.WriteLine("INFO: Using pattern " + pattern);
            foreach(FileInfo file in dir.GetFiles(pattern)) {
                if (FILES.Contains(file.ToString())) {
                    // cached
                    continue;
                }
                Console.WriteLine("INFO: Found " + file.ToString());
                files.Add(file.ToString());
            }
        }
        foreach(DirectoryInfo d in dir.GetDirectories()) {
            Console.WriteLine("INFO: Searching dir" + d.ToString());
            files.UnionWith(_GetFiles(d));
        }

        return files;
    }

    public static HashSet<string> GetFiles(DirectoryInfo dir) {
        /* Public wrapper to use cache 
         *
         * XXX I wish i could join all the wrappers in a single method
         * */

        if (CACHE_FILE.LastWriteTime.AddMinutes(5) < DateTime.Now) {
            // update cache every 5 mins
            Console.WriteLine("WARNING: Updating cache for " + CACHE_FILE.ToString());
            // _GetFiles only gets the new files
            // NOTE we could follow Dr. Piad's advice and update only every N new files
            // but we would have to keep track of the count
            // NOTE There is also the event where a file is modified. We would have an invalid cache.
            // To solve that we would have to keep track of the hashes of the files and
            // check all them every N minutes. A costly operation without too much reward for
            // our use-case.
            HashSet<string> new_files = _GetFiles(dir);
            if (new_files.Count != 0) {
                Console.WriteLine("INFO: Got " + new_files.Count.ToString() + " new files");
                Console.WriteLine("WARNING: Updating cache for " + CACHE_VECTORS.ToString());
                Vectorize(new_files);
                Console.WriteLine("WARNING: Updating cache for " + CACHE_TFIDF.ToString());
                TFIDF = UpdateTfIdf();
                SaveJSON(VECTORS, CACHE_VECTORS);
                // also update the wc and tfidf dictionaries
                SaveJSON(WC, CACHE_WC);
                SaveJSON(TFIDF, CACHE_TFIDF);
            }

            FILES.UnionWith(new_files);
            SaveJSON(FILES, CACHE_FILE);

        }
        return FILES;
    }

    public static Dictionary<string, int> GetWords(string filename) {
        /* Count words of a file */
        Dictionary<string, int> words = new Dictionary<string, int>();
        foreach(string line in File.ReadAllLines(filename)) {
            foreach(string word in line.Split(" ")) {
                // ignore len(word) < 3 and anything that doesn't look like a word
                if (word.Length < 3 || !Regex.Match(word, @"^[a-zA-Z,.;:]+$").Success) {
                    continue;
                }
                // clean up
                string lword = Regex.Replace(word.ToLower(), @"[,.;:]", "");
                
                if (!words.ContainsKey(lword)) {
                    words[lword] = 0;
                }
                words[lword] += 1;
            }
        }
        return words;
    }


    public static void Vectorize(HashSet<string> filenames) {
        /* Map all the files with their word count for each word
         *
         * {
         * [filename]: {
         *     [word_1]: [count_1],
         *     [word_2]: [count_2],
         *     ...
         * },
         * [filename]: {
         * ...
         * },
         * }
         *
         * */
        foreach(string filename in filenames) {
            // avoid using the costly GetWords method
            if (!VECTORS.ContainsKey(filename)) {
                Console.WriteLine("INFO: " + filename + " was sucessfully vectorized");
                VECTORS[filename] = GetWords(filename);
                // update the other dict
                UpdateWC(filename, VECTORS[filename]);
            }
        }
    }

    private static Dictionary<string, Dictionary<string, float>> UpdateTfIdf() {
        /* Term Frequency--Inverse term frequency algorithm implementation
         * https://en.wikipedia.org/wiki/Tf%E2%80%93idf
         *
         * Return a dict (Dictionary<document, Dictionary<word, tf-idf>>)
         * NOTE We don't do the changes in-place because we want to
         * cache 
         * NOTE We work with the internal properties (cache)
         * */

        Dictionary<string, Dictionary<string, float>> docs_tfidf = new Dictionary<string, Dictionary<string, float>>();
        // The formula is pretty simple so we can (and it's more efficient)
        // to calculate everything at once
        int N = VECTORS.Count();
        foreach(KeyValuePair<string, Dictionary<string, int>> file in VECTORS) {
            string filename = file.Key;
            Dictionary<string, int> words = file.Value;
            // here we will store a dict with the tf-idf value for 
            // each word of the document
            docs_tfidf[filename] = new Dictionary<string, float>();
            foreach(KeyValuePair<string, int> word_key in words) {
                string word = word_key.Key;
                int count = word_key.Value;

                // here we apply the formula
                int tf = count;
                float idf = (float) Math.Log(N / WC[word]);

                docs_tfidf[filename][word] = tf*idf;
                TopRanked.Add(word, filename, tf*idf);
            }
        }
        TopRanked.Save();
        return docs_tfidf;
    }

    public static void UpdateWC(string filename, Dictionary<string, int> words) {
        /* Receives a dictionary of words for a file and updates the word count 
         * accordingly
         */
        Console.WriteLine("INFO: Updating wc for {0}", filename);
        foreach(KeyValuePair<string, int> word_key in words) {
            string word = word_key.Key;
            int count = word_key.Value;
            
            if (!WC.ContainsKey(word)) {
                Console.WriteLine("INFO: New word {0} from {1}", word, filename);
                WC[word] = 1;
            }
            else {
                WC[word]++;
            }
        }
    }

    public static void SaveJSON(HashSet<string> files, FileInfo outfile) {
        string jfiles = JsonSerializer.Serialize(files);
        File.WriteAllTextAsync(outfile.ToString(), jfiles);
    }
    public static void SaveJSON(Dictionary<string, int> files, FileInfo outfile) {
        string jfiles = JsonSerializer.Serialize(files);
        File.WriteAllTextAsync(outfile.ToString(), jfiles);
    }
    public static void SaveJSON(Dictionary<string, Dictionary<string, int>> files, FileInfo outfile) {
        string jfiles = JsonSerializer.Serialize(files);
        File.WriteAllTextAsync(outfile.ToString(), jfiles);
    }
    public static void SaveJSON(Dictionary<string, Dictionary<string, float>> files, FileInfo outfile) {
        string jfiles = JsonSerializer.Serialize(files);
        File.WriteAllTextAsync(outfile.ToString(), jfiles);
    }

    public static HashSet<string> LoadHSJSON(FileInfo file) {
        if (!file.Exists) {
            SaveJSON(new HashSet<string>(), CACHE_FILE);
        }
        return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(file.ToString()));
    }

    public static Dictionary<string, int> LoadDictJSON(FileInfo file) {
        if (!file.Exists) {
            SaveJSON(new Dictionary<string, int>(), file);
        }

        try {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(file.ToString()));
        }
        catch (System.Text.Json.JsonException e) {
           // empty 
           // return new
           return new Dictionary<string, int>();
        }
    }
    public static Dictionary<string, Dictionary<string, int>> LoadNDictJSON(FileInfo file) {
        if (!file.Exists) {
            SaveJSON(new Dictionary<string, Dictionary<string,int>>(), file);
        }

        try {
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(File.ReadAllText(file.ToString()));
        }
        catch (System.Text.Json.JsonException e) {
           // empty 
           // return new
           return new Dictionary<string, Dictionary<string, int>>();
        }
    }

    public static Dictionary<string, Dictionary<string, float>> LoadFloaNtDictJSON(FileInfo file) {
        if (!file.Exists) {
            SaveJSON(new Dictionary<string, Dictionary<string, float>>(), file);
        }

        try {
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, float>>>(File.ReadAllText(file.ToString()));
        }
        catch (System.Text.Json.JsonException e) {
           // empty 
           // return new
           return new Dictionary<string, Dictionary<string, float>>();
        }

    }


    public static int GetWC(string word) {
        /* Getter for WC */
        // TODO implement meme difflib algo
        return WC[word];
    }

    public static int GetFileWC(string filename, string word) {
        return VECTORS[filename][word];
    }

    public static string GetHighlight(string filename, string[] words) {
        /* Match the neighbourhood V(word, 10) of the word "word" in the document with regex */

        Console.WriteLine("Fetching highlight for " + filename);
        string text = File.ReadAllText(filename);
        string match = "";
        foreach(string word in words) {
            match = Regex.Match(text, @".*" + word + @".*\n").Value;
            if (match.Length > 0) {
                if (match.Length > 500) {
                    // get first 500
                    match = match.Substring(0, 500);
                    // XXX the word might not appear
                }
                Console.WriteLine(match + "\n");
                // match one line to speed up things
                break;
            }
        }

        return match + "\n";
    }
}

public static class Moogle
{
    public static SearchResult Query(string query) {
        // reload caches
        Ranker rk = new Ranker();
        // we don't want duplicates
        Dictionary<string, float> candidates = new Dictionary<string, float>();
        string word;

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
                Console.WriteLine("Found candidate " + candidate.Item1);
                if (!candidates.ContainsKey(candidate.Item1)) {
                    // init
                    candidates[candidate.Item1] = 0;
                }
                // increase score to sort them later
                candidates[candidate.Item1] += candidate.Item2;
            };
            
        }


        SearchItem[] items = new SearchItem[candidates.Count];

        int count = 0;
        foreach(KeyValuePair<string, float> file in candidates) {
            // where Key is the filename and Value the score
            items[count++] = new SearchItem(file.Key, Ranker.GetHighlight(file.Key, query.Split()), file.Value);
        }

        return new SearchResult(items, query);
    }
}
