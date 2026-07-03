using FourDotnet.BoogaBooster.Core;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Verifies the lifecycle-state plumbing in the Core <see cref="DomainModel"/>
/// base class (ADR-0003) via a minimal test double.
/// </summary>
public class DomainModelStateTests
{
    private sealed class Sample : DomainModel
    {
        private string _name;

        public Sample(string name, bool isNew)
            : base(isNew)
        {
            _name = name;
        }

        public string Name => _name;

        public bool SetName(string value) => ApplyChange(ref _name, value);

        public void Remove() => MarkDeleted();
    }

    [Fact]
    public void NewModel_StartsNew()
    {
        var model = new Sample("a", isNew: true);
        Assert.Equal(DomainModelState.New, model.State);
    }

    [Fact]
    public void RehydratedModel_StartsPristine()
    {
        var model = new Sample("a", isNew: false);
        Assert.Equal(DomainModelState.Pristine, model.State);
    }

    [Fact]
    public void SetX_OnPristine_WithRealChange_BecomesModified()
    {
        var model = new Sample("a", isNew: false);

        var changed = model.SetName("b");

        Assert.True(changed);
        Assert.Equal("b", model.Name);
        Assert.Equal(DomainModelState.Modified, model.State);
    }

    [Fact]
    public void SetX_OnPristine_WithNoOp_BecomesTouched()
    {
        var model = new Sample("a", isNew: false);

        var changed = model.SetName("a");

        Assert.False(changed);
        Assert.Equal(DomainModelState.Touched, model.State);
    }

    [Fact]
    public void SetX_OnNew_StaysNew()
    {
        var model = new Sample("a", isNew: true);

        model.SetName("b");

        Assert.Equal(DomainModelState.New, model.State);
    }

    [Fact]
    public void Delete_MarksDeleted()
    {
        var model = new Sample("a", isNew: false);

        model.Remove();

        Assert.Equal(DomainModelState.Deleted, model.State);
    }
}
