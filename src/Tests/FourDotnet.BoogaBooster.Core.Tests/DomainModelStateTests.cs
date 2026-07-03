using FourDotnet.BoogaBooster.Core;
using Xunit;

namespace FourDotnet.BoogaBooster.Core.Tests;

/// <summary>
/// Verifies the lifecycle-state plumbing in the <see cref="DomainModel"/> base
/// class (ADR-0003) via a minimal test double.
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

        public bool SetNameCaseInsensitive(string value)
            => ApplyChange(ref _name, value, StringComparer.OrdinalIgnoreCase);

        public void TouchWithoutChange() => MarkChanged(false);

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

    [Fact]
    public void ApplyChange_WithCustomComparer_TreatsEqualValuesAsNoOp()
    {
        var model = new Sample("Rain", isNew: false);

        // Same word, different casing — the ordinal-ignore-case comparer treats it
        // as unchanged, so the value is not reassigned and the model is only touched.
        var changed = model.SetNameCaseInsensitive("RAIN");

        Assert.False(changed);
        Assert.Equal("Rain", model.Name);
        Assert.Equal(DomainModelState.Touched, model.State);
    }

    [Fact]
    public void MarkChanged_False_OnModified_KeepsModified()
    {
        var model = new Sample("a", isNew: false);
        model.SetName("b"); // -> Modified

        model.TouchWithoutChange();

        Assert.Equal(DomainModelState.Modified, model.State);
    }

    [Fact]
    public void ChangeAfterDelete_StaysDeleted()
    {
        var model = new Sample("a", isNew: false);
        model.Remove();

        // Deleted is terminal for change tracking: a later SetX() must not revive it.
        var changed = model.SetName("b");

        Assert.True(changed);
        Assert.Equal(DomainModelState.Deleted, model.State);
    }

    [Fact]
    public void SecondRealChange_OnModified_StaysModified()
    {
        var model = new Sample("a", isNew: false);
        model.SetName("b");
        model.SetName("c");

        Assert.Equal("c", model.Name);
        Assert.Equal(DomainModelState.Modified, model.State);
    }
}
