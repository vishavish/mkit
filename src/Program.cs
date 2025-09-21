using HtmlAgilityPack;
using PuppeteerSharp;
using Spectre.Console;


Console.OutputEncoding = System.Text.Encoding.UTF8;

/* DECLARATIONS */
HttpClient http = new();
List<Manga> results = new();
List<ChapterInfo> chapterList = new();
await new BrowserFetcher().DownloadAsync();
using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });


AnsiConsole.Write(new FigletText("Welcome to mkit!").Centered().Color(Color.SkyBlue1));
var name = AnsiConsole.Prompt(new TextPrompt<string>("Enter manga name: "));
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .SpinnerStyle(Style.Parse("green dim"))
    .StartAsync("Searching for results...", async ctx =>
    {
        results = await GetSearchResults(name.Trim());
    });

AnsiConsole.Markup("[bold yellow]Search Results:[/]");
var selected = AnsiConsole.Prompt(
     new SelectionPrompt<string>()
        .Title("[green]<enter>[/] to accept)")
        .EnableSearch()
        .AddChoices(results.Select(x => x.Name))
);

var mangaInfo = await GetMangaInfo(GetUrl(selected));
AnsiConsole.Write(new Panel(new Text(selected.ToUpperInvariant(),
                            new Style(Color.Red)).Centered())
    .Expand()
    .SquareBorder()
    .BorderColor(Color.SkyBlue1)
);


/* LAYOUT */
var layout = new Layout("Root")
    .SplitColumns(
        new Layout("Left"),
        new Layout("Right")
    );

Markup status = mangaInfo!.Status switch
{

    "Completed" => new Markup($"Status         : :green_circle: {mangaInfo!.Status}"),
    "Ongoing"   => new Markup($"Status         : :blue_circle: {mangaInfo!.Status}"),
    _           => new Markup($"Status         : :red_circle: {mangaInfo!.Status}")
};

layout["right"].Update(
    new Panel(
        new Rows(
            new Text("INFORMATION"),
            new Text($"Alt Name       : {mangaInfo!.AltName}"),
            new Text($"Authors        : {mangaInfo!.Authors}"),
            status,
            new Text($"Latest Chapter : {mangaInfo!.LatestChap}"),
            new Text($"Last Updated   : {mangaInfo!.LastUpdated}")
        )
    ).BorderColor(Color.SkyBlue1)
);

layout["left"].Update(
    new Panel(
        new Rows(
            new Text("SUMMARY"),
            new Text($"{mangaInfo.Description}")
        )
    ).BorderColor(Color.SkyBlue1)
);

AnsiConsole.Write(layout);
var action = AnsiConsole.Prompt(
    new SelectionPrompt<Action>()
        .Title("[green]ENTER to accept:[/]")
        .AddChoices(Action.Continue, Action.Back)
);

switch (action)
{
    case Action.Continue:
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green dim"))
            .StartAsync("Retrieving data from the site...", async ctx =>
            {
                chapterList = await GetChapterList(GetUrl(selected));
            });

        var chapterAll = new ChapterInfo("Select All", "");
        AnsiConsole.Write("Select Chapters: ");
        var selections = AnsiConsole.Prompt(
            new MultiSelectionPrompt<ChapterInfo>()
            .PageSize(10)
            .NotRequired()
            .InstructionsText(
                "[grey](Press [blue]<space>[/] to select a chapter, " +
                "[green]<enter>[/] to accept)[/]")
            .UseConverter(c => c.ChapterName)
            .AddChoiceGroup(chapterAll, chapterList)
        );

        if (selections.Count > 0) { await Task.WhenAll(selections.Select(async c => await DownloadChapters(c))); }

        break;
    case Action.Back:
        break;
}

Console.ReadLine();


/* *************************** */
/*          METHODS            */
/*                             */
/* *************************** */

string GetUrl(string selected) => results.Find(r => r.Name == selected)!.Url;

