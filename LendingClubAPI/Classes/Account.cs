using System.Collections.Generic;
using LendingClubAPI.Classes;

// Class needed to get available cash balance

namespace LendingClubAPI
{
    public class Account
    {
        // Properties from Lending Club
        public string investorID { get; set; }
        public double availableCash { get; set; }
        public double outstandingPrincipal { get; set; }
        public double accountTotal { get; set; }

        // Properties and/or methods I've added. 
        public double statePercentLimit { get; set; } = 0.05;
        public string authorizationTokenFilePath { get; set; }
        public string notesFromCSVFilePath { get; set; }
        public double amountToInvestPerLoan { get; set; } = 25;
        public string authorizationToken { get; set; }
        public string[] loanGradesAllowed { get; set; } = { "A", "B", "C", "D", "E", "F", "G" };
        public bool getAllLoans { get; set; } = true;
        public List<int> loanIDsOwned { get; set; }
        public string[] allowedStates { get; set; } = {
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
        public int numberOfLoansToInvestIn { get; set; }
        public string detailedNotesOwnedUrl { get; set; }
        public string accountSummaryUrl { get; set; }
        public string submitOrderUrl { get; set; }
        public NotesOwned notesOwnedByAccount { get; set; }
        public double? minimumInterestRate { get; set; } = 0.0;
        public double? maximumInterestRate { get; set; } = 99.9;
        public int? minimumAnnualIncome { get; set; } = 0;
        public double? maximumRevolvingBalance { get; set; } = 999999;
        public string[] allowedHomeOwnership { get; set; } = { "RENT", "OWN", "MORTGAGE", "OTHER" };
        public string accountTitle { get; set; }
        public int[] loanTermsAllowed { get; set; } = { 36, 60 };
        public int? maxInqLast6Months { get; set; } = 99;
        public int? maxPublicRecordsAllowed { get; set; } = 0;
        public string[] loanPurposesAllowed { get; set; } =         {
            "debt_consolidation", "medical","home_improvement", "renewable_energy", "small_business",
            "wedding", "vacation", "moving", "house", "car", "major_purchase", "credit_card", "other"
        };
        public int maxDelinqLast2Years { get; set; } = 0;
        public string[] statesToExclude { get; set; } = {};
    }
}
