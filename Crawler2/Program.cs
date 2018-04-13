using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler2
{
    class Program
    {
        static void Main(string[] args)
        {
        }
    }

    class ConsumingEnumerableDemo
    {
        static string startUrl = Properties.CrawlerSettings.Default.startUrl,
                rootMatch = Properties.CrawlerSettings.Default.rootUrlContainsMatch;


        public static void BC_GetConsumingEnumerable()
        {
            using (BlockingCollection<int> bc = new BlockingCollection<int>())
            {
                // Kick off a producer task
                Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        bc.Add(i);
                        Thread.Sleep(100); // sleep 100 ms between adds
                    }

                    // Need to do this to keep foreach below from hanging
                    bc.CompleteAdding();
                });

                // Now consume the blocking collection with foreach.
                // Use bc.GetConsumingEnumerable() instead of just bc because the
                // former will block waiting for completion and the latter will
                // simply take a snapshot of the current state of the underlying collection.
                foreach (var item in bc.GetConsumingEnumerable())
                {
                    Console.WriteLine(item);
                }
            }
        }

        private static String StripUrlStringParameters()
        {

        }
    }
}
