using Louw.SitemapParser;
using RobotsSharpParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TurnerSoftware.RobotsExclusionTools;

namespace Web_Crawl_Console;

public class SitemapChecker
{

    public bool RobotsTxtFound = false;
    private bool KeepProcessing = true;
    private int UrlsFromSitemapCountPreset = 40000;
    private bool FirstTryFail = false;
    private bool SitemapWithoutRobots = false;
    private Uri startUrl { get; set; }
    private SitemapLoader sitemapLoader { get; set; }
    public RobotsFile RobotsFile { get; set; }

    public bool ValidSitemapXmlFound { get; set; } = false;

    public List<string> UrlsFromSitemapPool { get; set; } = new();
    public List<string> UrlsFromSitemap { get; set; } = new();

    public SitemapChecker()
    {

    }

    public async Task GetValidUrlsFromRobot(Uri url)
    {
        try
        {
            startUrl = url;
            await GetRobotsFileAsync(startUrl);
            if (RobotsTxtFound | SitemapWithoutRobots)
            {

                if (!await FirstSitemapMethod(startUrl) && FirstTryFail)
                {
                    if (FirstTryFail == true)
                    {
                        await SecondSitemapMethod(startUrl);

                    }
                    else
                    {
                        Console.WriteLine("\r\nSitemap results will not be taken into account...");
                    }


                }

                if (ValidSitemapXmlFound && KeepProcessing)
                {
                    Console.WriteLine("\r\nSuccess!");
                }
                UrlsFromSitemap = UrlsFromSitemapPool.Distinct().ToList();
                Console.WriteLine($"Current processed unique sitemap URLs is : {UrlsFromSitemap.Count}");

            }
            else
            {
                Console.WriteLine("\r\nSitemap not found! Sitemap.xml will not be taken into account...");
                UrlsFromSitemap = UrlsFromSitemapPool.Distinct().ToList();
                Console.WriteLine($"Current processed unique sitemap URLs is : {UrlsFromSitemap.Count}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\r\nFail with: {ex.Message}");
            Console.WriteLine("\r\nSitemap results will not be taken into account...");
        }

    }


    private async Task GetRobotsFileAsync(Uri url)
    {
        try
        {
            var robotsFileParser = new RobotsFileParser();
            RobotsFile = await robotsFileParser.FromUriAsync(new Uri(url.ToString()));
            if (RobotsFile.SiteAccessEntries.Count > 0)
            {
                RobotsTxtFound = true;
                Console.WriteLine("\r\nRobots.txt found, setting robots rules...");
            }
            else
            {
                Console.WriteLine("\r\nRobots.txt not found or invalid...");

            }

            if (RobotsFile.SitemapEntries.Count == 0)
            {
                SitemapWithoutRobots = true;
                Console.WriteLine("\r\nTrying to find the sitemap by touch...");
                startUrl = new Uri(url, "/sitemap.xml");
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine("\r\nRobots.txt not found or invalid...");
        }

    }

    private async Task<bool> FirstSitemapMethod(Uri url)
    {
        try
        {
            sitemapLoader = new SitemapLoader();
            List<Sitemap> robotSitemaps = new List<Sitemap>();
            if (startUrl.LocalPath.Contains("/sitemap.xml"))
            {

                var sitemap = new Sitemap(startUrl);
                robotSitemaps.Add(sitemap);

                Console.WriteLine("\r\nSitemap.xml found, trying to read...");
                foreach (var siteMap in robotSitemaps)
                {
                    var taskToWait = Task.Run(async () => await GetFromSitemap(siteMap));
                    taskToWait.Wait();
                }
                return true;
            }
            else
            {
                foreach (var sitemapString in RobotsFile.SitemapEntries)
                {
                    var sitemap = new Sitemap(new Uri(sitemapString.Sitemap.ToString()));
                    robotSitemaps.Add(sitemap);
                }

                Console.WriteLine("\r\nSitemap.xml found, trying to read...");
                foreach (var siteMap in robotSitemaps)
                {
                    var taskToWait = Task.Run(async () => await GetFromSitemap(siteMap));
                    taskToWait.Wait();
                }
                return true;
            }


        }
        catch (Exception ex)
        {

            Console.WriteLine($"\r\nFail with: {ex.Message} " + $"\nTrying Again...");
            FirstTryFail = true;
            return false;
        }

    }

    private async Task GetFromSitemap(Sitemap sitemap)
    {
        var loadedSitemap = await sitemapLoader.LoadAsync(sitemap);

        if (loadedSitemap.IsLoaded && KeepProcessing)
        {
            if (loadedSitemap.SitemapType == SitemapType.Index)
            {
                foreach (var siteMap in loadedSitemap.Sitemaps)
                {
                    if (KeepProcessing)
                        await GetFromSitemap(siteMap);
                }

            }
            else if (loadedSitemap.SitemapType == SitemapType.Items)
            {
                UrlsFromSitemapsAdding(loadedSitemap, null);
            }
        }
    }

    private async Task SecondSitemapMethod(Uri url)
    {
        var sitemapGeter = new Robots(url.ToString(), "MyCustom");

        await sitemapGeter.Load();

        var sitemaps = new List<tSitemap>();

        var sitemapUrls = new List<string>();

        var getSitemapTask = Task.Run(async () => await sitemapGeter.GetSitemapIndexes());
        if (getSitemapTask.Wait(TimeSpan.FromSeconds(10)))
            sitemaps = (List<tSitemap>)await getSitemapTask;

        if (sitemaps.Count > 0)
        {
            ValidSitemapXmlFound = true;
            foreach (var sitemap in sitemaps)
            {
                if (KeepProcessing)
                {
                    var urls = await sitemapGeter.GetUrls(sitemap);
                    UrlsFromSitemapsAdding(null, urls);
                }
            }
        }
    }


    private void UrlsFromSitemapsAdding(Sitemap? sitemap, IReadOnlyList<tUrl>? tUrls)
    {
        if (sitemap != null && KeepProcessing)
        {
            UrlsFromSitemapPool.AddRange(sitemap.Items.Select(x => HttpUtility.UrlDecode(x.Location.ToString().TrimEnd('/'))));
            ValidSitemapXmlFound = true;
            Console.WriteLine($"Current processing sitemap URLs is : {UrlsFromSitemapPool.Count}");
        }
        else if (tUrls != null && KeepProcessing)
        {
            UrlsFromSitemapPool.AddRange(tUrls.Select(x => HttpUtility.UrlDecode(x.loc.TrimEnd('/'))));
            ValidSitemapXmlFound = true;
            Console.WriteLine($"Current processing sitemap URLs is : {UrlsFromSitemapPool.Count}");
        }
        if (UrlsFromSitemapPool.Count > UrlsFromSitemapCountPreset)
        {
            Console.WriteLine($"\r\nThe sitemap is too large or complex and may take a long time to read it!" +
                $"\r\nCurrent sitemap URLs limit value is {UrlsFromSitemapCountPreset}. Double current sitemap URLs limit and continue?");
            var key = YesNoCheker();
            if (key.Key == ConsoleKey.N)
            {
                KeepProcessing = false;
                Console.WriteLine($"\r\nIf part of the sitemap has been loaded, it will be taken into account in result...");
            }
            else if (key.Key == ConsoleKey.Y)
            {
                UrlsFromSitemapCountPreset = UrlsFromSitemapCountPreset * 2;
                Console.WriteLine($"\r\nCurrent sitemap URLs limit value is : {UrlsFromSitemapCountPreset}");
                Console.WriteLine("\r\nContinue reading the sitemap...");
            }
        }
    }

    public ConsoleKeyInfo YesNoCheker()
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
}
