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
            
            // Url to retrieve account summary, which contains value of outstanding princippal
            var accountSummaryUrl = "https://api.lendingclub.com/api/investor/v1/accounts/1302864/summary";

            // Url to submit a request to buy loans
            var submitOrderUrl = "https://api.lendingclub.com/api/investor/v1/accounts/1302864/orders";

            //Variable for storing total portfolio value limit

            var accountBalance = 0.0;

            //Console.WriteLine(RetrieveJsonString(accountSummaryUrl));
            string typeOfObject = "Account";
            var myAccount = new Account();
            Deserialize_Json(RetrieveJsonString(accountSummaryUrl), myAccount);

        }

    
        public static string RetrieveJsonString(string myURL)
        {
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(myURL);
            wrGETURL.Headers.Add("Authorization:NOT REAL AUTHENTICATION");
            wrGETURL.ContentType = "applicaton/json; charset=utf-8";

            Stream objStream;
            objStream = wrGETURL.GetResponse().GetResponseStream();
            //Variable for storing total portfolio value limit
            StreamReader objReader = new StreamReader(objStream);

            RootObject publicFeed = new RootObject();

            using (StreamReader reader = new StreamReader(objStream))
            {
                // Return a string of the JSON
                return reader.ReadToEnd();
            }

        }

        public static object Deserialize_Json(string inputJson, object baseObject)
        {
            object objectType = (baseObject.GetType());
            
            //string data = reader.ReadToEnd();
            Account accountDetails = JsonConvert.DeserializeObject<Account>(inputJson);
            
            return accountDetails;
            //List<Loan> newLoans = publicFeed.loans;
            //foreach (Loan test in newLoans) { Console.WriteLine(test.term); }

            //var myVariable = newLoans.Sum(loan => .03 * loan.loanAmount);
        }
    }
}
