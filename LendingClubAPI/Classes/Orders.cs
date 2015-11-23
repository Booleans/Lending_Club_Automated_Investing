using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LendingClubAPI.Classes
{
    public class LoanForOrder
    {
        public int loanId { get; set; }
        public double requestedAmount { get; set; }
        public int? portfolioId { get; set; }
    }

    public class Order
    {
        public int aid { get; set; }
        public List<LoanForOrder> orders { get; set; }
    }


    public class OrderConfirmation
    {
        public int loanId { get; set; }
        public double requestedAmount { get; set; }
        public double investedAmount { get; set; }
        public List<string> executionStatus { get; set; }
    }

    public class CompleteOrderConfirmation
    {
        public object orderInstructId { get; set; }
        public List<OrderConfirmation> orderConfirmations { get; set; }
    }
}