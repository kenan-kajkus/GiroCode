using FluentAssertions;
using GiroCode;

namespace GiroCodeTests;

/// <summary>
/// Giro code generator test
/// </summary>
public class GiroCodeGeneratorTest
{
    private readonly GiroCodeGenerator _sut;

    public GiroCodeGeneratorTest()
    {
        _sut = new GiroCodeGenerator();
    }
    
    /// <summary>
    /// Tests if giro code can be generated
    /// </summary>
    [Fact]
    public async Task GenerateGiroCode()
    {
        var generateGiroCode = _sut.GenerateGiroCode(
            "Kenan",
            "DE74500105176879856947",
            "Test subject",
            1.44M,
            "INGDDEFFXXX");

        // Remove comment to see giro code local
        /*
        var path = $@"{AppDomain.CurrentDomain.BaseDirectory}test.png";
        await using (var stream = new FileStream(
                         path,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None))
        {
            await stream.WriteAsync(generateGiroCode);
            await stream.FlushAsync();
            stream.Close();
        }
        */
        _ = generateGiroCode.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateGiroCoded_ShouldNotSuccess_WhenExceeds331Bytes()
    {
        var rem = "";
        for (var i = 0; i < 180; i++)
        {
            rem += "ä";
        }

        var a =  () => _sut.GenerateGiroCode(
            "Kenan",
            "DE74500105176879856947",
            rem,
            1.44M,
            "INGDDEFFXXX");
        
        var exceptionAssertions = a.Should().Throw<Exception>();
        exceptionAssertions.WithMessage("QrCodeCreationFailed");
    }
}
