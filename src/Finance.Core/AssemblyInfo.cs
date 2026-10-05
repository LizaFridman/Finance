using System.Runtime.CompilerServices;

// Lets tests exercise UtilityBillPdfParser.ParseBillText directly (a pure
// function kept internal since it's not part of the IRawFeedParser contract).
[assembly: InternalsVisibleTo("Finance.Tests")]
