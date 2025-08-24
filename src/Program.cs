using HtmlAgilityPack;
using PuppeteerSharp;
using Spectre.Console;
using System.Linq;


HttpClient http = new();
List<Manga> results = new();
List<ChapterInfo> chapterList = new();

AnsiConsole.Write(new FigletText("Welcome to mkit!").Centered().Color(Color.SkyBlue1));

var name = AnsiConsole.Prompt(new TextPrompt<string>("Enter manga name: "));
var rule = new Rule().RuleStyle(Style.Parse("blue"));
AnsiConsole.Write(rule);

await AnsiConsole.Status()
	.Spinner(Spinner.Known.Dots)
	.SpinnerStyle(Style.Parse("green dim"))
	.StartAsync("Connecting to server...", async ctx => {
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

AnsiConsole.Write(new Panel(new Text(selected).Centered())
	.Expand()
	.SquareBorder()
);

// if (results is not null)
// {
	
// 	AnsiConsole.Markup("[bold yellow]User PATH Entries:[/]");
// 	var selected = AnsiConsole.Prompt(
// 	     new SelectionPrompt<string>()
// 	        .Title("[green]<enter>[/] to accept)")
// 			.EnableSearch()
// 	        .AddChoices(results.Select(x => x.Name))
// 	);

// 	AnsiConsole.Write(new Panel(new Text("Sample").Centered())
// 		.Expand()
// 		.SquareBorder()
// 	);


// 	await AnsiConsole.Status()
// 		.Spinner(Spinner.Known.Dots)
// 		.SpinnerStyle(Style.Parse("green dim"))
// 		.StartAsync("Please wait while we connect and retrieve data from the site...", async ctx => {
// 			chapterList = await GetChapterList(GetUrl(selected));
// 		});

// 	AnsiConsole.Write("Select Chapters: ");
// 	AnsiConsole.Prompt(
// 		new MultiSelectionPrompt<string>()
// 		// .Title("")
// 		.PageSize(10)
// 		.NotRequired()
// 	    .InstructionsText(
// 	        "[grey](Press [blue]<space>[/] to select a chapter, " +
// 	        "[green]<enter>[/] to accept)[/]")
// 		.AddChoiceGroup<string>("Chapters", chapterList.Select(c => c.ChapterName))
// 	);
// }

// foreach (var x in z)
// 	await DownloadChapters(x);

// Console.ReadLine();


/* *************************** */
/*          FUNCTIONS          */
/*                             */
/* *************************** */

string GetUrl(string selected) => results.Find(r => r.Name == selected)!.Url;

async Task<List<Manga>> GetSearchResults(string searchTerm)
{
	List<Manga> mangaList = new();
	string url = $"https://mangakatana.com/?search={ searchTerm }&search_by=book_name";
	
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
	foreach (var node in nodes!)
	{
		altName = node!.SelectSingleNode("//div[@class='alt_name']")!.InnerText;
		authors = node!.SelectSingleNode("//div[contains(@class,'authors')]")!.InnerText;
		newChap = node!.SelectSingleNode("//div[contains(@class,'new_chap')]")!.InnerText;
		status = node!.SelectSingleNode("//div[contains(@class,'status')]")!.InnerText;
		lastUpdate = node!.SelectSingleNode("//div[contains(@class,'updateAt')]")!.InnerText;
	}

	return new MangaInfo(altName, authors, status, newChap, lastUpdate);
}

async Task<List<ChapterInfo>> GetChapterList(string url)
{
	List<ChapterInfo> chapterList = new();
	//https://mangakatana.com/manga/the-return-of-the-crazy-demon.25882
 
	await new BrowserFetcher().DownloadAsync();
	using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
	using var page = await browser.NewPageAsync();
	await page.GoToAsync(url); 
	string content = await page.GetContentAsync();
	var htmlDoc = new HtmlDocument();
	htmlDoc.LoadHtml(content);
	
	var nodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='chapters']//div[@class='chapter']");
	int i = 1;
	foreach (var node in nodes!)
	{
		string name = node.SelectSingleNode("a")!.InnerText;
		string mangaUrl = node.SelectSingleNode("a")!.GetAttributeValue("href", "N/A");

		chapterList.Add(new ChapterInfo(i, name, mangaUrl));
		i++;
	}

	return chapterList;
}

async Task DownloadChapters(ChapterInfo chapter)
{
	await new BrowserFetcher().DownloadAsync();
	using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
	using var page = await browser.NewPageAsync();
	await page.GoToAsync(chapter.Url); 
	
	string htmlResult = await page.GetContentAsync();
	
	var htmlDoc = new HtmlDocument();
	htmlDoc.LoadHtml(htmlResult);
	
	var nodes = htmlDoc.DocumentNode.SelectNodes("//div[@id='imgs']//div[contains(@class, 'wrap_img')]");
		
		
	for (int i = 0; i < nodes?.Count; i++)	
	{
		var imgUrl = nodes[i].SelectSingleNode("img")!.GetAttributeValue("data-src", "No");
		
		var res = await http.GetAsync(imgUrl);
		string fileName = i + ".jpg";
		byte[] bytes = await res.Content.ReadAsByteArrayAsync();
		var path = Path.Combine(Environment.CurrentDirectory, "chapters_" + chapter.Id);
		if (!Directory.Exists(path)) Directory.CreateDirectory(path);
		await File.WriteAllBytesAsync(Path.Combine(path, fileName), bytes);
	}
}

/* *************************** */
/*   CLASSES, RECORDS, ENUMS   */
/*                             */
/* *************************** */

record Manga(string Name, string Url, string Chapters);
record MangaInfo(string AltName, string Authors, string Status, string LatestChap, string LastUpdated);
record ChapterInfo(int Id, string ChapterName, string Url);

