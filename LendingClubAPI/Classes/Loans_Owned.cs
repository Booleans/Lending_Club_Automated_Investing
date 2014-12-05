using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LendingClubAPI.Classes
{
    public class MyNote
    {
        public int loanId { get; set; }
        public int noteId { get; set; }
        public int orderId { get; set; }
        public double interestRate { get; set; }
        public int loanLength { get; set; }
        public string loanStatus { get; set; }
        public string grade { get; set; }
        public int loanAmount { get; set; }
        public int noteAmount { get; set; }
        public double paymentsReceived { get; set; }
        public string issueDate { get; set; }
        public string orderDate { get; set; }
        public string loanStatusDate { get; set; }
    }

    public class NotesOwned
    {
        public List<MyNote> myNotes { get; set; }
    }
}
