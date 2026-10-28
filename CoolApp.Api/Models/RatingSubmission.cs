using System.ComponentModel.DataAnnotations;

namespace CoolApp.Models;

// The request shape for submitting a rating: validated at the API boundary with the
// same [Range] pattern as everywhere else in this controller, so a bad request still
// gets a clean 400 with a field-level message - rather than relying on Rating's own
// constructor guard (further down the call stack) to reject it with a raw exception.
public record RatingSubmission([Range(1, 5)] int Value);
