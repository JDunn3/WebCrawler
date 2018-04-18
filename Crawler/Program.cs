using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Crawler
{
    class Program
    {
        static string startUrl = Properties.Settings1.Default.startUrl,
                rootMatch = Properties.Settings1.Default.rootUrlContainsMatch,
                currentUrl;
        static List<string> uniqueLinks = new List<string>(),
            csvLinesToBeWritten = new List<string>(),
            csvBadLinks = new List<string>();
        static int count = 0;
        

        static void Main(string[] args)
        {
            currentUrl = startUrl;
            csvLinesToBeWritten.Add("URL,STATUS_CODE,ORIGIN\n");
            csvBadLinks.Add("URL,ORIGIN\n");
            Console.WriteLine("Starting Crawler...");
            Crawl();
            WriteCSV(csvLinesToBeWritten, "crawler_report.csv");
            WriteCSV(csvBadLinks, "crawler_report-excluded_links.csv");
        }

        private static void Crawl()
        {
            if (!uniqueLinks.Contains(currentUrl))
            {
                AddCSVLine(StringToCSVCell(currentUrl) + "," + GetStatusCode(currentUrl) + "," + currentUrl + "\n");
                uniqueLinks.Add(startUrl);
            }

            if (CheckForRoot(currentUrl))
            {
                List<String> linksFromPage = ExtractUncrawledLinksFromUrl(currentUrl);
                foreach (String link in linksFromPage)
                {
                    AddCSVLine(StringToCSVCell(link) + "," + GetStatusCode(link) + "," + currentUrl + "\n");
                }
            }
            try
            {
                currentUrl = uniqueLinks[++count];
            }
            catch(IndexOutOfRangeException)
            {
                return;
            }
            Console.WriteLine("Finished crawling page, grabbing next page from uniques list.");
            Crawl();
        }

        private static List<String> ExtractUncrawledLinksFromUrl(string url)
        {
            Console.WriteLine("Extracting uncrawled links from " + url);
            List<String> links = new List<string>();
            HtmlWeb hw = new HtmlWeb();
            HtmlDocument doc = hw.Load(url);
            try
            {
                foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//a[@href]"))
                {
                    string link = node.Attributes["href"].Value;
                    //make sure we haven't already found this link.
                    if (CheckForHttpOrHttpsUri(link))
                    {
                        if (!uniqueLinks.Contains(link))
                        {
                            uniqueLinks.Add(link);
                            links.Add(link);
                        }
                    }
                    else
                    {
                        csvBadLinks.Add(link + "," + url + "\n");
                    }
                }
            }
            catch (NullReferenceException)
            {
                Console.WriteLine("URL appears to not be an html document, cannot crawl: " + url);
                csvBadLinks.Add(url + "," + currentUrl + "\n");
            }
            return links;
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
            catch(WebException ex)
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

        private static string StringToCSVCell(string str)
        {
            bool mustQuote = (str.Contains(",") || str.Contains("\"") || str.Contains("\r") || str.Contains("\n"));
            if (mustQuote)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("\"");
                foreach (char nextChar in str)
                {
                    sb.Append(nextChar);
                    if (nextChar == '"')
                        sb.Append("\"");
                }
                sb.Append("\"");
                return sb.ToString();
            }

            return str;
        }

        private static bool CheckForRoot(string url)
        {
            if(url.Contains("https://newbalance.com")|| url.Contains("https://www.newbalance.com") || url.Contains("http://newbalance.com") || url.Contains("http://www.newbalance.com"))
                return true;
            return false;
        }

        private static void AddCSVLine(string line)
        {
            Console.WriteLine("Adding csv line(" + count + "): " + line);
            csvLinesToBeWritten.Add(line);
        }

        private static bool CheckForHttpOrHttpsUri(string url)
        {
            //https://stackoverflow.com/questions/7578857/how-to-check-whether-a-string-is-a-valid-http-url
            Uri uriResult;
            return Uri.TryCreate(url, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private static void WriteCSV(List<String> csvLines, string name)
        {
            System.IO.File.WriteAllLines(name, csvLines);
        }
    }
}
