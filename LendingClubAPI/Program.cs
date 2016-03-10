using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LendingClubAPI.Classes;
using Newtonsoft.Json;

namespace LendingClubAPI
{
    internal class Program
    {
        public static string latestLoansUrl;
        public static string[] stateAbbreviations = new string[] {
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

        private static void Main(string[] args)
        {
            var activeAccounts = InstantiateAccounts();

            // Use a stopwatch to terminate code after a certain duration.
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Parallel.ForEach(activeAccounts, investableAccount =>
            {
                // We only need to search for loans if the available balance >= minimum investment amount. 
                if (investableAccount.availableCash < investableAccount.amountToInvestPerLoan)
                {
                    Console.WriteLine("{0} does not have enough cash available to invest", investableAccount.accountTitle);
                    Console.WriteLine("Amount of cash currently available: ${0}", investableAccount.availableCash);
                    return;
                }

                Console.WriteLine("Searching for loans for the following account: {0}", investableAccount.accountTitle);
                Console.WriteLine("Amount of cash currently available: ${0}", investableAccount.availableCash);

                while (stopwatch.ElapsedMilliseconds < 60000 && investableAccount.availableCash >= investableAccount.amountToInvestPerLoan)
                {
                    // If this is the first time retrieving listed loans, retrieve all.
                    // Retrieve only new loans for subsequent loops. 
                    if (investableAccount.getAllLoans)
                    {
                        latestLoansUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing?showAll=true";
                        investableAccount.getAllLoans = false;
                    }
                    else
                    {
                        latestLoansUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing?showAll=false";
                    }

                    // Retrieve the latest offering of loans on the platform.
                    NewLoans latestListedLoans = GetNewLoansFromJson(RetrieveJsonString(latestLoansUrl, investableAccount.authorizationToken));

                    if (latestListedLoans.loans == null)
                    {
                        continue;
                    }

                    // Filter the new loans based off of my criteria. 
                    var filteredLoans = FilterNewLoans(latestListedLoans.loans, investableAccount);

                    // We only need to build an order if filteredLoan is not null.
                    if (!filteredLoans.Any())
                    {
                        // Wait one second before retrieving loans again if there are no loans passing the filter. 
                        Thread.Sleep(1000);
                        continue;
                    }

                    // Create a new order to purchase the filtered loans. 
                    Order order = BuildOrder(filteredLoans, investableAccount.amountToInvestPerLoan, investableAccount.investorID);

                    string output = JsonConvert.SerializeObject(order);

                    var orderResponse = JsonConvert.DeserializeObject<CompleteOrderConfirmation>(SubmitOrder(investableAccount.submitOrderUrl, output, investableAccount.authorizationToken));

                    var orderConfirmations = orderResponse.orderConfirmations.AsEnumerable();

                    var loansPurchased = (from confirmation in orderConfirmations
                                          where confirmation.investedAmount >= 0
                                          select confirmation.loanId);

                    if (loansPurchased.Any())
                    {
                        foreach (var loan in loansPurchased)
                        {
                            Console.WriteLine("The account {0} purchased loan ID: {1} at {2}", investableAccount.accountTitle, loan, DateTime.Now.ToShortTimeString());
                        } 
                    }


                    // Add purchased loans to the list of loan IDs owned. 
                    investableAccount.loanIDsOwned.AddRange(loansPurchased);

                    // Subtract successfully invested loans from account balance.
                    investableAccount.availableCash -= loansPurchased.Count() * investableAccount.amountToInvestPerLoan;
                }

            });

            Console.WriteLine("Execution has completed");
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
                                 where l.annualInc >= accountToUse.minimumAnnualIncome &&
                                 (l.purpose == "debt_consolidation" || l.purpose == "credit_card") &&
                                 (l.inqLast6Mths == 0) &&
                                 (l.pubRec == 0) &&
                                 (l.intRate >= accountToUse.minimumInterestRate) &&
                                 //(l.intRate <= 18.0) &&
                                 (l.term == 36) &&
                                 (accountToUse.loanGradesAllowed.Contains(l.grade)) &&
                                 (l.mthsSinceLastDelinq == null) &&
                                 //(l.loanAmount <= 1.1*l.revolBal) &&
                                 //(l.loanAmount >= .9*l.revolBal) &&
                                 (accountToUse.allowedHomeOwnership.Contains(l.homeOwnership)) &&
                                 (accountToUse.allowedStates.Contains(l.addrState)) &&
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
              
                double principalRemainingOfNote = 0;

                var noteDetails = note.Split(',');

                // We need to make sure we are only using loans that are current. 
                bool isNoteCurrent = noteDetails.Any(detail => detail == "Current");

                // If the note is not current then skip to the next iteration of the foreach loop. 
                if (!isNoteCurrent) continue;

                string stateOfNote = noteDetails.First(detail => stateAbbreviations.Contains(detail));

                principalRemainingOfNote = Double.Parse(noteDetails[10]);

                // Increase the principal value in that state.
                states[stateOfNote] += Math.Round(principalRemainingOfNote, 2);
            }

            // Sort the states in alphabetical order.
            var sortedStates = from k in states
                               where (k.Value <= statePercentLimit * totalAccountValue)
                               orderby k.Key
                               select k.Key;

            return sortedStates.ToArray();
        }

