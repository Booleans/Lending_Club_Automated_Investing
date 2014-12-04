using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
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
            string sUrl = "https://api.lendingclub.com/api/investor/v1/loans/listing";

            //Variable for storing total portfolio value limit
            double totalPortfolioValueLimit = 0.0;

            HTTP_GET_REQUEST(sUrl,ref totalPortfolioValueLimit);
        }

    
        public static void HTTP_GET_REQUEST(string myURL, ref dynamic myVariable)
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
                string data = reader.ReadToEnd();
                publicFeed = JsonConvert.DeserializeObject<RootObject>(data);

                List<Loan> newLoans = publicFeed.loans;
                foreach (Loan test in newLoans) { Console.WriteLine(test.term); }

                myVariable = newLoans.Sum(loan => .03 * loan.loanAmount);

            }

        }

        public class Investor 
        {
            public string investorID { get; set; }
            public double availableCash { get; set; }
        }

        public class Loan
        {
            public int id { get; set; }
            public int memberId { get; set; }
            public double loanAmount { get; set; }
            public double fundedAmount { get; set; }
            public int term { get; set; }
            public double intRate { get; set; }
            public double expDefaultRate { get; set; }
            public double serviceFeeRate { get; set; }
            public double installment { get; set; }
            public string grade { get; set; }
            public string subGrade { get; set; }
            public int empLength { get; set; }
            public string homeOwnership { get; set; }
            public double annualInc { get; set; }
            public string isIncV { get; set; }
            public string acceptD { get; set; }
            public string expD { get; set; }
            public string listD { get; set; }
            public string creditPullD { get; set; }
            public string reviewStatusD { get; set; }
            public string reviewStatus { get; set; }
            public string desc { get; set; }
            public string purpose { get; set; }
            public string addrZip { get; set; }
            public string addrState { get; set; }
            public string investorCount { get; set; }
            public string ilsExpD { get; set; }
            public string initialListStatus { get; set; }
            public string empTitle { get; set; }
            public string accNowDelinq { get; set; }
            public int accOpenPast24Mths { get; set; }
            public int? bcOpenToBuy { get; set; }
            public double? percentBcGt75 { get; set; }
            public double? bcUtil { get; set; }
            public double dti { get; set; }
            public int? delinq2Yrs { get; set; }
            public double? delinqAmnt { get; set; }
            public string earliestCrLine { get; set; }
            public int ficoRangeLow { get; set; }
            public int ficoRangeHigh { get; set; }
            public int inqLast6Mths { get; set; }
            public int? mthsSinceLastDelinq { get; set; }
            public int? mthsSinceLastRecord { get; set; }
            public int? mthsSinceRecentInq { get; set; }
            public int? mthsSinceRecentRevolDelinq { get; set; }
            public int? mthsSinceRecentBc { get; set; }
            public int mortAcc { get; set; }
            public int openAcc { get; set; }
            public int pubRec { get; set; }
            public int totalBalExMort { get; set; }
            public double revolBal { get; set; }
            public double revolUtil { get; set; }
            public int totalBcLimit { get; set; }
            public int totalAcc { get; set; }
            public int totalIlHighCreditLimit { get; set; }
            public int numRevAccts { get; set; }
            public int? mthsSinceRecentBcDlq { get; set; }
            public int? pubRecBankruptcies { get; set; }
            public int numAcctsEver120Ppd { get; set; }
            public int? chargeoffWithin12Mths { get; set; }
            public int? collections12MthsExMed { get; set; }
            public int? taxLiens { get; set; }
            public int? mthsSinceLastMajorDerog { get; set; }
            public int? numSats { get; set; }
            public int? numTlOpPast12m { get; set; }
            public int? moSinRcntTl { get; set; }
            public int totHiCredLim { get; set; }
            public int totCurBal { get; set; }
            public int avgCurBal { get; set; }
            public int? numBcTl { get; set; }
            public int? numActvBcTl { get; set; }
            public int? numBcSats { get; set; }
            public int pctTlNvrDlq { get; set; }
            public int? numTl90gDpd24m { get; set; }
            public int? numTl30dpd { get; set; }
            public int? numTl120dpd2m { get; set; }
            public int? numIlTl { get; set; }
            public int? moSinOldIlAcct { get; set; }
            public int? numActvRevTl { get; set; }
            public int? moSinOldRevTlOp { get; set; }
            public int? moSinRcntRevTlOp { get; set; }
            public int totalRevHiLim { get; set; }
            public int numRevTlBalGt0 { get; set; }
            public int numOpRevTl { get; set; }
            public int totCollAmt { get; set; }
        }

        public class RootObject
        {
            public String asOfDate { get; set; }
            public List<Loan> loans { get; set; }
        }
    }
}
