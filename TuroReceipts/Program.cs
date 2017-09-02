using CsvHelper;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace TuroReceipts
{
    class Program
    {
        private const NumberStyles CurrencyStyles = NumberStyles.AllowCurrencySymbol | NumberStyles.Number | NumberStyles.AllowThousands;
        private const string TuroBaseUrl = "https://turo.com";

        static void Main(string[] args)
        {
            var username = ConfigurationManager.AppSettings["Username"];
            var password = ConfigurationManager.AppSettings["Password"];

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("Username and password must be configured...");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Enter the max receipts to fetch:");
            var maxReceiptsInput = Console.ReadLine();

            int maxReceipts = 10;
            if (!int.TryParse(maxReceiptsInput, out maxReceipts))
            {
                Console.WriteLine("Defaulting to 10...");
            }

            IWebDriver webDriver = new ChromeDriver();
            Login(webDriver, username, password);
            Thread.Sleep(1000);
            var trips = GetTrips(webDriver, new List<Trip>(), maxReceipts);
            webDriver.Quit();

            if (trips.Any())
            {
                using (TextWriter tr = new StreamWriter("TuroReceipts.csv"))
                {
                    var csvWriter = new CsvWriter(tr);
                    csvWriter.WriteRecords(trips);
                }

                var process = Process.Start("TuroReceipts.csv");

                Console.WriteLine("Turo Receipts written to CSV");
            }
            else
            {
                Console.WriteLine("No Trips found...");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        /// <summary>
        /// Logins to Turo.
        /// </summary>
        /// <param name="webDriver">The web driver.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        static void Login(IWebDriver webDriver, string username, string password)
        {
            webDriver.Navigate().GoToUrl(string.Format("{0}/login", TuroBaseUrl));

            webDriver.FindElement(By.Name("username")).SendKeys(username);
            webDriver.FindElement(By.Name("password")).SendKeys(password);

            webDriver.FindElement(By.Id("submit")).Click();
        }

        /// <summary>
        /// Recurrsivley iterate throught the pages of trips.
        /// </summary>
        /// <param name="webDriver"></param>
        /// <param name="trips"></param>
        /// <param name="pageSlug"></param>
        /// <returns></returns>
        static List<Trip> GetTrips(IWebDriver webDriver, List<Trip> trips, int maxReceipts, string pageSlug = null)
        {
            if (pageSlug == null)
            {
                webDriver.Navigate().GoToUrl(string.Format("{0}/trips", TuroBaseUrl));
            }
            else
            {
                string tripsUrl = string.Format("{0}/trips?{1}", TuroBaseUrl, pageSlug);
                Console.WriteLine("Navigating to {0}...", tripsUrl);
                webDriver.Navigate().GoToUrl(tripsUrl);
            }

            string nextPage = null;
            var pageLinks = webDriver.FindElements(By.ClassName("paginator-link"));
            if (pageLinks.Any())
            {
                var lastPage = pageLinks.Last();
                if (lastPage.Text == "›")
                {
                    var href = lastPage.GetAttribute("href");
                    nextPage = href.Split('?').Last();
                }
            }

            var tripElements = webDriver.FindElements(By.ClassName("reservationSummary"))
                .Where(x => 
                    x.GetAttribute("class").Contains("completed") || 
                    x.GetAttribute("class").Contains("cancelled"))
                .ToList();

            var cancelledTripElemenets = tripElements
                .Where(x => x.GetAttribute("class").Contains("cancelled"))
                .ToList();

            var cancelledTrips = cancelledTripElemenets.Select(x => x.Text).ToList();

            var tripSlugs = tripElements.Where(x => x.GetAttribute("class").Contains("completed"))
                .Select(x => x.FindElement(By.ClassName("reservation")).GetAttribute("data-href"))
                .ToList();

            Console.WriteLine("Trip Slugs: {0}", string.Join(",", tripSlugs));
            Console.WriteLine("Cancelled Trips: {0}", string.Join(",", cancelledTrips));

            foreach(var tripSlug in tripSlugs)
            {
                if (trips.Count >= maxReceipts) break;

                var trip = GetTrip(webDriver, tripSlug);
                if (trip != null)
                {
                    trips.Add(trip);
                }
            }

            foreach (var cancelledTripElemenet in cancelledTripElemenets)
            {
                if (trips.Count >= maxReceipts) break;

                var trip = ProcessCancelledTrip(cancelledTripElemenet);
                if (trip != null)
                {
                    trips.Add(trip);
                }
            }

            if (nextPage != null && trips.Count < maxReceipts) return GetTrips(webDriver, trips, maxReceipts, nextPage);
            return trips;
        }

        /// <summary>
        /// Get an individual trip info by going to the receipt and pulling out the data.
        /// </summary>
        /// <param name="webDriver"></param>
        /// <param name="reservationUrlSnippet"></param>
        /// <returns></returns>
        static Trip GetTrip(IWebDriver webDriver, string reservationUrlSnippet)
        {
            var receiptUrl = string.Format("{0}{1}/receipt/", TuroBaseUrl, reservationUrlSnippet);
            webDriver.Navigate().GoToUrl(receiptUrl);

            var pickup = webDriver.FindElement(By.ClassName("reservationSummary-schedulePickUp"));
            var dropoff = webDriver.FindElement(By.ClassName("reservationSummaryDropOff"));

            var costElement = webDriver.FindElement(By.ClassName("cost-details"));
            var cost = costElement.FindElement(By.ClassName("value")).Text;
            var costAmount = ParseCurrency(cost);

            var paymentElement = webDriver.FindElement(By.ClassName("payment-details"));

            decimal paymentAmount = 0m;
            decimal turoFees = 0m;
            try
            {
                var payment = paymentElement.FindElement(By.ClassName("positive")).Text;
                paymentAmount = ParseCurrency(payment);

                var fees = paymentElement.FindElement(By.ClassName("negative")).Text;
                turoFees = ParseCurrency(fees.Substring(1));
            }
            catch (Exception)
            {
                Console.WriteLine("Not included in earnings: {0}", receiptUrl);
                return null;
            }

            var reimbursementTolls = 0m;
            var reimbursementMileage = 0m;

            try
            {

                var reimbursementsElement = webDriver.FindElement(By.ClassName("reimbursements"));
                var lineItems = reimbursementsElement.FindElements(By.ClassName("line-item--longLabel"));
                foreach(var lineItem in lineItems)
                {
                    if (lineItem.Text.Contains("tolls"))
                    {
                        var toll = lineItem.Text.Split(' ').Last();
                        reimbursementTolls = ParseCurrency(toll);
                    }

                    if (lineItem.Text.Contains("additional miles driven"))
                    {
                        var miles = lineItem.Text.Split(' ').Last();
                        reimbursementMileage = ParseCurrency(miles);
                    }
                }
            }
            catch(Exception)
            {
                Console.WriteLine("No reimbursements found: {0}", receiptUrl);
            }

            return new Trip()
            {
                ReceiptUrl = receiptUrl,
                PickupDateTime = ProcessDateTime(pickup),
                DropoffDateTime = ProcessDateTime(dropoff),
                Cost = costAmount,
                TuroFees = turoFees,
                Earnings = paymentAmount,
                ReimbursementMileage = reimbursementMileage,
                ReimbursementTolls = reimbursementTolls
            };
        }

        /// <summary>
        /// Process a cancelled trip.
        /// </summary>
        /// <param name="webElement"></param>
        /// <returns></returns>
        static Trip ProcessCancelledTrip(IWebElement webElement)
        {
            if (!webElement.Text.Contains("$")) return null;

            var earnings = 0m;

            //find where the dollar starts
            var dollarSymbolIndex = webElement.Text.IndexOf("$");

            //find where a space is after the dollar symbol
            var spaceOrEndOfStringIndex = webElement.Text.IndexOf(" ", dollarSymbolIndex);

            //if space not found replace with end index of string
            spaceOrEndOfStringIndex = spaceOrEndOfStringIndex == -1 ? webElement.Text.Length - 1 : spaceOrEndOfStringIndex;

            //find the length of the dollar string
            var length = spaceOrEndOfStringIndex - dollarSymbolIndex;

            //get the substring for the amount
            var amount = webElement.Text.Substring(dollarSymbolIndex, length);

            //parse!
            earnings = ParseCurrency(amount);

            return new Trip()
            {
                Earnings = earnings
            };
        }

        /// <summary>
        /// Process the date and time from a web element.
        /// </summary>
        /// <param name="webElement"></param>
        /// <returns></returns>
        static string ProcessDateTime(IWebElement webElement)
        {
            var date = webElement.FindElement(By.ClassName("scheduleDate")).Text;
            var time = webElement.FindElement(By.ClassName("scheduleTime")).Text;

            return string.Format("{0} {1}", date, time);
        }

        /// <summary>
        /// Parse currency value from a string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static decimal ParseCurrency(string value)
        {
            return decimal.Parse(value, CurrencyStyles);
        }
    }
}
