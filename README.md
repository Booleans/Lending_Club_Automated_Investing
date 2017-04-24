# Automated Personal Loan Investing

LendingClub Corporation, together with its subsidiaries, operates as an online marketplace that connects borrowers and investors in the United States.
This code allows the user to automate the purchase of personal loans as an investment. Lending Club adds new loans to the platform at 9 AM, 1 PM, 5 PM, and 9 PM EST each day.
The demand for loans has greatly outpaced supply, resulting in loans being funded in under 60 seconds. Users who attempt to log into the website
and invest manually may find that they are unable to purchase their desired loans quickly enough. This code allows the user to filter and purchase loans
immediately as they come on the platform.

## Getting Started

These instructions will get you a copy of the project up and running on your local machine for development and testing purposes. See deployment for notes on how to deploy the project on a live system.

### Prerequisities

Install the latest version of Visual Studio Community Edition 2015. This will install the associated .NET frameworks necessary to run the code.
Visual Studio Community Edition can be downloaded for free from Microsoft:
https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx

### Installing

Download the project onto your PC and open the Program.cs file. At the end of the file, there is a method named ```InstantiateAccounts```. This
method contains the variables to edit. 

The default account object is named ```dadRothAccount```. You may change this to something else if you'd like. 

Locate your API key on Lending Club's website. You may need to request API access before you will be given a key. Once you have it,
save the key to a txt file saved on your machine. Then change your account's ```AuthorizationTokenFilePath``` variable to the location of the txt file.

The account contains properties you can assign in order to customize the loans you filter and purchase. Set the property ```.amountToInvestPerLoan``` to the amount of money
you would like to invest per loan. This should be in increments of $25. 

You can assign the following properties to the account object in order to filter and purchase loans based off of custom criteria:

minimumInterestRate, minimumAnnualIncome, maxInquiriesLast6Months, loanPurposesAllowed, maximumRevolvingBalance, loanTermsAllowed,
allowedHomeOwnership, and loanGradesAllowed.

The code will then filter and purchase loans based off the criteria you have provided in the above properties. 

## Deployment

Use Windows Task Scheduler to run the project's executable at 9 AM, 1 PM, 5 PM, and 9 PM EST each day.

## Built With

* Visual Studio Community Edition 2015
* C#

## Authors

* **Andrew Nicholls** - *Initial work* - [Github](https://github.com/Booleans)
