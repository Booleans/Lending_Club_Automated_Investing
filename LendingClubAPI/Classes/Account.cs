using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public double statePercentLimit { get; set; }
        public string authorizationTokenFilePath { get; set; }
        public string notesFromCSVFilePath { get; set; }
        public double amountToInvestPerLoan { get; set; }
        public string authorizationToken { get; set; }
        public string[] loanGradesAllowed { get; set; }
        public bool getAllLoans { get; set; } = true;
        public List<int> loanIDsOwned { get; set; }
        public string[] allowedStates { get; set; }

    }
}
