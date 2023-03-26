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
// 2. Create hashtable[word, Arr<doc>[5]] (sort eagerly to avoid adding unnecessary shite)
// 3. Create hashtable[doc, HT[word, count]]
// 4. Rank queries dividing the query in words and using the wc to calculate a score
// . Markov chain
// ???
// n. Profit

class FileTools {
    /* Singleton to handle files and load words to memory */

    // important directories
    static public DirectoryInfo BASE_DIR = new DirectoryInfo(
        Assembly.GetAssembly(typeof (Moogle)).Location
    ).Parent.Parent.Parent.Parent.Parent;
    static public DirectoryInfo DATA_DIR = new DirectoryInfo(BASE_DIR.ToString() + "/files");
    // cache files
    static public FileInfo CACHE_FILE = new FileInfo(BASE_DIR.ToString() + "/cache.json");
    static public FileInfo CACHE_WC = new FileInfo(BASE_DIR.ToString() + "/cache_wc.json");
    static public FileInfo CACHE_RANKS = new FileInfo(BASE_DIR.ToString() + "/cache_ranks.json");

    // other attributes
    static public string[] PATTERNS = new string[] {"*.txt", "*.cs", "*.py"};
    static private HashSet<string> FILES = LoadHSJSON(CACHE_FILE);
    static private Dictionary<string, List<string>> WC = LoadDictJSON(CACHE_WC);
    static private Dictionary<string, Dictionary<string, int>> RANKS = LoadNDictJSON(CACHE_RANKS);

    static public int CACHE_TIMEOUT = 5; // minutes
    static public int MAX_RANKED_FILES = 5;

    public FileTools() {
        // reload caches and stuff
        GetRanks(GetFiles(DATA_DIR));
    }

    private HashSet<string> _GetFiles(DirectoryInfo dir) {
        /* Private.
         * Get all the filenames from the BASE_DIR/files directory recursively 
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
            files.UnionWith(this._GetFiles(d));
        }

        return files;
    }

    public HashSet<string> GetFiles(DirectoryInfo dir) {
        /* Public wrapper to use cache 
         *
         * XXX I wish i could join those in a single method
         * */

        if (CACHE_FILE.LastWriteTime.AddMinutes(5) < DateTime.Now) {
            // update cache every 5 mins
            Console.WriteLine("WARNING: Updating cache for " + CACHE_FILE.ToString());
            // _GetFiles only gets the new files
            HashSet<string> new_files = this._GetFiles(dir);
            if (new_files.Count != 0) {
                Console.WriteLine("INFO: Got " + new_files.Count.ToString() + " new files");
                Console.WriteLine("WARNING: Updating cache for " + CACHE_RANKS.ToString());
                this._GetRanks(new_files);
                SaveJSON(RANKS, CACHE_RANKS);
                // also update the wc dictionary
                SaveJSON(WC, CACHE_WC);
            }

            FILES.UnionWith(new_files);
            SaveJSON(FILES, CACHE_FILE);

        }
        return FILES;
    }

    public static Dictionary<string, int> GetWords(string filename) {
        /* Count words from a file */
        Dictionary<string, int> words = new Dictionary<string, int>();
        foreach(string line in File.ReadAllLines(filename)) {
            foreach(string word in line.Split(" ")) {
                // ignore len(word) < 3
                if (word.Length < 3 || !Regex.Match(word, "^[a-zA-Z]+$").Success) {
                    continue;
                }
                if (!words.ContainsKey(word)) {
                    words[word] = 0;
                }
                words[word] += 1;
            }
        }
        return words;
    }


    private void _GetRanks(HashSet<string> filenames) {
        foreach(string filename in filenames) {
            // avoid using the costly GetWords method
            if (!RANKS.ContainsKey(filename)) {
                Console.WriteLine("INFO: " + filename + " sucessfully was ranked");
                RANKS[filename] = GetWords(filename);
                // update the other dict
                UpdateWC(filename, RANKS[filename]);
            }
        }
    }

    public Dictionary<string, Dictionary<string, int>> GetRanks(HashSet<string> filenames) {
        /* Public wrapper with cache for wc */
        return RANKS;
    }

