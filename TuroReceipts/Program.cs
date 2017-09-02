using CsvHelper;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
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

            int maxReceipts = 0;
            if (!int.TryParse(maxReceiptsInput, out maxReceipts))
            {
                Console.WriteLine("Defaulting to 10...");
                maxReceipts = 10;
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

            var tripSlugs = tripElements.Where(x => x.GetAttribute("class").Contains("completed"))
                .Select(x => x.FindElement(By.ClassName("reservation")).GetAttribute("data-href"))
                .ToList();

            var cancelledTripSlugs = tripElements.Where(x => x.GetAttribute("class").Contains("cancelled"))
                .Select(x => x.FindElement(By.ClassName("reservation")).GetAttribute("data-href"))
                .ToList();

            foreach (var tripSlug in tripSlugs)
            {
                if (trips.Count >= maxReceipts) break;

                var trip = GetTrip(webDriver, tripSlug);
                if (trip != null)
                {
                    trips.Add(trip);
                }
            }

            foreach (var cancelledTripSlug in cancelledTripSlugs)
            {
                if (trips.Count >= maxReceipts) break;

                var trip = ProcessCancelledTrip(webDriver, cancelledTripSlug);
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
            var reservationId = reservationUrlSnippet.Substring(reservationUrlSnippet.LastIndexOf("/") + 1);

            var tripUrl = string.Format("{0}{1}", TuroBaseUrl, reservationUrlSnippet);
            Console.WriteLine("Navigating to {0}...", tripUrl);
            webDriver.Navigate().GoToUrl(tripUrl);

            WebDriverWait wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(10));
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException));
            wait.PollingInterval = TimeSpan.FromMilliseconds(100);
            wait.Until(x => x.FindElement(By.ClassName("vehicleDetailsHeader-text")));

            var carElement = webDriver.FindElement(By.ClassName("vehicleDetailsHeader-text"));
            var car = carElement.FindElement(By.TagName("div")).Text;
            var carUrl = carElement.FindElement(By.TagName("a")).GetAttribute("href");
            var carId = carUrl.Substring(carUrl.LastIndexOf("/") + 1);

            var receiptUrl = string.Format("{0}{1}/receipt/", TuroBaseUrl, reservationUrlSnippet);
            Console.WriteLine("Navigating to {0}...", receiptUrl);
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
                ReservationId = reservationId,
                TripUrl = tripUrl,
                ReceiptUrl = receiptUrl,
                CarUrl = carUrl,
                CarId = carId,
                Car = car,
                Status = "Completed",
                PickupDateTime = ProcessReservationDateTime(pickup),
                DropoffDateTime = ProcessReservationDateTime(dropoff),
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
        static Trip ProcessCancelledTrip(IWebDriver webDriver, string reservationUrlSnippet)
        {
            var reservationId = reservationUrlSnippet.Substring(reservationUrlSnippet.LastIndexOf("/") + 1);

            var tripUrl = string.Format("{0}{1}", TuroBaseUrl, reservationUrlSnippet);
            Console.WriteLine("Navigating to {0}...", tripUrl);
            webDriver.Navigate().GoToUrl(tripUrl);

            WebDriverWait wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(10));
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException));
            wait.PollingInterval = TimeSpan.FromMilliseconds(100);
            wait.Until(x => x.FindElement(By.ClassName("vehicleDetailsHeader-text")));

            var carElement = webDriver.FindElement(By.ClassName("vehicleDetailsHeader-text"));
            var car = carElement.FindElement(By.TagName("div")).Text;
            var carUrl = carElement.FindElement(By.TagName("a")).GetAttribute("href");
            var carId = carUrl.Substring(carUrl.LastIndexOf("/") + 1);

            var pickup = webDriver.FindElement(By.ClassName("tripSchedule-startDate"));
            var dropoff = webDriver.FindElement(By.ClassName("tripSchedule-endDate"));

            var earninsText = webDriver.FindElement(By.ClassName("reservationDetails-totalEarnings")).Text;
            var earnings = ParseCurrency(earninsText);

            return new Trip()
            {
                ReservationId = reservationId,
                TripUrl = tripUrl,
                CarUrl = carUrl,
                CarId = carId,
                Car = car,
                Status = "Cancelled",
                PickupDateTime = ProcessTripScheduleDateTime(pickup),
                DropoffDateTime = ProcessTripScheduleDateTime(dropoff),
                Earnings = earnings
            };
        }

        /// <summary>
        /// Process the date and time from a web element.
        /// </summary>
        /// <param name="webElement"></param>
        /// <returns></returns>
        static string ProcessReservationDateTime(IWebElement webElement)
        {
            var date = webElement.FindElement(By.ClassName("scheduleDate")).Text;
            var time = webElement.FindElement(By.ClassName("scheduleTime")).Text;

            return string.Format("{0} {1}", date, time);
        }

        /// <summary>
        /// Processes the trip schedule date time.
        /// </summary>
        /// <param name="webElement">The web element.</param>
        /// <returns></returns>
        static string ProcessTripScheduleDateTime(IWebElement webElement)
        {
            var date = webElement.FindElement(By.ClassName("schedule-date")).Text;
            var time = webElement.FindElement(By.ClassName("schedule-time")).Text;

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
