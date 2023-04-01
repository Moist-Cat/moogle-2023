namespace MoogleEngine;

public class SearchResult
{
    private void SortItems() {
        SearchItem temp;
        for(int i = 0; i < this.items.Length; i++) {
            Console.WriteLine("{0} {1}", this.items[i].Score, this.items[i].Title);
            for (int j = 0; j < this.items.Length - i - 1; j++) {
                if (this.items[j].Score < this.items[j+1].Score) {
                    temp = this.items[j+1];
                    this.items[j+1] = this.items[j];
                    this.items[j] = temp;
                }
            }
        }
    }
    private SearchItem[] items;

    public SearchResult(SearchItem[] items, string suggestion="")
    {
        if (items == null) {
            throw new ArgumentNullException("items");
        }

        this.items = items;
        this.SortItems();
        this.Suggestion = suggestion;
    }

    public SearchResult() : this(new SearchItem[0]) {

    }

    public string Suggestion { get; private set; }

    public IEnumerable<SearchItem> Items() {
        return this.items;
    }

    public int Count { get { return this.items.Length; } }
}
