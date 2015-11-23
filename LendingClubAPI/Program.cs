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
        private static void Main(string[] args)
        {
            // Use a stopwatch to terminate code after a certain duration.
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Read authorization token stored in text file.
            string authorizationToken = File.ReadAllText(@"C:\Users\andre_000\Documents\GitHub\Lending_Club_API\AndrewAuthorizationToken.txt");

            // Url for retrieving the latest listing of loans
            var latestLoansUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing";

            // Url to retrieve the detailed list of notes owned
            var detailedNotesOwnedUrl = "https://api.lendingclub.com/api/investor/v1/accounts/1302864/detailednotes";

            // Url to retrieve account summary, which contains value of outstanding principal
            var accountSummaryUrl = "https://api.lendingclub.com/api/investor/v1/accounts/1302864/summary";

            // Url to submit a request to buy loans
            var submitOrderUrl = "https://api.lendingclub.com/api/investor/v1/accounts/1302864/orders";

            // Store the Account object to get balance and outstanding principal.
            Account myAccount = new Account();
            myAccount = getAccountFromJson(RetrieveJsonString(accountSummaryUrl, authorizationToken));

            // Variable for storing cash balance available.
            double accountBalance = myAccount.availableCash;

            double amountToInvest = 25.0;

            // We only need to search for loans if we have at least $25 to buy one. 
            //if (accountBalance >= 25)
            if(accountBalance >= 0)
            {
  
                int numberOfLoansToBuy = (int) (accountBalance/25);

                // Retrieve list of notes owned to create a list of loan ID values.
                NotesOwned myNotesOwned = getLoansOwnedFromJson(RetrieveJsonString(detailedNotesOwnedUrl, authorizationToken));

                List<int> loanIDsOwned = (from loan in myNotesOwned.myNotes.AsEnumerable()
                                          select loan.loanId).ToList();

                while (stopwatch.ElapsedMilliseconds < 120000 && accountBalance >= 0)
                {
                    // Retrieve the latest offering of loans on the platform.
                    NewLoans latestListedLoans = getNewLoansFromJson(RetrieveJsonString(latestLoansUrl, authorizationToken));

                    // Need to programatically figure out allowed states.
                    string[] allowedStates = {
                    "AK","AL","AR","AZ","CT",
                    "DC","DE","FL","HI","IA",
                    "ID","IN","KS","KY","LA",
                    "MD","ME","MN","MO","MS",
                    "MT","ND","NH","NM","NV",
                    "OK","OR","RI","SC","SD",
                    "TN","UT","VT","WI","WV",
                    "WY","CA","TX","NY" };

                    // Filter the new loans based off of my criteria. 
                    var filteredLoans = filterNewLoans(latestListedLoans.loans, numberOfLoansToBuy, allowedStates, loanIDsOwned);

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

        public static IEnumerable<Loan> filterNewLoans(List<Loan> newLoans, int numberOfLoansToInvestIn, string[] allowedStates, List<int> loanIDsOwned)
        {

            var filteredLoans = (from l in newLoans
                                 where l.annualInc >= 60000 &&
                                //(l.purpose == "debt_consolidation" || l.purpose == "credit_card") &&
                                //(l.inqLast6Mths == 0) &&
                                //(l.intRate >= 12.0) &&
                                //(l.intRate <= 18.0) &&
                                (l.term == 36) &&
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
            order.aid = 1302864;
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
    }
}
