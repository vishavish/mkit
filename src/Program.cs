using System.Text.RegularExpressions;
using HtmlAgilityPack;
using PuppeteerSharp;

HttpClient http = new();

if (args.Length > 0)
{
	var results = await GetSearchResults(args[0]);

	foreach (var manga in results)
	{
		Console.WriteLine("Manga Information: ");
		Console.WriteLine($"Name: { manga.Name }");
		Console.WriteLine($"Url: { manga.Url }");
		Console.WriteLine($"Chapter(s): { manga.Chapters }");
		Console.WriteLine();
	}
	
	Console.WriteLine("Enter URL: ");
	var url = Console.ReadLine();
	Console.Clear();
	var chapterList = await GetChapterList(url!);
	foreach(var chapterInfo in chapterList)
	{
		Console.WriteLine(chapterInfo.Id);
	}
	
	Console.WriteLine("NOTE: You can include different chapters using [,] or a range using [-].");
	Console.WriteLine("Example: \n[1,5,10] this will download chapters 1, 5, and 10.");
	Console.WriteLine("[1-10] this will download chapters 1 to 10.");
	Console.Write("Enter chapters: ");
	var chapterUrl = Console.ReadLine();
	var input = ParseInput(chapterUrl!);
	
	Console.WriteLine(input.mode);
	Console.WriteLine(input.collection);
	
	// await DownloadChapters(chapterUrl!);
	
	Console.ReadLine();
}


/* *************************** */
/*          FUNCTIONS          */
/*                             */
/* *************************** */

(Mode mode, int[] collection) ParseInput(string input)
{
	if (input.IndexOf(',') >= 0)
	{
		var x = input.Split(',');
		return (Mode.Selection, x.Select(int.Parse).ToArray());
	}
	else
	{
		var x = input.Split('-');
		return (Mode.Selection, x.Select(int.Parse).ToArray());
	}

}

async Task<List<ChapterInfo>> GetChapterList(string url)
{
	List<ChapterInfo> chapterList = new();
	Console.WriteLine("Please wait while we connect and retrieve data from the site...");

	//https://mangakatana.com/manga/the-return-of-the-crazy-demon.25882
 
	await new BrowserFetcher().DownloadAsync();
	using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
	using var page = await browser.NewPageAsync();
	await page.GoToAsync(url); 
	string content = await page.GetContentAsync();
	var htmlDoc = new HtmlDocument();
	htmlDoc.LoadHtml(content);
	
	var nodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='chapters']//div[@class='chapter']");
	
	foreach (var node in nodes!)
	{
		string name = node.SelectSingleNode("a")!.InnerText;
		string mangaUrl = node.SelectSingleNode("a")!.GetAttributeValue("href", "N/A");
		
		// TODO:: Validate here; but okay for now
		int.TryParse(Regex.Replace(name, @"\D", ""), out int res);
		
		chapterList.Add(new ChapterInfo(res, mangaUrl));
	}

	return chapterList;
}

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


async Task DownloadChapters(string url)
{
	await new BrowserFetcher().DownloadAsync();
	using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
	using var page = await browser.NewPageAsync();
	await page.GoToAsync(url); 
	
	string htmlResult = await page.GetContentAsync();
	
	var htmlDoc = new HtmlDocument();
	htmlDoc.LoadHtml(htmlResult);
	
	var nodes = htmlDoc.DocumentNode.SelectNodes("//div[@id='imgs']//div[contains(@class, 'wrap_img')]");
		
	foreach (var node in nodes!)
	{
		 // string name = node.SelectSingleNode("a")!.InnerText;
		 // string chapters = node.SelectSingleNode("span")!.InnerText.Replace('-', ' ').Trim();
		 // string mangaUrl = node.SelectSingleNode("img")!.GetAttributeValue("src", "N/A");
		
		 Console.WriteLine(node.SelectSingleNode("img")!.GetAttributeValue("data-src", "No"));
	}
			
	// DOWNLOAD IMAGE	

	// var res = await http.GetAsync(url);
	// byte[] bytes = await res.Content.ReadAsByteArrayAsync();
	// await File.WriteAllBytesAsync(Path.Combine(Environment.CurrentDirectory, "image.jpg"), bytes);
}

/* *************************** */
/*   CLASSES, RECORDS, ENUMS   */
/*                             */
/* *************************** */

record Manga(string Name, string Url, string Chapters);

record ChapterInfo(int Id, string Url);

enum Mode
{
	Selection,
	Range
}
