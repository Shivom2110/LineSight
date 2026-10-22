using LineSight.Core.Traceability;

namespace LineSight.Tests.Traceability;

public class SerialNumberTests
{
    [Theory]
    [InlineData("FS-20261022-00001", true)]
    [InlineData("FS-20261231-99999", true)]
    [InlineData("fs-20261022-00001", false)]   // prefix is case-sensitive
    [InlineData("FS-2026102-00001", false)]    // date too short
    [InlineData("FS-20261022-0001", false)]    // sequence too short
    [InlineData("FS-20261341-00001", false)]   // not a real date
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ChecksFormatAndDate(string? serial, bool expected)
    {
        Assert.Equal(expected, SerialNumber.IsValid(serial));
    }

    [Fact]
    public void Create_ProducesAValidZeroPaddedSerial()
    {
        var serial = SerialNumber.Create(new DateOnly(2026, 10, 22), 42);

        Assert.Equal("FS-20261022-00042", serial);
        Assert.True(SerialNumber.IsValid(serial));
    }
}
