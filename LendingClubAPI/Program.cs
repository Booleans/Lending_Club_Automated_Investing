using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using LendingClubAPI.Classes;
using Newtonsoft.Json;

namespace LendingClubAPI
{
    internal class Program
    {
        public static string projectDirectory;
        public static string latestLoansUrl;
        public static string detailedNotesOwnedUrl;
        public static string accountSummaryUrl;
        public static string submitOrderUrl;
        public static NotesOwned myNotesOwned;
        public static string[] stateAbbreviations;


        private static void Main(string[] args)
        {
            //********************************************************************************************************************************//
            //********************************************************************************************************************************//

            // We need an array of possible state abbreviations.
            stateAbbreviations = new string[] {
                                 "AK","AL","AR","AZ","CA",
                                 "CO","CT","DE","FL","GA",
                                 "HI","IA","ID","IL","IN",
                                 "KS","KY","LA","MA","MD",
                                 "ME","MI","MN","MO","MS",
                                 "MT","NC","ND","NE","NH",
                                 "NJ","NM","NV","NY","OH",
                                 "OK","OR","PA","RI","SC",
                                 "SD","TN","TX","UT","VA",
                                 "VT","WA","WI","WV","WY"};

            // Find the directory of the project so we can use a relative path to the authorization token file. 
            projectDirectory = Directory.GetParent(Directory.GetParent(Directory.GetCurrentDirectory()).ToString()).ToString();

            var andrewAuthorizationTokenFilePath = @"C:\AndrewAuthorizationToken.txt";
            var andrewAuthorizationToken = File.ReadAllText(andrewAuthorizationTokenFilePath);

            // Store the Account object to get balance and outstanding principal.
            Account andrewAccount = GetAccountFromJson(RetrieveJsonString("https://api.lendingclub.com/api/investor/v1/accounts/1302864/summary", andrewAuthorizationToken));

            andrewAccount.authorizationToken = andrewAuthorizationToken;
            andrewAccount.statePercentLimit = 0.05;
            andrewAccount.amountToInvestPerLoan = 25.0;
            andrewAccount.loanGradesAllowed = new string[] {"B", "C", "D"};
            andrewAccount.authorizationTokenFilePath = @"C:\AndrewAuthorizationToken.txt";
            andrewAccount.notesFromCSVFilePath = projectDirectory + @"\notes_ext.csv";
            andrewAccount.allowedStates = CalculateAndSetAllowedStatesFromCsv(andrewAccount.notesFromCSVFilePath, andrewAccount.statePercentLimit, andrewAccount.accountTotal);
            andrewAccount.numberOfLoansToInvestIn = (int)(andrewAccount.availableCash / andrewAccount.amountToInvestPerLoan);

            // Url for retrieving the latest listing of loans
            latestLoansUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing?showAll=true";

            // Url to retrieve the detailed list of notes owned
            detailedNotesOwnedUrl = "https://api.lendingclub.com/api/investor/v1/accounts/1302864/detailednotes";

            // Url to retrieve account summary, which contains value of outstanding principal
            accountSummaryUrl = "https://api.lendingclub.com/api/investor/v1/accounts/1302864/summary";

            // Url to submit a request to buy loans
            submitOrderUrl = "https://api.lendingclub.com/api/investor/v1/accounts/1302864/orders";
            //********************************************************************************************************************************//
            //********************************************************************************************************************************//

            // Use a stopwatch to terminate code after a certain duration.
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // We only need to search for loans if the available balance >= minimum investment amount. 
            if (andrewAccount.availableCash < andrewAccount.amountToInvestPerLoan)
            {
                return;
            }

            // Retrieve list of notes owned to create a list of loan ID values.
            myNotesOwned = GetLoansOwnedFromJson(RetrieveJsonString(detailedNotesOwnedUrl, andrewAccount.authorizationToken));

            andrewAccount.loanIDsOwned = (from loan in myNotesOwned.myNotes.AsEnumerable()
                                          select loan.loanId).ToList();
                         
            while (stopwatch.ElapsedMilliseconds < 120000 && andrewAccount.availableCash >= 0)
            {
                // If this is the first time retrieving listed loans, retrieve all.
                // Retrieve only new loans for subsequent loops. 
                if (andrewAccount.getAllLoans)
                {
                    latestLoansUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing?showAll=true";
                    andrewAccount.getAllLoans = false;
                }
                else
                {
                    latestLoansUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing?showAll=false";
                }

                // Retrieve the latest offering of loans on the platform.
                NewLoans latestListedLoans = GetNewLoansFromJson(RetrieveJsonString(latestLoansUrl, andrewAccount.authorizationToken));

                // Filter the new loans based off of my criteria. 
                var filteredLoans = FilterNewLoans(latestListedLoans.loans, andrewAccount);

                // We only need to build an order if filteredLoan is not null.
                if (!filteredLoans.Any())
                {
                    // Wait one second before retrieving loans again if there are no loans passing the filter. 
                    Thread.Sleep(1000);
                    continue;
                }

                // Create a new order to purchase the filtered loans. 
                Order order = new Order();
                order = BuildOrder(filteredLoans, andrewAccount.amountToInvestPerLoan, andrewAccount.investorID);

                string output = JsonConvert.SerializeObject(order);

                var orderResponse = JsonConvert.DeserializeObject<CompleteOrderConfirmation>(SubmitOrder(submitOrderUrl, output, andrewAccount.authorizationToken));

                var orderConfirmations = orderResponse.orderConfirmations.AsEnumerable();

                var loansPurchased = (from confirmation in orderConfirmations
                                      where confirmation.investedAmount >= 0
                                      select confirmation.loanId);

                // Add purchased loans to the list of loan IDs owned. 
                andrewAccount.loanIDsOwned.AddRange(loansPurchased);

                // Subtract successfully invested loans from account balance.
                andrewAccount.availableCash -= loansPurchased.Count() * andrewAccount.amountToInvestPerLoan;
            }

            Console.ReadLine();
        }

