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
        public static string accountNumber;
        public static string projectDirectory;
        public static string authorizationTokenFilePath;
        public static string allowedStatesCSVFilePath;
        public static double amountToInvest;
        public static string[] loanGradesAllowed;
        public static string latestLoansUrl;
        public static string detailedNotesOwnedUrl;
        public static string accountSummaryUrl;
        public static string submitOrderUrl;
        public static NotesOwned myNotesOwned;
        public static string authorizationToken;
        public static double accountBalance;
        public static string notesFromCSVFilePath;
        public static string[] stateAbbreviations;
        public static string[] allowedStates;
        public static double totalAccountValue;
        public static bool getAllLoans;
        public static double statePercentLimit;

        private static void Main(string[] args)
        {
            //********************************************************************************************************************************//
            //********************************************************************************************************************************//

            // Boolean to change the get listed loans URL. After we've retrieved all loans, only retrieve new. 
            getAllLoans = true;

            // Limit outstanding principal in each state to no more than this % of the portfolio value.
            statePercentLimit = .05;

            // Find the directory of the project so we can use a relative path to the authorization token file. 
            projectDirectory = Directory.GetParent(Directory.GetParent(Directory.GetCurrentDirectory()).ToString()).ToString();

            // Location of the file that stores the account's authorization token.
            // authorizationTokenFilePath = projectDirectory + @"\AndrewAuthorizationToken.txt";
            authorizationTokenFilePath = @"C:\AndrewAuthorizationToken.txt";

            // File path of the CSV file downloaded from Lending Club.
            // This data will be used to create the list of allowed states.
            notesFromCSVFilePath = projectDirectory + @"\notes_ext.csv";

            // Account number that you want the code to run on.
            accountNumber = "1302864";            

            // How much should be invested per loan? Must be an increment of $25. 
            amountToInvest = 25.0;

            // Loan grades that you are willing to invest in.
            loanGradesAllowed = new string[] { "B", "C", "D" };

            // Url for retrieving the latest listing of loans
            latestLoansUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing?showAll=true";

            // Url to retrieve the detailed list of notes owned
            detailedNotesOwnedUrl = "https://api.lendingclub.com/api/investor/v1/accounts/"+ accountNumber +"/detailednotes";

            // Url to retrieve account summary, which contains value of outstanding principal
            accountSummaryUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + accountNumber + "/summary";

            // Url to submit a request to buy loans
            submitOrderUrl = "https://api.lendingclub.com/api/investor/v1/accounts/" + accountNumber + "/orders";
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

            // Call CalculateAndSetAllowedStatesFromCsv function to set the allowed states.
            allowedStates = CalculateAndSetAllowedStatesFromCsv(notesFromCSVFilePath);

            // Use a stopwatch to terminate code after a certain duration.
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Read authorization token stored in text file.
            authorizationToken = File.ReadAllText(authorizationTokenFilePath);

            // Store the Account object to get balance and outstanding principal.
            Account myAccount = GetAccountFromJson(RetrieveJsonString(accountSummaryUrl));

            // Get the total value of the account. 
            totalAccountValue = myAccount.accountTotal;

            // Variable for storing cash balance available.
            accountBalance = myAccount.availableCash;

            // We only need to search for loans if the available balance >= minimum investment amount. 
            if (accountBalance < amountToInvest)
            {
                return;
            }

            var numberOfLoansToBuy = (int) (accountBalance / amountToInvest);

            // Retrieve list of notes owned to create a list of loan ID values.
            myNotesOwned = GetLoansOwnedFromJson(RetrieveJsonString(detailedNotesOwnedUrl));

            List <int> loanIDsOwned = (from loan in myNotesOwned.myNotes.AsEnumerable()
                                       select loan.loanId).ToList();
                         
            while (stopwatch.ElapsedMilliseconds < 120000 && accountBalance >= 0)
            {
                // If this is the first time retrieving listed loans, retrieve all.
                // Retrieve only new loans for subsequent loops. 
                if (getAllLoans)
                {
                    latestLoansUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing?showAll=true";
                    getAllLoans = false;
                }
                else
                {
                    latestLoansUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing?showAll=false";
                }

                // Retrieve the latest offering of loans on the platform.
                NewLoans latestListedLoans = GetNewLoansFromJson(RetrieveJsonString(latestLoansUrl));

                // Filter the new loans based off of my criteria. 
                var filteredLoans = FilterNewLoans(latestListedLoans.loans, numberOfLoansToBuy, allowedStates, loanIDsOwned, loanGradesAllowed);

                // We only need to build an order if filteredLoan is not null.
                if (!filteredLoans.Any())
                {
                    // Wait one second before retrieving loans again if there are no loans passing the filter. 
                    Thread.Sleep(1000);
                    continue;
                }

                // Create a new order to purchase the filtered loans. 
                Order order = new Order();
                order = BuildOrder(filteredLoans, amountToInvest);

                string output = JsonConvert.SerializeObject(order);

                var orderResponse = JsonConvert.DeserializeObject<CompleteOrderConfirmation>(SubmitOrder(submitOrderUrl, output));

                var orderConfirmations = orderResponse.orderConfirmations.AsEnumerable();

                var loansPurchased = (from confirmation in orderConfirmations
                    where confirmation.investedAmount >= 0
                    select confirmation.loanId);

                // Add purchased loans to the list of loan IDs owned. 
                loanIDsOwned.AddRange(loansPurchased);

                // Subtract successfully invested loans from account balance.
                accountBalance -= loansPurchased.Count() * amountToInvest;
            }

            Console.ReadLine();
        }

        public static string RetrieveJsonString(string myURL)
        {
            WebRequest wrGETURL = WebRequest.Create(myURL);
 
            // Read authorization token from file.
            wrGETURL.Headers.Add("Authorization:" + authorizationToken);
            wrGETURL.ContentType = "applicaton/json; charset=utf-8";

            var objStream = wrGETURL.GetResponse().GetResponseStream();
            //Variable for storing total portfolio value limit
            StreamReader objReader = new StreamReader(objStream);

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

        public static IEnumerable<Loan> FilterNewLoans(List<Loan> newLoans, int numberOfLoansToInvestIn, string[] allowedStates, List<int> loanIDsOwned, string[] gradesAllowed)
        {

            var filteredLoans = (from l in newLoans
                                 where l.annualInc >= 59900 &&
                                 (l.purpose == "debt_consolidation" || l.purpose == "credit_card") &&
                                 (l.inqLast6Mths == 0) &&
                                 (l.intRate >= 10.0) &&
                                 //(l.intRate <= 18.0) &&
                                 (l.term == 36) &&
                                 (gradesAllowed.Contains(l.grade)) &&
                                 (l.mthsSinceLastDelinq == null) &&
                                 (l.loanAmount <= 1.1*l.revolBal) &&
                                 (l.loanAmount >= .9*l.revolBal) &&
                                 (allowedStates.Contains(l.addrState.ToString())) &&
                                 (!loanIDsOwned.Contains(l.id))
                                 orderby l.intRate descending                                                            
                                 select l).Take(numberOfLoansToInvestIn);
            
            return filteredLoans;
        }

        public static Order BuildOrder(IEnumerable<Loan> loansToBuy, double amountToInvest)
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

        public static string SubmitOrder(string postURL, string jsonToSubmit)
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

        public static string[] CalculateAndSetAllowedStatesFromCsv(string CSVInputpath)
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

        public static void DownloadNotesOwnedCsv()
        {

            var webClient = new WebClient
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential("andrewsnicholls@gmail.com", "testPassword")
            };

            webClient.DownloadFile("https://www.lendingclub.com/account/notesRawDataExtended.csv", @"C:\notes.csv");

        }
    }
}