    public static void UpdateWC(string filename, Dictionary<string, int> words) {
        /* Received a dictionary of words for a file and updates the word count 
         * accordingly (sorting when needed)
         */
        // 1. Iterate words
        // 2. Try to add the filename using the count (no more than MAX_RANKED_FILES)
        Console.WriteLine("INFO: Updating wc for {0}", filename);
        foreach(KeyValuePair<string, int> word in words) {
            if (!WC.ContainsKey(word.Key)) {
                // the word is new so it's easier
                // we just add an entry with the filename as the first value
                Console.WriteLine("INFO: New word {0} from {1}", word.Key, filename);
                WC[word.Key] = new List<string> {filename};
            }
            // NOTE where word.key is the word and word.value is the count
            else {
                if (WC[word.Key].Count() >= MAX_RANKED_FILES) {
                    // get last (it should be always sorted)
                    string last_file = WC[word.Key][MAX_RANKED_FILES - 1];
                    // the wc for this file with the least number of occurences of
                    // the word
                    // compare it to the new contender's
                    if (RANKS[last_file][word.Key] < word.Value) {
                        // replace
                        Console.WriteLine(
                            "INFO: Replaced a ranked file {0} in favor of {1} with {2} wc "
                        , last_file, filename, word.Value);
                        WC[word.Key][MAX_RANKED_FILES - 1] = filename;
                    }
                    else {
                        // we didn't do anything
                        // no need to sort
                        continue;
                    }
                }
                else {
                    // simply add it
                    WC[word.Key].Add(filename);
                }
                // keep it sorted
                WC[word.Key].Sort();
            }
        }
        // NOTE we could use the chance to clean up any keys with < 3 elements
        // not that it matters, though (it's a hashlib)
    }

    public static void SaveJSON(HashSet<string> files, FileInfo outfile) {
        string jfiles = JsonSerializer.Serialize(files);
        File.WriteAllTextAsync(outfile.ToString(), jfiles);
    }
    public static void SaveJSON(Dictionary<string, List<string>> files, FileInfo outfile) {
        string jfiles = JsonSerializer.Serialize(files);
        File.WriteAllTextAsync(outfile.ToString(), jfiles);
    }
    public static void SaveJSON(Dictionary<string, Dictionary<string, int>> files, FileInfo outfile) {
        string jfiles = JsonSerializer.Serialize(files);
        File.WriteAllTextAsync(outfile.ToString(), jfiles);
    }

    public static HashSet<string> LoadHSJSON(FileInfo file) {
        if (!file.Exists) {
            SaveJSON(new HashSet<string>(), CACHE_FILE);
        }
        return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(file.ToString()));
    }
    public static Dictionary<string, List<string>> LoadDictJSON(FileInfo file) {
        if (!file.Exists) {
            SaveJSON(new Dictionary<string, List<string>>(), file);
        }

        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(file.ToString()));
    }
    public static Dictionary<string, Dictionary<string, int>> LoadNDictJSON(FileInfo file) {
        if (!file.Exists) {
            SaveJSON(new Dictionary<string, Dictionary<string,int>>(), file);
        }

        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(File.ReadAllText(file.ToString()));
    }

    public static List<string> GetWC(string word) {
        /* Getter for WC */
        // TODO implement meme difflib algo
        return WC[word];
    }

    public static int GetFileWC(string filename, string word) {
        return RANKS[filename][word];
    }

    public static string GetHighlight(string filename, string[] words) {
        // 1. Match line containing word with regex
        // 2. repeat for each word
        // 3. Profit
        Console.WriteLine("Fetching highlight for " + filename);
        string text = File.ReadAllText(filename);
        StringBuilder highlight = new StringBuilder("");
        foreach(string word in words) {
            Console.WriteLine(word);
            highlight.Append(Regex.Match(text, @".*" + word + @".*\n").Value + "\n");
        }
        Console.WriteLine(highlight.ToString());

        return highlight.ToString();
    }
}


public static class Moogle
{
    public static SearchResult Query(string query) {
        // reload caches
        FileTools ft = new FileTools();
        // we don't want duplicates
        Dictionary<string, int> candidates = new Dictionary<string, int>();

        // partition the terms of the query
        foreach(string word in query.Split(" ")) {
            if (word.Length < 3) {
                // useless
                continue;
            }
            foreach (string candidate in FileTools.GetWC(word)) {
                Console.WriteLine(candidate);
                if (!candidates.ContainsKey(candidate)) {
                    candidates[candidate] = 0;
                }
                // increase score to sort them later
                candidates[candidate] += FileTools.GetFileWC(candidate, word);
            };
            
        }


        SearchItem[] items = new SearchItem[candidates.Count];

        int count = 0;
        foreach(KeyValuePair<string, int> file in candidates) {
            // where Key is the filename and Value the score
            items[count++] = new SearchItem(file.Key, FileTools.GetHighlight(file.Key, query.Split()), file.Value);
        }

        return new SearchResult(items, query);
    }
}
