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
        static string accountNumber;
        static string projectDirectory;
        static string authorizationTokenFilePath;
        static string allowedStatesCSVFilePath;
        static double amountToInvest;
        static string[] loanGradesAllowed;
        static string latestLoansUrl;
        static string detailedNotesOwnedUrl;
        static string accountSummaryUrl;
        static string submitOrderUrl;
        static NotesOwned myNotesOwned;
        static string authorizationToken;
        static double accountBalance;
        static string notesFromCSVFilePath;
        public static string[] stateAbbreviations;
        public static string[] allowedStates;
        public static double totalAccountValue;

        private static void Main(string[] args)
        {
            //********************************************************************************************************************************//
            //********************************************************************************************************************************//

            // Find the directory of the project so we can use a relative path to the authorization token file. 
            projectDirectory = Directory.GetParent(Directory.GetParent(Directory.GetCurrentDirectory()).ToString()).ToString();

            // Location of the file that stores the account's authorization token.
            authorizationTokenFilePath = projectDirectory + @"\AndrewAuthorizationToken.txt";
            
            // Location of the file used to determine which states can be invested in.
            allowedStatesCSVFilePath = projectDirectory + @"\AllowedStates.csv";

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
            latestLoansUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing";

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

            // Call calculateAndSetAllowedStatesFromCSV function to set the allowed states.
            calculateAndSetAllowedStatesFromCSV(notesFromCSVFilePath);

            // Use a stopwatch to terminate code after a certain duration.
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Read authorization token stored in text file.
            authorizationToken = File.ReadAllText(authorizationTokenFilePath);

            // Store the Account object to get balance and outstanding principal.
            Account myAccount = new Account();
            myAccount = getAccountFromJson(RetrieveJsonString(accountSummaryUrl, authorizationToken));

            // Get the total value of the account. 
            totalAccountValue = myAccount.accountTotal;

            // Variable for storing cash balance available.
            accountBalance = myAccount.accountTotal;

            // We only need to search for loans if we have at least $25 to buy one. 
            // if (accountBalance >= amountToInvest)
            if(accountBalance >= 0)
            {
                int numberOfLoansToBuy = (int) (accountBalance / amountToInvest);

                // Retrieve list of notes owned to create a list of loan ID values.
                myNotesOwned = getLoansOwnedFromJson(RetrieveJsonString(detailedNotesOwnedUrl, authorizationToken));

                List <int> loanIDsOwned = (from loan in myNotesOwned.myNotes.AsEnumerable()
                                             select loan.loanId).ToList();
                         
                while (stopwatch.ElapsedMilliseconds < 120000 && accountBalance >= 0)
                {
                    // Retrieve the latest offering of loans on the platform.
                    NewLoans latestListedLoans = getNewLoansFromJson(RetrieveJsonString(latestLoansUrl, authorizationToken));

                    // Need to programatically figure out allowed states.
                    allowedStates = stateAbbreviations;

                    // Filter the new loans based off of my criteria. 
                    var filteredLoans = filterNewLoans(latestListedLoans.loans, numberOfLoansToBuy, allowedStates, loanIDsOwned, loanGradesAllowed);

                    // Create a new order to purchase the filtered loans. 
                    Order order = new Order();
                    order = BuildOrder(filteredLoans, amountToInvest);
                    
                    string output = JsonConvert.SerializeObject(order);

                    var orderResponse = JsonConvert.DeserializeObject<CompleteOrderConfirmation>(submitOrder(submitOrderUrl, output, authorizationToken));
                    
                    var orderConfirmations = orderResponse.orderConfirmations.AsEnumerable();

                    var loansPurchased = (from confirmation in orderConfirmations
                                          where confirmation.investedAmount >= 0
                                          select confirmation.loanId);

                    // Add purchased loans to the list of loan IDs owned. 
                    foreach (int l in loansPurchased)
                    {
                        loanIDsOwned.Add(l);
                    }

                    // Subtract successfully invested loans from account balance.
                    accountBalance -= loansPurchased.Count() * amountToInvest;
                }

                Console.ReadLine();
            }
        }

        public static string RetrieveJsonString(string myURL, string AuthToken)
        {
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(myURL);
            // Read authorization token from file.
            wrGETURL.Headers.Add("Authorization:" + AuthToken);
            wrGETURL.ContentType = "applicaton/json; charset=utf-8";

            Stream objStream;
            objStream = wrGETURL.GetResponse().GetResponseStream();
            //Variable for storing total portfolio value limit
            StreamReader objReader = new StreamReader(objStream);

            using (StreamReader reader = new StreamReader(objStream))
            {
                // Return a string of the JSON.
                return reader.ReadToEnd();
            }

        }

        // Method to convert JSON into account balance.
        public static Account getAccountFromJson(string inputJson)
        {
            Account accountDetails = JsonConvert.DeserializeObject<Account>(inputJson);
            return accountDetails;
        }

        public static NotesOwned getLoansOwnedFromJson(string inputJson)
        {
            NotesOwned notesOwned = JsonConvert.DeserializeObject<NotesOwned>(inputJson);
            return notesOwned;
        }

        public static NewLoans getNewLoansFromJson(string inputJson)
        {
            NewLoans newLoans = JsonConvert.DeserializeObject<NewLoans>(inputJson);
            return newLoans;
        }

        public static IEnumerable<Loan> filterNewLoans(List<Loan> newLoans, int numberOfLoansToInvestIn, string[] allowedStates, List<int> loanIDsOwned, string[] gradesAllowed)
        {

            var filteredLoans = (from l in newLoans
                                 where l.annualInc >= 60000 &&
                                //(l.purpose == "debt_consolidation" || l.purpose == "credit_card") &&
                                //(l.inqLast6Mths == 0) &&
                                //(l.intRate >= 12.0) &&
                                //(l.intRate <= 18.0) &&
                                (l.term == 36) &&
                                gradesAllowed.Contains(l.grade) &&                                 
                                //(l.mthsSinceLastDelinq == null) &&
                                //(l.loanAmount < 1.1*l.revolBal) &&
                                //(l.loanAmount > .9*l.revolBal) &&
                                (allowedStates.Contains(l.addrState.ToString())) &&
                                (!loanIDsOwned.Contains(l.id))
                                 orderby l.intRate descending
                                 select l).Take(3);
                                // Comment out for testing.
                                //select l).Take(numberOfLoansToInvestIn);
            
            return filteredLoans;
        }

        public static Order BuildOrder(IEnumerable<Loan> loansToBuy, double amountToInvest)
        {
            Order order = new Order();
            order.aid = (Int32.Parse(accountNumber));
            List<LoanForOrder> loansToOrder = new List<LoanForOrder>();

            foreach (Loan loan in loansToBuy)
            {
                LoanForOrder buyLoan = new LoanForOrder();
                buyLoan.loanId = loan.id;
                buyLoan.requestedAmount = 25.0;
                buyLoan.portfolioId = null;
                
                loansToOrder.Add(buyLoan);
            }

            order.orders = loansToOrder;
            return order;
        }

        public static string submitOrder(string postURL, string jsonToSubmit, string AuthToken)
        {
            
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(postURL);
            httpWebRequest.Headers.Add("Authorization:" + AuthToken);
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

        public static void calculateAndSetAllowedStatesFromCSV(string CSVInputpath)
        {
            string allowedStatesFromCSV = File.ReadAllText(CSVInputpath);
            char[] delimiters = new char[] { '\r', '\n' };
            string[] allowedStates = allowedStatesFromCSV.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, double> states = new Dictionary<string, double>();

            foreach (string note in allowedStates.Skip(1))
            {
                string stateOfNote = null;
                double principalRemainingOfNote = 0;

                // Need a boolean to filter out notes that aren't current. 
                bool isNoteCurrent = false;

                var noteDetails = note.Split(',');

                // We need to make sure we are only using loans that are current. 
                foreach (var detail in noteDetails)
                {
                    // This needs to be changed to "Current" when done testing.
                    if (detail == "Fully Paid") {
                        isNoteCurrent = true;
                        break;
                    }
                }

                // If the note is current we want to find out the index po
                if (isNoteCurrent) {
                    for (int i = 0; i < noteDetails.Length; i++) {
                        if (stateAbbreviations.Contains(noteDetails[i])) {
                            stateOfNote = noteDetails[i];
                        }                        
                    }

                    principalRemainingOfNote = Double.Parse(noteDetails[10]);

                    // If the state has already been added to the dictionary...
                    if (states.ContainsKey(stateOfNote))
                    {
                        // Increase the principal value in that state.
                        states[stateOfNote] += Math.Round(principalRemainingOfNote, 2);
                    }
                    else
                    {
                        // Add state and its principal value for this note.
                        states.Add(stateOfNote, Math.Round(principalRemainingOfNote));
                    }                 
                }
            }

            // Get the total outstanding principal out of all states.
            // double outstandingPrincipal = states.Sum(x => x.Value);

            // Sort the states in alphabetical order.
            // Change <= .03 to < .03 when testing has concluded.
            var sortedStates = from k in states
                                where (k.Value <= .03 * totalAccountValue)
                                orderby k.Key
                                select k.Key;

            // Set the allowedStates variable to the result of the query.
            allowedStates = sortedStates.ToArray();
        }
    }
}
