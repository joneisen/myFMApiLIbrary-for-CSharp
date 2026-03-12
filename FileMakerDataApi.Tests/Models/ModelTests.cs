using FileMakerDataApi.Models;

namespace FileMakerDataApi.Tests.Models;

public class FindRequestTests
{
    [Fact]
    public void Omit_DefaultsToFalse()
    {
        var req = new FindRequest { Fields = new() { ["Name"] = "Alice" } };
        Assert.False(req.Omit);
    }

    [Fact]
    public void Fields_StoresValues()
    {
        var req = new FindRequest { Fields = new() { ["Name"] = "Alice", ["Age"] = ">18" } };
        Assert.Equal("Alice", req.Fields["Name"]);
        Assert.Equal(">18", req.Fields["Age"]);
    }

    [Fact]
    public void Omit_CanBeSetToTrue()
    {
        var req = new FindRequest { Fields = new() { ["Name"] = "Alice" }, Omit = true };
        Assert.True(req.Omit);
    }
}

public class SortFieldTests
{
    [Fact]
    public void SortOrder_DefaultsToAscend()
    {
        var sf = new SortField { FieldName = "Name" };
        Assert.Equal(SortOrder.Ascend, sf.SortOrder);
    }

    [Fact]
    public void FieldName_StoresValue()
    {
        var sf = new SortField { FieldName = "ModifiedDate" };
        Assert.Equal("ModifiedDate", sf.FieldName);
    }

    [Fact]
    public void SortOrder_CanBeSetToDescend()
    {
        var sf = new SortField { FieldName = "Name", SortOrder = SortOrder.Descend };
        Assert.Equal(SortOrder.Descend, sf.SortOrder);
    }
}

public class PortalOptionsTests
{
    [Fact]
    public void Name_StoresValue()
    {
        var po = new PortalOptions { Name = "LineItems" };
        Assert.Equal("LineItems", po.Name);
    }

    [Fact]
    public void Limit_DefaultsToNull()
    {
        var po = new PortalOptions { Name = "LineItems" };
        Assert.Null(po.Limit);
    }

    [Fact]
    public void Offset_DefaultsToNull()
    {
        var po = new PortalOptions { Name = "LineItems" };
        Assert.Null(po.Offset);
    }

    [Fact]
    public void Limit_CanBeSet()
    {
        var po = new PortalOptions { Name = "LineItems", Limit = 10 };
        Assert.Equal(10, po.Limit);
    }

    [Fact]
    public void Offset_CanBeSet()
    {
        var po = new PortalOptions { Name = "LineItems", Offset = 5 };
        Assert.Equal(5, po.Offset);
    }
}

public class ScriptDefinitionTests
{
    [Fact]
    public void Type_DefaultsToPostRequest()
    {
        var sd = new ScriptDefinition { Name = "MyScript" };
        Assert.Equal(ScriptType.PostRequest, sd.Type);
    }

    [Fact]
    public void Name_StoresValue()
    {
        var sd = new ScriptDefinition { Name = "SendEmail" };
        Assert.Equal("SendEmail", sd.Name);
    }

    [Fact]
    public void Param_DefaultsToNull()
    {
        var sd = new ScriptDefinition { Name = "MyScript" };
        Assert.Null(sd.Param);
    }

    [Fact]
    public void Param_CanBeSet()
    {
        var sd = new ScriptDefinition { Name = "MyScript", Param = "arg1" };
        Assert.Equal("arg1", sd.Param);
    }

    [Fact]
    public void Type_CanBeSetToPreRequest()
    {
        var sd = new ScriptDefinition { Name = "MyScript", Type = ScriptType.PreRequest };
        Assert.Equal(ScriptType.PreRequest, sd.Type);
    }

    [Fact]
    public void Type_CanBeSetToPreSort()
    {
        var sd = new ScriptDefinition { Name = "MyScript", Type = ScriptType.PreSort };
        Assert.Equal(ScriptType.PreSort, sd.Type);
    }
}

public class EnumTests
{
    [Fact]
    public void SortOrder_HasAscendAndDescend()
    {
        Assert.Equal(0, (int)SortOrder.Ascend);
        Assert.Equal(1, (int)SortOrder.Descend);
    }

    [Fact]
    public void DapiVersion_HasExpectedValues()
    {
        var values = Enum.GetValues<DapiVersion>();
        Assert.Contains(DapiVersion.V1,      values);
        Assert.Contains(DapiVersion.V2,      values);
        Assert.Contains(DapiVersion.VLatest, values);
    }

    [Fact]
    public void DateFormat_HasExpectedValues()
    {
        var values = Enum.GetValues<DateFormat>();
        Assert.Contains(DateFormat.Default,    values);
        Assert.Contains(DateFormat.FileLocale, values);
        Assert.Contains(DateFormat.Iso8601,    values);
    }

    [Fact]
    public void ScriptType_HasExpectedValues()
    {
        var values = Enum.GetValues<ScriptType>();
        Assert.Contains(ScriptType.PreRequest,  values);
        Assert.Contains(ScriptType.PreSort,     values);
        Assert.Contains(ScriptType.PostRequest, values);
    }
}
