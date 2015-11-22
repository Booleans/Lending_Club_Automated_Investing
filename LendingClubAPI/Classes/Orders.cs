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
}

public class OrderResponse {

}
