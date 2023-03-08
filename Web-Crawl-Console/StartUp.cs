using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Web_Crawl_Console;

public class StartUp
{
    private Uri UserUri { get; set; }
    public SiteCrawler siteCrawler { get; set; }
    public void StartSiteCrawl()
    {
        Console.WriteLine("\r\nPlease enter the site address in the format: \"https://example.com\"");

        var userEnterense = Console.ReadLine();

        while (!Uri.TryCreate(userEnterense, UriKind.Absolute, out Uri uriResult))
        {
            Console.WriteLine("\r\nIncorrect format, try again!");
            userEnterense = Console.ReadLine();
        }

        Console.WriteLine($"\r\nStart Crawling {userEnterense}");

        UserUri = new Uri(userEnterense);

        siteCrawler = new SiteCrawler();

        siteCrawler.StartCrawl(UserUri).Wait();

        if (siteCrawler.StartUrlValidation)
        {
            PrintResult();
        }
        else
        {
            Console.WriteLine("\r\nThe website is unavailable, doesn't exist, or \"Houston, we have internet problems!\"");
        }

        GetContinuation();

    }

    private void PrintResult()
    {
        PrintInSitemapButNotCrawled();
        PrintCrawledButNotInSitemap();
        PrintDisallowedFromRobotRules();
        PrintBrokenLinks();
        PrintCrawlResultWithTimings();

    }

    private void PrintInSitemapButNotCrawled()
    {
        List<string> pages = new List<string>();
        foreach (var site in siteCrawler.SitemapCrawler.UrlsFromSitemap)
        {
            if (!siteCrawler.Result.Exists(x => x.Key == site))
            {
                pages.Add(site);
            }
        }
        if (pages.Count > 0)
        {
            Console.WriteLine($"\r\nPrint URLs existing in sitemap.xml but not found on crawl? ({pages.Count} entities)");
            YesNoPrintChecer(pages, null);

        }
        else if (pages.Count == 0)
        {
            Console.WriteLine($"\r\nCan't print URLs existing in sitemap.xml but not found on crawl because links count is 0!");
        }
    }

    private void PrintCrawledButNotInSitemap()
    {
        List<string> pages = new List<string>();
        foreach (var site in siteCrawler.Result)
        {
            if (!siteCrawler.SitemapCrawler.UrlsFromSitemap.Contains(site.Key))
            {
                pages.Add(site.Key);
            }
        }
        if (pages.Count > 0)
        {
            Console.WriteLine($"\r\nPrint URLs founded by crawling the website but not in sitemap.xml?({pages.Count} entities)");
            YesNoPrintChecer(pages, null);

        }
        else if (pages.Count == 0)
        {
            Console.WriteLine($"\r\nCan't print URLs founded by crawling the website but not in sitemap.xml because links count is 0!");
        }

    }

    private void PrintCrawlResultWithTimings()
    {
        List<KeyValuePair<string, long>> pages = new List<KeyValuePair<string, long>>();
        if (siteCrawler.Result.Count > 0)
        {
            pages = siteCrawler.Result;

            Console.WriteLine($"\r\nURLs found after crawling the {UserUri.Host} : {siteCrawler.Result.Count}" +
            $"\r\nURLs found in sitemap: {siteCrawler.SitemapCrawler.UrlsFromSitemap.Count}\r\n");

            Console.WriteLine($"\r\nPrint all URLs founded by crawling with timing? ({pages.Count} entities)");

            YesNoPrintChecer(null, pages);

        }
        else if (siteCrawler.Result.Count == 0)
        {
            Console.WriteLine($"\r\nCan't print URLs founded by crawling with timing because links count is 0!");
        }

    }

    private void PrintBrokenLinks()
    {
        List<string> pages = new List<string>();
        foreach (var site in siteCrawler.BrokenLinks)
        {
            pages.Add(site.ToString());
        }
        if (pages.Count > 0)
        {
            Console.WriteLine($"\nPrint broken URLs founded by crawling the website? ({pages.Count} entities)");
            YesNoPrintChecer(pages, null);
        }
        else if (pages.Count == 0)
        {
            Console.WriteLine($"\r\nCan't print broken URLs founded by crawling the website because links count is 0!");
        }
    }

    private void PrintDisallowedFromRobotRules()
    {
        List<string> pages = new List<string>();
        foreach (var site in siteCrawler.DisallowedFromRobotRules)
        {
            pages.Add(site.ToString());

        }
        if (pages.Count > 0)
        {
            Console.WriteLine($"\nPrint disallowed by robots.txt rules URLs founded by crawling the website? ({pages.Count} entities)");
            YesNoPrintChecer(pages, null);
        }
        else if (pages.Count == 0 | !siteCrawler.SitemapCrawler.RobotsTxtFound)
        {
            Console.WriteLine($"\r\nCan't print disallowed by robots.txt rules URLs founded by crawling the website because links count is 0 or robots.txt not found!");
        }
    }

    private void YesNoPrintChecer(List<string>? pages, List<KeyValuePair<string, long>>? results)
    {
        var key = YesNoCheker();
        if (key.Key == ConsoleKey.Y)
        {
            if (pages != null)
            {
                foreach (var page in pages)
                {
                    Console.WriteLine(page);
                }

            }
            else
            {
                Console.WriteLine("\r\nUrl : Timing (ms)");
                foreach (var site in results)
                {
                    Console.WriteLine(site.Key + " : " + site.Value);
                }
            }

        }

    }

    private ConsoleKeyInfo YesNoCheker()
    {
        Console.WriteLine("Y/N");
        var ans = Console.ReadKey();
        while (ans.Key != ConsoleKey.Y && ans.Key != ConsoleKey.N)
        {
            Console.WriteLine("\r\nWrong key! Try again");
            ans = Console.ReadKey();
        }
        Console.WriteLine("\r\n");
        return ans;

    }

    private void GetContinuation()
    {
        Console.WriteLine("\r\nDo you want to continue?");
        var key = YesNoCheker();
        if (key.Key == ConsoleKey.Y)
        {
            StartSiteCrawl();
        }
    }
}
