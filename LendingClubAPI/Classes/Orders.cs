using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LendingClubAPI.Classes
{
public class Order
{
    public int loanId { get; set; }
    public double requestedAmount { get; set; }
    public int portfolioId { get; set; }
}

public class RootObject
{
    public int aid { get; set; }
    public List<Order> orders { get; set; }
}
}
