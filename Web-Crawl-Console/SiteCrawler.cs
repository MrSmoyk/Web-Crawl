using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Web_Crawl_Console;

public class SiteCrawler
{
    private HttpClient client;

    private int ProcessingCount = 0;

    private int taskCount = 0;

    public bool StartUrlValidation = false;
    private Uri startingURL { get; set; }

    private ConcurrentBag<string> ImageFormatExceptions = new ConcurrentBag<string>() { ".gif", ".jpg", ".jpeg",
        ".png",".ico",".TIFF",".webp",".eps", ".ttf", ".wav", ".zip", ".crt", ".traineddata"
        ,".svg",".psd",".indd",".cdr",".ai", ".xlsx", ".docx", ".msi", ".crl"
        ,".raw",".txt",".xml",".pdf",".dib", ".snippet", ".pfx" };

    public ConcurrentBag<Uri> DisallowedFromRobotRules = new ConcurrentBag<Uri>();

    private ConcurrentDictionary<string, long> PingedPages = new ConcurrentDictionary<string, long>();
    private ConcurrentBag<string> UniqueURLs { get; set; } = new ConcurrentBag<string>();
    public ConcurrentBag<Uri> BrokenLinks { get; set; } = new ConcurrentBag<Uri>();
    private ConcurrentQueue<Uri> UrlTaskQueue { get; set; } = new ConcurrentQueue<Uri>();
    public List<KeyValuePair<string, long>> Result { get; set; } = new();

    public SitemapChecker SitemapCrawler;

    public SiteCrawler()
    {
        client = new HttpClient();
        taskCount = 20;
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36 Edg/105.0.1343.53");
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml");
    }

    public async Task StartCrawl(Uri uri)
    {
        startingURL = uri;

        if (ValidateStartUrl())
        {
            SitemapCrawler = new SitemapChecker();
            await SitemapCrawler.GetValidUrlsFromRobot(uri);

            Console.WriteLine("\r\nStart processing site...");

            if (CrawlUrl(uri))
            {
                TaskEditor();
            }

            Result = PingedPages.OrderBy(x => x.Value).ToList();
        }

        Console.WriteLine("\r\nDone!");
        Console.WriteLine($"\r\nThe number of Unique URLs is : {UniqueURLs.Count}" + 
            $"\r\nThe number of Pinged URLs is : {Result.Count}" +
            $"\r\nThe number of Disallowed from robots.txt rules URLs is : {DisallowedFromRobotRules.Count}" +
            $"\r\nThe number of Broken URLs is : {BrokenLinks.Count}");

    }

    private void TaskEditor()
    {
        if (taskCount > 0 && UniqueURLs.Count > 0)
        {
            ProcessingCount++;
            List<Task> tasksToWait = new List<Task>();
            for (int i = 0; i <= taskCount; i++)
            {
                if (i < taskCount)
                {
                    Task crawlTask = new Task(() =>
                    {
                        TaskQueueWorker();
                    }, TaskCreationOptions.LongRunning);
                    tasksToWait.Add(crawlTask);
                    crawlTask.Start();
                }
                else
                {
                    Task.WaitAll(tasksToWait.ToArray());
                    tasksToWait.Clear();
                }
            }
            ProcessingCount--;
        }
    }

    private void TaskQueueWorker()
    {
        while (ProcessingCount > 0)
        {
            while (UrlTaskQueue.TryDequeue(out var url))
            {
                if (url != null)
                {
                    ProcessingCount++;
                    CrawlUrl(url);
                    ProcessingCount--;
                }
            }
            if (ProcessingCount == 1 && UrlTaskQueue.Count == 0)
            {
                ProcessingCount--;
            }

        }
    }

    private bool CrawlUrl(Uri uri)
    {
        HtmlDocument htmlDocument = new HtmlDocument();

        try
        {

            var watch = new Stopwatch();
            watch.Restart();

            var htmlString = client.GetStringAsync(uri).Result;
            watch.Stop();

            htmlDocument.LoadHtml(htmlString);

            List<string> htmlOutput = new List<string>(htmlDocument.DocumentNode
                .Descendants("a")
                .Select(a => a.GetAttributeValue("href", null))
                .Where(u => !string.IsNullOrEmpty(u))
                .Distinct()
                .ToList());

            if (htmlOutput.Count > 0 | htmlOutput != null)
            {
                FilterUrl(uri, htmlOutput, startingURL.Host);
                PingedPages.TryAdd(uri.ToString().TrimEnd('/'), watch.ElapsedMilliseconds);
                return true;

            }
            else
            {
                return false;
            }

        }
        catch (Exception ex)
        {
            if (!BrokenLinks.Contains(uri))
                BrokenLinks.Add(uri);

            return false;
        }
    }

    private void FilterUrl(Uri scannedURL, IEnumerable<string> listFoundLinks, string hostDomain)
    {
        foreach (var link in listFoundLinks)
        {
            var standartisedUrlString = StandardiseUrlString(link);
            if (standartisedUrlString != null)
            {
                try
                {
                    if (link.StartsWith("/"))
                    {
                        Uri foundUrl = new Uri(scannedURL, standartisedUrlString);
                        Uri finalUrl = new Uri(scannedURL, StandardiseUrlString(foundUrl.LocalPath));
                        if (!UniqueURLs.Contains(finalUrl.LocalPath) && IsAllowed(finalUrl))
                        {
                            UniqueURLs.Add(finalUrl.LocalPath);
                            UrlTaskQueue.Enqueue(finalUrl);
                        }

                    }
                    else if (Uri.TryCreate(standartisedUrlString, UriKind.Absolute, out Uri uriResult))
                    {
                        if (uriResult.Host == hostDomain && !UniqueURLs.Contains(uriResult.LocalPath) && IsAllowed(uriResult))
                        {
                            Uri absoluteURL = new Uri(uriResult, StandardiseUrlString(uriResult.LocalPath));

                            if (!UniqueURLs.Contains(absoluteURL.LocalPath))
                            {
                                UniqueURLs.Add(absoluteURL.LocalPath);
                                UrlTaskQueue.Enqueue(absoluteURL);
                            }
                        }

                    }

                }
                catch (Exception ex)
                {
                    continue;
                }

            }
        }
        Console.WriteLine($"\r\nI keep working, already processed : {UniqueURLs.Count} unique URLs , pinged URLs {PingedPages.Count} and found broken URLs {BrokenLinks.Count}!");
    }

    private bool IsAllowed(Uri url)
    {
        if (SitemapCrawler.RobotsTxtFound)
        {
            if (!SitemapCrawler.RobotsFile.IsAllowedAccess(url, "MyCustom"))
            {
                if (!DisallowedFromRobotRules.Contains(url))
                    DisallowedFromRobotRules.Add(url);
                return false;

            }
            foreach (var cotn in ImageFormatExceptions)
            {
                if (url.ToString().Contains(cotn))
                    return false;
            }

            return true;

        }
        foreach (var cotn in ImageFormatExceptions)
        {
            if (url.ToString().Contains(cotn))
                return false;
        }
        return true;

    }

    private string StandardiseUrlString(string url)
    {
        if (url.Length > 1 && url.EndsWith("/"))
        {
            return url.TrimEnd('/').ToLower();
        }
        else
        {
            return url.ToLower();
        }
    }

    private bool ValidateStartUrl()
    {
        try
        {
            client.GetStringAsync(startingURL).Wait();

            StartUrlValidation = true;

            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
}

