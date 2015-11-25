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
        public string investorID { get; set; }
        public double availableCash { get; set; }
        public double outstandingPrincipal { get; set; }
        public double accountTotal { get; set; }

    }
}
