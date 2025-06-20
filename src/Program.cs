using HtmlAgilityPack;
using PuppeteerSharp;

HttpClient http = new();

var x = await GetSearchResults("crazy demon");

foreach (var manga in x)
{
	Console.WriteLine("Manga Information: ");
	Console.WriteLine($"Name: { manga.Name }");
	Console.WriteLine($"Url: { manga.Url }");
	Console.WriteLine($"Chapter(s): { manga.Chapters }");
	Console.WriteLine();
}

// await GetChapterList("https://mangakatana.com/manga/the-return-of-the-crazy-demon.25882");
// await DownloadChapters();

async Task GetChapterList(string url)
{
	Console.WriteLine("Please while we connect and retrieve data from the site...");

	// https://mangakatana.com/manga/the-return-of-the-crazy-demon.25882
 
	await new BrowserFetcher().DownloadAsync();
	using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
	using var page = await browser.NewPageAsync();
	await page.GoToAsync(url); 
	
	string content = await page.GetContentAsync();
	
	var htmlDoc = new HtmlDocument();
	htmlDoc.LoadHtml(content);
	
	var nodes = htmlDoc.DocumentNode
		.SelectNodes("//div[@class='chapters']//div[@class='chapter']");
	
	Console.WriteLine(nodes!.Count);
	
	foreach (var node in nodes!)
	{
		string name = node.SelectSingleNode("a")!.InnerText;
		// string chapters = node.SelectSingleNode("span")!.InnerText.Replace('-', ' ').Trim();
		string mangaUrl = node.SelectSingleNode("a")!.GetAttributeValue("href", "N/A");
	
		Console.WriteLine(name);
		Console.WriteLine(mangaUrl);
	}
	
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
	
	var nodes = htmlDoc.DocumentNode
		.SelectNodes("//div[@id='book_list']//div[@class='item']//h3");
		
	foreach (var node in nodes!)
	{
		string name = node.SelectSingleNode("a")!.InnerText;
		string chapters = node.SelectSingleNode("span")!.InnerText.Replace('-', ' ').Trim();
		string mangaUrl = node.SelectSingleNode("a")!.GetAttributeValue("href", "N/A");
	
		mangaList.Add(new Manga(name, mangaUrl, chapters));		
	}
	
	return mangaList;
}


async Task DownloadChapters()
{
	HttpClient http = new HttpClient();

	var res = await http.GetAsync("https://i6.mangakatana.com/token/db4dd62c30106067941pp%3At%3A821.r16p2p-8c1p0w%3Ar%3A1p1%3A9q1q8851260/0.jpg");
	byte[] bytes = await res.Content.ReadAsByteArrayAsync();
	await File.WriteAllBytesAsync(Path.Combine(Environment.CurrentDirectory, "image.jpg"), bytes);
}

record Manga(string Name, string Url, string Chapters);