async Task<List<Manga>> GetSearchResults(string searchTerm)
{
    List<Manga> mangaList = new();
    string url = $"https://mangakatana.com/?search={searchTerm}&search_by=book_name";

    var res = await http.GetAsync(url);
    if (!res.IsSuccessStatusCode)
    {
        Console.WriteLine("Failed to search for manga");
        return new();
    }

    string htmlResult = await res.Content.ReadAsStringAsync();
    var htmlDoc = new HtmlDocument();
    htmlDoc.LoadHtml(htmlResult);

    var nodes = htmlDoc.DocumentNode.SelectNodes("//div[@id='book_list']//div[@class='item']//h3");
    if (nodes is null) { return new(); }

    foreach (var node in nodes!)
    {
        string name = node.SelectSingleNode("a")!.InnerText;
        string chapters = node.SelectSingleNode("span")!.InnerText.Replace('-', ' ').Trim();
        string mangaUrl = node.SelectSingleNode("a")!.GetAttributeValue("href", "N/A");

        mangaList.Add(new Manga(name, mangaUrl, chapters));
    }

    return mangaList;
}

async Task<MangaInfo?> GetMangaInfo(string url)
{
    string altName = "";
    string authors = "";
    string desc = "";
    string newChap = "";
    string status = "";
    string lastUpdate = "";

    var res = await http.GetAsync(url);
    if (!res.IsSuccessStatusCode)
    {
        Console.WriteLine("Failed to search for manga");
        return null;
    }

    string htmlResult = await res.Content.ReadAsStringAsync();
    var htmlDoc = new HtmlDocument();
    htmlDoc.LoadHtml(htmlResult);

    var nodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='info']//ul[contains(@class, 'meta')]");
    if (nodes is null) { return new(altName, authors, desc, status, newChap, lastUpdate); }

    foreach (var node in nodes)
    {
        altName = node.SelectSingleNode("//div[@class='alt_name']")?.InnerText ?? "";
        authors = node.SelectSingleNode("//div[contains(@class,'authors')]")?.InnerText ?? "";
        newChap = node.SelectSingleNode("//div[contains(@class,'new_chap')]")?.InnerText ?? "";
        status = node.SelectSingleNode("//div[contains(@class,'status')]")?.InnerText ?? "";
        lastUpdate = node.SelectSingleNode("//div[contains(@class,'updateAt')]")?.InnerText ?? "";
    }
    nodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='summary']");
    if (nodes!.Count > 0) { desc = nodes![0].SelectSingleNode("p")!.InnerText ?? ""; }

    return new(altName, authors, desc, status, newChap, lastUpdate);
}

async Task<List<ChapterInfo>> GetChapterList(string url)
{
    List<ChapterInfo> chapterList = new();

    using var page = await browser.NewPageAsync();
    await page.GoToAsync(url);

    string content = await page.GetContentAsync();
    var htmlDoc = new HtmlDocument();
    htmlDoc.LoadHtml(content);

    var nodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='chapters']//div[@class='chapter']");
    if (nodes is null) { return chapterList; }

    foreach (var node in nodes!)
    {
        string name = node.SelectSingleNode("a")?.InnerText ?? "";
        string mangaUrl = node.SelectSingleNode("a")?.GetAttributeValue("href", "N/A") ?? "";

        chapterList.Add(new ChapterInfo(name, mangaUrl));
    }

    return chapterList;
}

async Task<bool> DownloadChapters(ChapterInfo chapter)
{
    using var page = await browser.NewPageAsync();
    await page.GoToAsync(chapter.Url);

    string htmlResult = await page.GetContentAsync();
    var htmlDoc = new HtmlDocument();
    htmlDoc.LoadHtml(htmlResult);

    var nodes = htmlDoc.DocumentNode.SelectNodes("//div[@id='imgs']//div[contains(@class, 'wrap_img')]");
    if (nodes is null) { return false; }

    for (int i = 0; i < nodes?.Count; i++)
    {
        var imgUrl = nodes[i].SelectSingleNode("img")!.GetAttributeValue("data-src", "");
        if (string.IsNullOrEmpty(imgUrl)) { return false; }

        var res = await http.GetAsync(imgUrl);
        string fileName = i + ".jpg";
        byte[] bytes = await res.Content.ReadAsByteArrayAsync();
        var path = Path.Combine(Environment.CurrentDirectory, chapter.ChapterName);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        await File.WriteAllBytesAsync(Path.Combine(path, fileName), bytes);
    }

    return true;
}

/* *************************** */
/*   CLASSES, RECORDS, ENUMS   */
/*                             */
/* *************************** */

record Manga(string Name, string Url, string Chapters);
record MangaInfo(string AltName, string Authors, string Description, string Status, string LatestChap, string LastUpdated);
record ChapterInfo(string ChapterName, string Url);

enum Action
{
    Continue,
    Back
}
