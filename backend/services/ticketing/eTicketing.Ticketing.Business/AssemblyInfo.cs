using System.Runtime.CompilerServices;

// Lets the test project observe internals it has no other way to reach — currently just
// TicketPrintRenderer's diagnostic hook for which thread rendered the last PDF (see its
// LastGeneratePdfThreadId remarks). Nothing here is meant to be usable production surface for any
// other assembly.
[assembly: InternalsVisibleTo("eTicketing.Ticketing.Business.Tests")]
