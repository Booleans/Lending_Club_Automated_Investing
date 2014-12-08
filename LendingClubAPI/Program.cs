using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using LendingClubAPI.Classes;
using Newtonsoft.Json;

namespace LendingClubAPI
{
    internal class Program
    {
        private static void Main(string[] args)
        {
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
            myAccount = getAccountFromJson(RetrieveJsonString(accountSummaryUrl));
            // Variable for storing cash balance available.
            double accountBalance = myAccount.availableCash;
            int numberOfLoansToBuy = (int)(accountBalance/25);
            // Total outstanding principal of account. Used to get value each state should be limited to.
            //double outstandingPrincipal = myAccount.outstandingPrincipal;
            // Limit for a state is 3% of total outstanding principal.
            //double statePrincipalLimit = .03*outstandingPrincipal;

            // List of notes I own. Used to determine which states I should invest in. 
            //NotesOwned myNotesOwned = getLoansOwnedFromJson(RetrieveJsonString(detailedNotesOwnedUrl));

            // Retrieve the latest offering of loans on the platform.
            NewLoans latestListedLoans = getNewLoansFromJson(RetrieveJsonString(latestLoansUrl));

            // Filter the new loans based off of my criteria. 
            var filteredLoans = filterNewLoans(latestListedLoans.loans,numberOfLoansToBuy);


             Order order = new Order();
             order = BuildOrder(filteredLoans);


            foreach (Loan loan in (filteredLoans))
            {
                
                Console.WriteLine((loan.intRate-loan.serviceFeeRate - loan.expDefaultRate ));
            }

            string output = JsonConvert.SerializeObject(order);
            submitOrder(submitOrderUrl,output);
            Console.Read();
        }


        public static string RetrieveJsonString(string myURL)
        {
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(myURL);
            wrGETURL.Headers.Add("Authorization:cPSkXgXlJI1G6X6cDzWCN5FX8uY=");
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

        public static IEnumerable<Loan> filterNewLoans(List<Loan> newLoans, int numberOfLoansToInvestIn)
        {   // Array of states to invest in. Have to calculate this manually by downloading spreadsheet.
            string[] allowedStates =
            {
                "AK","AL","AR","AZ","CT",
                "DC","DE","FL","HI","IA",
                "ID","IN","KS","KY","LA",
                "MD","ME","MN","MO","MS",
                "MT","ND","NH","NM","NV",
                "OK","OR","RI","SC","SD",
                "TN","UT","VT","WI","WV",
                "WY"
            };

            var filteredLoans = (from l in newLoans
                                 where l.annualInc >= 60000 &&
                                (l.purpose == "debt_consolidation" || l.purpose == "credit_card") &&
                                (l.inqLast6Mths == 0) &&
                                (l.intRate - l.expDefaultRate - l.serviceFeeRate) > 9.0 &&
                                (l.mthsSinceLastDelinq == null) &&
                                (l.loanAmount < 1.02*l.revolBal) &&
                                (allowedStates.Contains(l.addrState.ToString()))
                                 orderby l.intRate descending 
                                 select l).Take(numberOfLoansToInvestIn);

            return filteredLoans;
        }

        public static Order BuildOrder(IEnumerable<Loan> loansToBuy)
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

        public static string submitOrder(string postURL, string jsonToSubmit)
        {
            
        var httpWebRequest = (HttpWebRequest)WebRequest.Create(postURL);
        httpWebRequest.Headers.Add("Authorization:cPSkXgXlJI1G6X6cDzWCN5FX8uY=");
        httpWebRequest.ContentType = "application/json; charset=utf-8";
        httpWebRequest.Method = "POST";            

        using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
        {
            string json = jsonToSubmit;

            streamWriter.Write(json);
            // Line maybe not needed.
            // streamWriter.Flush();
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
