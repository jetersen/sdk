// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Tasks;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class SourceDateEpochParserTests
{
    [Theory]
    [InlineData("0", 0L)]
    [InlineData("1636374896", 1636374896L)]
    [InlineData("99999999999", 99999999999L)]
    [InlineData("100000000000", 100000000000L)]
    public void ParseReturnsUtcTimestamp(string value, long expectedSeconds)
    {
        DateTime? actual = SourceDateEpochParser.Parse(value);

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(expectedSeconds).UtcDateTime, actual);
        Assert.Equal(DateTimeKind.Utc, actual!.Value.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("  1636374896\n")]
    [InlineData("not-a-number")]
    [InlineData("1636374896.5")]
    [InlineData("-1")]
    [InlineData("0x10")]
    [InlineData("1,636,374,896")]
    [InlineData("99999999999999999999")]
    [InlineData("253402300800")]
    public void ParseReturnsNullForInvalidValues(string? value)
        => Assert.Null(SourceDateEpochParser.Parse(value));
}
