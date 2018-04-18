using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler2
{
    class Program
    {
        

        static void Main(string[] args)
        {
            WebCrawler.Crawl();
        }
    }
    //todo https://stackoverflow.com/questions/10208330/why-does-iterating-over-getconsumingenumerable-not-fully-empty-the-underlying
    static class WebCrawler
    {
        static readonly object myLock = new object();
        static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        static string mStartUrl = Properties.CrawlerSettings.Default.startUrl;
        static ConcurrentDictionary<string,string> mUniqueResources = new ConcurrentDictionary<string,string>();//uniquely linked resources on the page(no query parameters)
        //static ConcurrentDictionary<string,string> mUniqueLinks = new ConcurrentDictionary<string, string>();//unique links linked on the page
        static ConcurrentDictionary<int, bool> threadTracking = new ConcurrentDictionary<int, bool>();
        static int nextThreadId = 0;

        public static void Crawl()
        {
            log.Info("Starting Crawler...");
            using (BlockingCollection<string> urlsToCrawl  = new BlockingCollection<string>())
            {
                log.Debug("Adding seed url: " + mStartUrl);
                urlsToCrawl.Add(mStartUrl);


                Parallel.ForEach(urlsToCrawl.GetConsumingEnumerable(), url =>//(string url in )
                {
                    int threadId = Interlocked.Increment(ref nextThreadId);
                    if (!threadTracking.TryAdd(threadId, true))
                    {
                        log.Error("Failed to track thread with id: " + threadId);
                        return;
                    }

                    log.Debug("Started new thread(ID: " + threadId + ") to process URL: " + url);
                    //handle adding new links to the crawling collection first so new threads can spin up.
                    foreach (string newUrlToCrawl in ProcessNewlyFoundLinks(ExtractHttpLinks(url)))
                    {
                        string newUrlToCrawlStripped = StripQueryParametersFromUriString(newUrlToCrawl);
                        lock (myLock)
                        {
                            if (!urlsToCrawl.Contains(newUrlToCrawlStripped))
                                urlsToCrawl.Add(newUrlToCrawlStripped);
                        }
                    }

                    if (mUniqueResources.TryAdd(url, GetStatusCode(StripQueryParametersFromUriString(url))))
                        log.Info("Added new unique resource: " + url);
                    else
                        log.Warn("Failed to add new unique resource. Likely already exists: " + url);

                    bool temp;
                    if (!threadTracking.TryRemove(threadId, out temp))
                        throw new Exception("Failed to remove thread ID: " + threadId);

                    if (threadTracking.IsEmpty)
                    {
                        log.Info("All processing threads appear to have stopped, waiting two minutes then shutting down.");
                        Thread.Sleep(120000);//wait two more minutes to give all remaining threads ample time to get out.
                        if (threadTracking.IsEmpty)
                        {
                            urlsToCrawl.CompleteAdding();
                            log.Info("Shutting down.");
                        }
                        else
                            log.Info("Aborting shutdown, new threads found and running.");
                    }
                });
            }
        }

        private static string GetStatusCode(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "HEAD";
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                int statusCode = (int)response.StatusCode;
                response.Close();
                response.Dispose();
                return statusCode.ToString();
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (response != null)
                    {
                        int statusCode = (int)response.StatusCode;
                        return statusCode.ToString();
                    }
                    else
                    {
                        return "Uknown Status";
                    }
                }
                else
                {
                    return "Uknown Status";
                }
            }
        }

        private static List<string> ProcessNewlyFoundLinks(List<string> links)
        {
            List<string> newUrlsToCrawl = new List<string>();
            foreach (string link in links)
            {
                if (CheckForRoot(link))
                {
                    newUrlsToCrawl.Add(link);
                    log.Debug("Adding new URL to crawl: " + link);
                }
            }

            return newUrlsToCrawl;
        }

        private static List<string> ExtractHttpLinks(string root)
        {
            log.Info("Extracting links from " + root);
            List<String> links = new List<string>();
            HtmlWeb hw = new HtmlWeb();
            HtmlDocument doc = hw.Load(root);
            try
            {
                foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//a[@href]"))
                {
                    string link = node.Attributes["href"].Value;
                    //If its a link we can load, make sure we haven't already found this link.
                    if (CheckForHttpOrHttpsUri(link))
                    {
                        links.Add(link);
                        log.Debug("Found link: " + link + ". Source page: " + root);
                    }
                    else
                    {
                        log.Debug("Found non-http/https link, ignoring. Source page: " + root);
                    }
                }
            }
            catch (NullReferenceException)
            {
                log.Info("URL appears to not be an html document, cannot extract links: " + root);
            }
            return links;
        }

        private static bool CheckForHttpOrHttpsUri(string url)
        {
            //https://stackoverflow.com/questions/7578857/how-to-check-whether-a-string-is-a-valid-http-url
            Uri uriResult;
            return Uri.TryCreate(url, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private static string StripQueryParametersFromUriString(string uri)
        {
            if(uri.Contains('?'))
                return uri.Substring(0, uri.IndexOf('?'));
            return uri;
        }

        private static bool CheckForRoot(string url)
        {
            foreach(SettingsProperty myProperty in Properties.CrawlerSettings.Default.Properties)
                if (url.Contains(Properties.CrawlerSettings.Default[myProperty.Name].ToString()))
                    return true;

            return false;
        }
    }
}
