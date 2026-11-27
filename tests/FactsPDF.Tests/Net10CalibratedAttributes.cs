using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace FactsPDF.Tests;

/// <summary>
/// Marks a check that is authoritative on .NET 10 only and skips it on the library's older targets. It
/// also tags the test with its category, so a run can select or exclude the whole group.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public abstract class Net10CalibratedAttribute(string category, string reason) : NUnitAttribute, IApplyToTest
{
    public void ApplyToTest(Test test)
    {
        test.Properties.Add(PropertyNames.Category, category);
#if !NET10_0_OR_GREATER
        if (test.RunState is RunState.NotRunnable) return;
        test.RunState = RunState.Ignored;
        test.Properties.Set(PropertyNames.SkipReason, reason);
#else
        _ = reason;
#endif
    }
}

/// <summary>
/// An allocation budget or object-size measurement. Budgets are tuned on the .NET 10 runtime and only
/// ever lowered; older runtimes allocate differently, so the budget is not checked there.
/// </summary>
public sealed class AllocationBudgetAttribute() : Net10CalibratedAttribute(
    "AllocationBudget", "Allocation budgets are measured and enforced on .NET 10 only.");

/// <summary>
/// A hash pin over PDF bytes that include compressed streams. Each .NET version ships its own zlib build,
/// so compressed bytes, and the hash, are only pinned on .NET 10.
/// </summary>
public sealed class CompressedBytesPinAttribute() : Net10CalibratedAttribute(
    "CompressedBytesPin", "Compressed PDF bytes are pinned on .NET 10 only; other runtimes ship a different zlib.");
