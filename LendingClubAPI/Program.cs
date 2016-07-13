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
        // We need default values for the loan selection theory. If a property is not explicitly set for an account the code will use these defaults.
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
        public static string[] allPossibleLoanGrades =  {"A", "B", "C", "D", "E", "F", "G"};
        public static int[] allLoanTerms = {36, 60};
        public static string[] allHomeOwnership = {"RENT", "OWN", "MORTGAGE", "OTHER"};
        public static string[] allLoanPurposes =
        {
            "debt_consolidation", "medical","home_improvement", "renewable_energy", "small_business",
            "wedding", "vacation", "moving", "house", "car", "major_purchase", "credit_card", "other"
        };

        private static void Main(string[] args)
        {
            var activeAccounts = InstantiateAccounts();

            // Stopwatch is necessary to terminate code if new loans are not found after a set time period.
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var millisecondsUntilTermination = 120000;

            // Multiple accounts can access the API so we might as well run them in parallel instead of waiting for one account after the other.
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

                while (stopwatch.ElapsedMilliseconds < millisecondsUntilTermination && investableAccount.availableCash >= investableAccount.amountToInvestPerLoan)
                {
                    // If this is the first time retrieving latest listed loans, retrieve all.
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

                    NewLoans latestListedLoans = GetNewLoansFromJson(RetrieveJsonString(latestLoansUrl, investableAccount.authorizationToken));

                    // If there are no newly listed loans then we do not need to continue and filter them.
                    if (latestListedLoans.loans == null)
                    {
                        continue;
                    }

                    // We need to filter the new loans to see which ones pass the account's screening criteria. 
                    var filteredLoans = FilterNewLoans(latestListedLoans.loans, investableAccount);

                    // There's no need to move on to building an order if there were no loans that passed the screening filter.
                    if (!filteredLoans.Any())
                    {
                        // Lending Club limited the API to one request per second so we need a delay before trying again to find new loans.
                        Thread.Sleep(1000);
                        continue;
                    }
 
                    Order order = BuildOrder(filteredLoans, investableAccount.amountToInvestPerLoan, investableAccount.investorID);

                    string output = JsonConvert.SerializeObject(order);

                    var orderResponse = JsonConvert.DeserializeObject<CompleteOrderConfirmation>(SubmitOrder(investableAccount.submitOrderUrl, output, investableAccount.authorizationToken));

                    var orderConfirmations = orderResponse.orderConfirmations.AsEnumerable();

                    // Collecting the loan IDs of purchased loans so we can prevent them from being invested in again on the next iteration of the loop.
                    var loansPurchased = (from confirmation in orderConfirmations
                                          where confirmation.investedAmount >= 0
                                          select confirmation.loanId);

                    // We want to notify the user of any loans were successfully purchased. 
                    if (loansPurchased.Any())
                    {
                        foreach (var loan in loansPurchased)
                        {
                            Console.WriteLine("The account {0} purchased loan ID: {1} at {2}", investableAccount.accountTitle, loan, DateTime.Now.ToLongTimeString());
                        } 
                    }
 
                    // Updating the account to avoid purchasing loans that have already been purchased. 
                    investableAccount.loanIDsOwned.AddRange(loansPurchased);

                    // Update the amount of available cash. Loop will then terminate if the available cash is less than the amount needed to invest in a loan.
                    investableAccount.availableCash -= loansPurchased.Count() * investableAccount.amountToInvestPerLoan;
                }

            });
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

        // Need to parse the API response to get the details of the account. This will inform us of the amount of cash available to invest. 
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

        // Need to parse the API response to figure out which loans are newly listed.
        public static NewLoans GetNewLoansFromJson(string inputJson)
        {
            NewLoans newLoans = JsonConvert.DeserializeObject<NewLoans>(inputJson);
            return newLoans;
        }

        // Not all users want to invest in every possible loan. Filtering allows each account to use unique criteria. 
        public static IEnumerable<Loan> FilterNewLoans(List<Loan> newLoans, Account accountToUse)
        {

            var filteredLoans = (from l in newLoans
                                 where 
                                 (l.annualInc >= (accountToUse.minimumAnnualIncome ?? 0)) &&
                                 ((accountToUse.loanPurposesAllowed ?? allLoanPurposes).Contains(l.purpose)) &&
                                 (l.inqLast6Mths <= (accountToUse.maxInqLast6Months ?? 99)) &&
                                 (l.pubRec <= (accountToUse.maxPublicRecordsAllowed ?? 0)) &&
                                 (l.collections12MthsExMed == 0 || l.collections12MthsExMed == null) &&
                                 (l.intRate >= (accountToUse.minimumInterestRate ?? 0)) &&
                                 (l.intRate <= (accountToUse.maximumInterestRate ?? 99)) &&
                                 ((accountToUse.loanTermsAllowed ?? allLoanTerms).Contains(l.term)) &&
                                 ((accountToUse.loanGradesAllowed ?? allPossibleLoanGrades).Contains(l.grade)) &&
                                 (l.mthsSinceLastDelinq == null) &&
                                 (l.empLength != null) &&
                                 (l.revolBal <= (accountToUse.maximumRevolvingBalance ?? 999999)) &&
                                 (l.delinq2Yrs == null || l.delinq2Yrs == 0) &&
                                 ((accountToUse.allowedHomeOwnership ?? allHomeOwnership).Contains(l.homeOwnership)) &&
                                 ((accountToUse.allowedStates ?? stateAbbreviations).Contains(l.addrState)) &&
                                 (!accountToUse.loanIDsOwned.Contains(l.id))
                                 // Users want to select the highest interest rate possible from the loans that match their criteria. 
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
            // Method is necessary to prevent geographic risk. Without it each account will be loaded with loans from California, Texas, New York, and Florida.
            string allowedStatesFromCSV = File.ReadAllText(CSVInputpath);
            char[] delimiters = new char[] { '\r', '\n' };
            string[] allowedStates = allowedStatesFromCSV.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, double> states = stateAbbreviations.ToDictionary(state => state, state => 0.0);

            // First line needs to be skipped because it contains column headings.  
            foreach (string note in allowedStates.Skip(1))
            {
              
                double principalRemainingOfNote = 0;

                var noteDetails = note.Split(',');

                // Only loans that are current are relevant for calculating total outstanding principal invested.
                // Any loan not current is considered a complete loss of remaining principal balance. 
                bool isNoteCurrent = noteDetails.Any(detail => detail == "Current");

                if (!isNoteCurrent) continue;

                // We are calculating principal outstanding in each state so we need to determine which state this loan was issued in.
                string stateOfNote = noteDetails.First(detail => stateAbbreviations.Contains(detail));

                principalRemainingOfNote = Double.Parse(noteDetails[10]);

                // Need to keep track of total principal outstanding in each state in order to filter later on.
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
            // Need to crate a list of active accounts if we are going to be running this code on multiple accounts.
            List<Account> activeAccounts = new List<Account>();

            // Find the directory of the project so we can use a relative path to the authorization token file. 
            string projectDirectory = Directory.GetParent(Directory.GetParent(Directory.GetCurrentDirectory()).ToString()).ToString();

            const string dadRothAuthorizationTokenFilePath = @"C:\DadRothAuthorizationToken.txt";
            var dadRothAuthorizationToken = File.ReadAllText(dadRothAuthorizationTokenFilePath);

            Account dadRothAccount = GetAccountFromJson(RetrieveJsonString("https://api.lendingclub.com/api/investor/v1/accounts/77100250/summary", dadRothAuthorizationToken));

            dadRothAccount.accountTitle = "Dad's Roth Account";
            dadRothAccount.authorizationToken = dadRothAuthorizationToken;
            dadRothAccount.statePercentLimit = 0.05;
            dadRothAccount.amountToInvestPerLoan = 50.0;
            dadRothAccount.minimumInterestRate = 6.5;
            dadRothAccount.minimumAnnualIncome = 42000;
            dadRothAccount.maxInqLast6Months = 0;
            dadRothAccount.loanPurposesAllowed = new string[] {"debt_consolidation", "credit_card"};
            //dadRothAccount.maximumRevolvingBalance = 15000;
            dadRothAccount.loanTermsAllowed = new int[] {36};
            dadRothAccount.allowedHomeOwnership = new string[] {"MORTGAGE", "OWN"};
            dadRothAccount.loanGradesAllowed = new string[] { "A", "B", "C"};
            dadRothAccount.notesFromCSVFilePath = projectDirectory + @"\notes_ext.csv";
            dadRothAccount.allowedStates = stateAbbreviations;
            dadRothAccount.allowedStates = CalculateAndSetAllowedStatesFromCsv(dadRothAccount.notesFromCSVFilePath, dadRothAccount.statePercentLimit, dadRothAccount.accountTotal);
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
