namespace Domain.Exceptions;

public class InvalidScoreException : DomainException
{
    public InvalidScoreException(string message) : base(message) { }
}
