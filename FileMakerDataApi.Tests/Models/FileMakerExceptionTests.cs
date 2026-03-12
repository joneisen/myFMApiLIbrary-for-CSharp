using FileMakerDataApi.Models;

namespace FileMakerDataApi.Tests.Models;

public class FileMakerExceptionTests
{
    [Fact]
    public void Constructor_SetsMessage()
    {
        var ex = new FileMakerException("Something went wrong.");
        Assert.Equal("Something went wrong.", ex.Message);
    }

    [Fact]
    public void Constructor_DefaultErrorCodesAreZero()
    {
        var ex = new FileMakerException("msg");
        Assert.Equal(0, ex.FileMakerErrorCode);
        Assert.Equal(0, ex.HttpStatusCode);
    }

    [Fact]
    public void Constructor_SetsFileMakerErrorCode()
    {
        var ex = new FileMakerException("No records.", 401, 200);
        Assert.Equal(401, ex.FileMakerErrorCode);
    }

    [Fact]
    public void Constructor_SetsHttpStatusCode()
    {
        var ex = new FileMakerException("Server error.", 0, 500);
        Assert.Equal(500, ex.HttpStatusCode);
    }

    [Fact]
    public void IsException_CanBeCaughtAsException()
    {
        void Throw() => throw new FileMakerException("test");
        Assert.Throws<FileMakerException>(Throw);
    }

    [Fact]
    public void IsException_InheritsFromException()
    {
        var ex = new FileMakerException("msg");
        Assert.IsAssignableFrom<Exception>(ex);
    }
}
