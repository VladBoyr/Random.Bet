using Microsoft.Playwright;
using System.Globalization;

internal class Program
{
    private static int defaultTimeout = 120000;
    private static ILocator scoreLocator = null!;
    private static ILocator koefLocator = null!;
    private static ILocator team1btn1 = null!;
    private static ILocator team1btn2 = null!;
    private static ILocator team2btn1 = null!;
    private static ILocator team2btn2 = null!;
    private static IPage page = null!;

    private static async Task Main(string[] args)
    {
        const string matchList = "https://www.fon.bet/sports/football/101631";

        const string matchLinksPath = $"xpath=/html/body/application/" +
                                      $"div[2]/div[1]/div[1]/div/div/" +
                                      $"div[2]/div/div/div[1]/div/div/div/" +
                                      $"div[2]/div/div[1]/div/div[1]/div/div[2]/" +
                                      $"div/div/div[1]/div[1]/div[1]/a";

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        page = await browser.NewPageAsync();
        await page.GotoAsync(matchList, new() { Timeout = defaultTimeout });

        var matchLinks = new List<string>();
        var elements = await page.Locator(matchLinksPath).ElementHandlesAsync();
        foreach (var elementTask in elements)
        {
            var textLink = await elementTask.InnerTextAsync();
            if (textLink.Contains("Home") || textLink.Contains("Away") || 
                textLink.Contains("Хозяева") || textLink.Contains("Гости"))
                continue;

            var matchLink = await elementTask.GetAttributeAsync("href");
            matchLinks.Add("https://www.fon.bet" + matchLink!);
        }

        foreach (var matchLink in matchLinks)
            await GetMatch(matchLink);
    }

    private static async Task GetMatch(string matchLink)
    {
        const string templateBtnPath = $"xpath=/html/body/application/" +
                                       $"div[2]/div[1]/div[1]/div/div/" +
                                       $"div[2]/div/div/div[1]/div/div/div/div/div/div[1]/div/div[1]/div[3]/div[8]/" +
                                       $"div[2]/div/div/div[2]/div/div/";

        const string team1btn1Path = templateBtnPath + $"div[1]/div[2]/span[1]";
        const string team1btn2Path = templateBtnPath + $"div[1]/div[2]/span[2]";
        const string team2btn1Path = templateBtnPath + $"div[3]/div[2]/span[1]";
        const string team2btn2Path = templateBtnPath + $"div[3]/div[2]/span[2]";

        const string team1Path = templateBtnPath + $"div[1]/div[1]";
        const string team2Path = templateBtnPath + $"div[3]/div[1]";

        const string scorePath = templateBtnPath + $"div[2]/div[1]";
        const string koefPath = templateBtnPath + $"div[2]/div[2]/div/div/div";

        await page.GotoAsync(matchLink, new() { Timeout = defaultTimeout });
        scoreLocator = page.Locator(scorePath);
        koefLocator = page.Locator(koefPath);
        team1btn1 = page.Locator(team1btn1Path);
        team1btn2 = page.Locator(team1btn2Path);
        team2btn1 = page.Locator(team2btn1Path);
        team2btn2 = page.Locator(team2btn2Path);

        var matchKoefs = new Dictionary<(int, int), decimal>();
        for (var i = 0; i <= 10; i++)
        {
            for (var j = 0; j <= 10; j++)
            {
                if (!await SetMatchScore((i, j)))
                    break;

                var koef = await GetMatchKoef();
                if (koef < 0)
                    break;

                matchKoefs.Add(await GetMatchScore(), koef);
            }
        }

        var randomScore = GetScoreByMinKoef(matchKoefs);
        // var randomScore = GetRandomScore(matchKoefs);
        Console.WriteLine($"{await page.Locator(team1Path).InnerTextAsync(new() { Timeout = defaultTimeout })} - " +
                          $"{await page.Locator(team2Path).InnerTextAsync(new() { Timeout = defaultTimeout })} , " +
                          $"{randomScore.Item1.Item1}:{randomScore.Item1.Item2} , " +
                          $"{randomScore.Item2}");
    }

    private static async Task<(int, int)> GetMatchScore()
    {
        var arr = (await scoreLocator.InnerTextAsync(new() { Timeout = defaultTimeout })).Split("–");
        return (int.Parse(arr[0].Trim()), int.Parse(arr[1].Trim()));
    }

    private static async Task<decimal> GetMatchKoef()
    {
        if (decimal.TryParse(await koefLocator.InnerTextAsync(new() { Timeout = defaultTimeout }), CultureInfo.InvariantCulture, out decimal result))
            return result;
        return -1;
    }

    private static async Task<bool> SetMatchScore((int, int) newScore)
    {
        var team1 = await SetTeam1Score(newScore.Item1);
        var team2 = await SetTeam2Score(newScore.Item2);
        return team1 && team2;
    }

    private static async Task<bool> SetTeam1Score(int newScore)
    {
        var (currentScore, _) = await GetMatchScore();

        while (currentScore != newScore)
        {
            if (currentScore > newScore)
            {
                if (!await ButtonEnabled(team1btn1))
                    return false;

                await team1btn1.ClickAsync(new() { Timeout = defaultTimeout });
            }
            else
            {
                if (!await ButtonEnabled(team1btn2))
                    return false;

                await team1btn2.ClickAsync(new() { Timeout = defaultTimeout });
            }

            (currentScore, _) = await GetMatchScore();
        }

        return true;
    }

    private static async Task<bool> SetTeam2Score(int newScore)
    {
        var (_, currentScore) = await GetMatchScore();

        while (currentScore != newScore)
        {
            if (currentScore > newScore)
            {
                if (!await ButtonEnabled(team2btn1))
                    return false;

                await team2btn1.ClickAsync(new() { Timeout = defaultTimeout });
            }
            else
            {
                if (!await ButtonEnabled(team2btn2))
                    return false;

                await team2btn2.ClickAsync(new() { Timeout = defaultTimeout });
            }

            (_, currentScore) = await GetMatchScore();
        }

        return true;
    }

    private static async Task<bool> ButtonEnabled(ILocator btn)
    {
        return (await btn.GetAttributeAsync("class"))!.Contains("enabled");
    }

    private static ((int, int), decimal) GetScoreByMinKoef(Dictionary<(int, int), decimal> koefs)
    {
        var minKoef = koefs.Min(x => x.Value);
        var minKoefs = koefs.Where(x => x.Value == minKoef).ToArray();
        var index = new Random().Next(minKoefs.Length);
        return (minKoefs[index].Key, minKoefs[index].Value);
    }

    private static ((int, int), decimal) GetRandomScore(Dictionary<(int, int), decimal> koefs)
    {
        while (true)
        {
            var newKoefs = new Dictionary<(int, int), decimal>();
            foreach (var (score, koef) in koefs)
            {
                if (CheckKoef(koef))
                    newKoefs.Add(score, koef);
            }

            if (newKoefs.Count == 1)
                return (newKoefs.Keys.First(), newKoefs.Values.First());
        }
    }

    private static bool CheckKoef(decimal koef)
    {
        return (new Random().Next((int)(koef * 100)) < 100);
    }
}