        public static List<Account> InstantiateAccounts()
        {
            // Need a list of active accounts if we are going to be running this code on multiple accounts.
            List<Account> activeAccounts = new List<Account>();

            // Find the directory of the project so we can use a relative path to the authorization token file. 
            string projectDirectory = Directory.GetParent(Directory.GetParent(Directory.GetCurrentDirectory()).ToString()).ToString();

            const string andrewAuthorizationTokenFilePath = @"C:\AndrewAuthorizationToken.txt";
            var andrewAuthorizationToken = File.ReadAllText(andrewAuthorizationTokenFilePath);

            // Store the Account object to get balance and outstanding principal.
            Account andrewTaxableAccount = GetAccountFromJson(RetrieveJsonString("https://api.lendingclub.com/api/investor/v1/accounts/1302864/summary", andrewAuthorizationToken));

            andrewTaxableAccount.accountTitle = "Andrew's Taxable Account";
            andrewTaxableAccount.authorizationToken = andrewAuthorizationToken;
            andrewTaxableAccount.minimumInterestRate = 10.0;
            andrewTaxableAccount.statePercentLimit = 0.05;
            andrewTaxableAccount.amountToInvestPerLoan = 25.0;
            andrewTaxableAccount.minimumAnnualIncome = 59900;
            andrewTaxableAccount.maximumRevolvingBalance = 9999999;
            andrewTaxableAccount.loanGradesAllowed = new string[] { "B", "C", "D" };
            andrewTaxableAccount.authorizationTokenFilePath = @"C:\AndrewAuthorizationToken.txt";
            //andrewTaxableAccount.notesFromCSVFilePath = projectDirectory + @"\notes_ext.csv";
            andrewTaxableAccount.allowedStates = stateAbbreviations;
            //andrewTaxableAccount.allowedStates = CalculateAndSetAllowedStatesFromCsv(andrewTaxableAccount.notesFromCSVFilePath, andrewTaxableAccount.statePercentLimit, andrewTaxableAccount.accountTotal);
            //andrewTaxableAccount.numberOfLoansToInvestIn = (int)(andrewTaxableAccount.availableCash / andrewTaxableAccount.amountToInvestPerLoan);
            andrewTaxableAccount.detailedNotesOwnedUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + andrewTaxableAccount.investorID + "/detailednotes";
            andrewTaxableAccount.accountSummaryUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + andrewTaxableAccount.investorID + "/summary";
            andrewTaxableAccount.submitOrderUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + andrewTaxableAccount.investorID + "/orders";
            andrewTaxableAccount.notesOwnedByAccount = GetLoansOwnedFromJson(RetrieveJsonString(andrewTaxableAccount.detailedNotesOwnedUrl, andrewTaxableAccount.authorizationToken));
            andrewTaxableAccount.loanIDsOwned = (from loan in andrewTaxableAccount.notesOwnedByAccount.myNotes.AsEnumerable()
                                                 select loan.loanId).ToList();

            activeAccounts.Add(andrewTaxableAccount);

            //const string andrewRothAuthorizationTokenFilePath = @"C:\AndrewRothAuthorizationToken.txt";
            //var andrewRothAuthorizationToken = File.ReadAllText(andrewRothAuthorizationTokenFilePath);

            //Account andrewRothAccount = GetAccountFromJson(RetrieveJsonString("https://api.lendingclub.com/api/investor/v1/accounts/?????/summary", andrewRothAuthorizationToken));

            //andrewRothAccount.authorizationToken = andrewRothAuthorizationToken;
            //andrewRothAccount.statePercentLimit = 0.05;
            //andrewRothAccount.amountToInvestPerLoan = 25.0;
            //andrewRothAccount.loanGradesAllowed = new string[] { "B", "C", "D" };
            //andrewRothAccount.authorizationTokenFilePath = @"C:\AndrewRothAuthorizationToken.txt";
            //andrewRothAccount.notesFromCSVFilePath = projectDirectory + @"\Roth_notes_ext.csv";
            //andrewRothAccount.allowedStates = CalculateAndSetAllowedStatesFromCsv(andrewRothAccount.notesFromCSVFilePath, andrewRothAccount.statePercentLimit, andrewRothAccount.accountTotal);
            //andrewRothAccount.numberOfLoansToInvestIn = (int)(andrewRothAccount.availableCash / andrewRothAccount.amountToInvestPerLoan);
            //andrewRothAccount.detailedNotesOwnedUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + andrewRothAccount.investorID + "/detailednotes";
            //andrewRothAccount.accountSummaryUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + andrewRothAccount.investorID + "/summary";
            //andrewRothAccount.submitOrderUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + andrewRothAccount.investorID + "/orders";
            //andrewRothAccount.notesOwnedByAccount = GetLoansOwnedFromJson(RetrieveJsonString(andrewRothAccount.detailedNotesOwnedUrl, andrewRothAccount.authorizationToken));
            //andrewRothAccount.loanIDsOwned = (from loan in andrewRothAccount.notesOwnedByAccount.myNotes.AsEnumerable()
            //                                  select loan.loanId).ToList();

            //activeAccounts.Add(andrewRothAccount);

            const string dadRothAuthorizationTokenFilePath = @"C:\DadRothAuthorizationToken.txt";
            var dadRothAuthorizationToken = File.ReadAllText(dadRothAuthorizationTokenFilePath);

            Account dadRothAccount = GetAccountFromJson(RetrieveJsonString("https://api.lendingclub.com/api/investor/v1/accounts/77100250/summary", dadRothAuthorizationToken));

            dadRothAccount.accountTitle = "Dad's Roth Account";
            dadRothAccount.authorizationToken = dadRothAuthorizationToken;
            dadRothAccount.statePercentLimit = 0.05;
            dadRothAccount.amountToInvestPerLoan = 75.0;
            dadRothAccount.minimumInterestRate = 6.5;
            dadRothAccount.minimumAnnualIncome = 42000;
            dadRothAccount.maximumRevolvingBalance = 15000;
            dadRothAccount.loanGradesAllowed = new string[] { "A", "B", "C"};
            dadRothAccount.authorizationTokenFilePath = @"C:\DadRothAuthorizationToken.txt";
            //dadRothAccount.notesFromCSVFilePath = projectDirectory + @"\Roth_notes_ext.csv";
            dadRothAccount.allowedStates = stateAbbreviations;
            //dadRothAccount.allowedStates = CalculateAndSetAllowedStatesFromCsv(dadRothAccount.notesFromCSVFilePath, dadRothAccount.statePercentLimit, dadRothAccount.accountTotal);
            dadRothAccount.numberOfLoansToInvestIn = (int)(dadRothAccount.availableCash / dadRothAccount.amountToInvestPerLoan);
            dadRothAccount.detailedNotesOwnedUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + dadRothAccount.investorID + "/detailednotes";
            dadRothAccount.accountSummaryUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + dadRothAccount.investorID + "/summary";
            dadRothAccount.submitOrderUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + dadRothAccount.investorID + "/orders";
            dadRothAccount.notesOwnedByAccount = GetLoansOwnedFromJson(RetrieveJsonString(dadRothAccount.detailedNotesOwnedUrl, dadRothAccount.authorizationToken));
            dadRothAccount.loanIDsOwned = (from loan in dadRothAccount.notesOwnedByAccount.myNotes.AsEnumerable()
                                              select loan.loanId).ToList();

            activeAccounts.Add(dadRothAccount);

            return activeAccounts;
        }
    }
}