        public static string RetrieveJsonString(string myURL, string authorizationToken)
        {
            WebRequest wrGETURL = WebRequest.Create(myURL);
 
            // Read authorization token from file.
            wrGETURL.Headers.Add("Authorization:" + authorizationToken);
            wrGETURL.ContentType = "applicaton/json; charset=utf-8";

            var objStream = wrGETURL.GetResponse().GetResponseStream();

            using (StreamReader reader = new StreamReader(objStream))
            {
                // Return a string of the JSON.
                return reader.ReadToEnd();
            }

        }

        // Method to convert JSON into account balance.
        public static Account GetAccountFromJson(string inputJson)
        {
            Account accountDetails = JsonConvert.DeserializeObject<Account>(inputJson);
            return accountDetails;
        }

        public static NotesOwned GetLoansOwnedFromJson(string inputJson)
        {
            NotesOwned notesOwned = JsonConvert.DeserializeObject<NotesOwned>(inputJson);
            return notesOwned;
        }

        public static NewLoans GetNewLoansFromJson(string inputJson)
        {
            NewLoans newLoans = JsonConvert.DeserializeObject<NewLoans>(inputJson);
            return newLoans;
        }

        public static IEnumerable<Loan> FilterNewLoans(List<Loan> newLoans, Account accountToUse)
        {

            var filteredLoans = (from l in newLoans
                                 where l.annualInc >= 59900 &&
                                 (l.purpose == "debt_consolidation" || l.purpose == "credit_card") &&
                                 (l.inqLast6Mths == 0) &&
                                 (l.intRate >= 10.0) &&
                                 //(l.intRate <= 18.0) &&
                                 (l.term == 36) &&
                                 (accountToUse.loanGradesAllowed.Contains(l.grade)) &&
                                 (l.mthsSinceLastDelinq == null) &&
                                 (l.loanAmount <= 1.1*l.revolBal) &&
                                 (l.loanAmount >= .9*l.revolBal) &&
                                 (accountToUse.allowedStates.Contains(l.addrState.ToString())) &&
                                 (!accountToUse.loanIDsOwned.Contains(l.id))
                                 orderby l.intRate descending                                                            
                                 select l).Take(accountToUse.numberOfLoansToInvestIn);
            
            return filteredLoans;
        }

        public static Order BuildOrder(IEnumerable<Loan> loansToBuy, double amountToInvest, string accountNumber)
        {
            Order order = new Order {aid = (Int32.Parse(accountNumber))};

            List<LoanForOrder> loansToOrder = new List<LoanForOrder>();

            foreach (Loan loan in loansToBuy)
            {
                LoanForOrder buyLoan = new LoanForOrder
                {
                    loanId = loan.id,
                    requestedAmount = amountToInvest,
                    portfolioId = null
                };

                loansToOrder.Add(buyLoan);
            }

            order.orders = loansToOrder;
            return order;
        }

        public static string SubmitOrder(string postURL, string jsonToSubmit, string authorizationToken)
        {
            
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(postURL);
            httpWebRequest.Headers.Add("Authorization:" + authorizationToken);
            httpWebRequest.ContentType = "application/json; charset=utf-8";
            httpWebRequest.Method = "POST";            

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = jsonToSubmit;

                streamWriter.Write(json);
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                return result;
            }
        }

        public static string[] CalculateAndSetAllowedStatesFromCsv(string CSVInputpath, double statePercentLimit, double totalAccountValue)
        {
            string allowedStatesFromCSV = File.ReadAllText(CSVInputpath);
            char[] delimiters = new char[] { '\r', '\n' };
            string[] allowedStates = allowedStatesFromCSV.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, double> states = stateAbbreviations.ToDictionary(state => state, state => 0.0);

            // Skip the first line because it contains the row headings. 
            foreach (string note in allowedStates.Skip(1))
            {
                string stateOfNote = null;
                double principalRemainingOfNote = 0;

                var noteDetails = note.Split(',');

                // We need to make sure we are only using loans that are current. 
                bool isNoteCurrent = noteDetails.Any(detail => detail == "Current");

                // If the note is not current then skip to the next iteration of the foreach loop. 
                if (!isNoteCurrent) continue;

                stateOfNote = noteDetails.First(detail => stateAbbreviations.Contains(detail));

                principalRemainingOfNote = Double.Parse(noteDetails[10]);

                // Increase the principal value in that state.
                states[stateOfNote] += Math.Round(principalRemainingOfNote, 2);
            }

            // Sort the states in alphabetical order.
            var sortedStates = from k in states
                               where (k.Value <= statePercentLimit * totalAccountValue)
                               orderby k.Key
                               select k.Key;

            // Set the allowedStates variable to the result of the query.
            allowedStates = sortedStates.ToArray();

            return allowedStates;
        }
    }
}
