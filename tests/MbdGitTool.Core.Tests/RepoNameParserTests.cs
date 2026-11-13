using MbdGitTool.Core.Domain;

namespace MbdGitTool.Core.Tests;

public class RepoNameParserTests
{
    [Theory]
    [InlineData("CF123A_ShortName_Design", "CF123A_ShortName", RepoType.Design)]
    [InlineData("CF325F_ChanganSigProcgBascFct_Impl", "CF325F_ChanganSigProcgBascFct", RepoType.Impl)]
    [InlineData("CF325F_ChanganSigProcgBascFct_MIL", "CF325F_ChanganSigProcgBascFct", RepoType.Mil)]
    [InlineData("CF325F_ChanganSigProcgBascFct_SIL", "CF325F_ChanganSigProcgBascFct", RepoType.Sil)]
    [InlineData("A_B_C_MIL", "A_B_C", RepoType.Mil)]
    [InlineData("foo_impl", "foo", RepoType.Impl)]
    [InlineData("Bar_DeSiGn", "Bar", RepoType.Design)]
    [InlineData("Baz_sil", "Baz", RepoType.Sil)]
    public void TryParse_ValidNames_ReturnsComponentAndType(
        string fullName,
        string expectedComponentId,
        RepoType expectedType)
    {
        var ok = RepoNameParser.TryParse(fullName, out var componentId, out var repoType);

        Assert.True(ok);
        Assert.Equal(expectedComponentId, componentId);
        Assert.Equal(expectedType, repoType);
    }

    [Theory]
    [InlineData("X_SIL_Design")] // 前缀以生命周期词结尾：不得把 X_SIL 误当组件
    [InlineData("X_Design_Impl")]
    [InlineData("X_MIL_SIL")]
    [InlineData("_Design")] // 空组件 ID
    [InlineData("CF123A_Design2")] // 非法后缀
    [InlineData("X_Docs")] // 非四类后缀
    [InlineData("CF325F")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_InvalidNames_ReturnsFalse(string? fullName)
    {
        var ok = RepoNameParser.TryParse(fullName, out var componentId, out var repoType);

        Assert.False(ok);
        Assert.Equal(string.Empty, componentId);
        Assert.Equal(RepoType.Other, repoType);
    }

    [Fact]
    public void Parse_InvalidName_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => RepoNameParser.Parse("X_SIL_Design"));
    }

    [Fact]
    public void TryParse_DoesNotStripMultipleLifecycleWords()
    {
        // 只有最后一个生命周期后缀被剥离；前缀再以生命周期词结尾则整体解析失败（SRS §6.1）。
        Assert.False(RepoNameParser.TryParse("Team_SIL_Impl", out _, out _));
        Assert.False(RepoNameParser.TryParse("Team_SIL_Design", out _, out _));
    }
}
