using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using LendingClubAPI.Classes;
using Newtonsoft.Json;

namespace LendingClubAPI
{
    class Program
    {
        static void Main(string[] args)
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
            // Total outstanding principal of account. Used to get value each state should be limited to.
            double outstandingPrincipal = myAccount.outstandingPrincipal;
            // Limit for a state is 3% of total outstanding principal.
            double statePrincipalLimit = .03*outstandingPrincipal;

            // List of notes I own. Used to determine which states I should invest in. 
            NotesOwned myNotesOwned = getLoansOwnedFromJson(RetrieveJsonString(detailedNotesOwnedUrl));

            // Retrieve the latest offering of loans on the platform.
            NewLoans latestListedLoans = getNewLoansFromJson(RetrieveJsonString(latestLoansUrl));
            Console.WriteLine(latestListedLoans.loans[0].grade);
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
    }
}
