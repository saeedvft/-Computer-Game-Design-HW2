using System;

namespace HW2
{
    /// <summary>
    /// Marks a serialized reference a component can go without, because the part it points at is not
    /// always built: a speed strip has no percent readout, and a title flickers without being told which
    /// graphic it owns. <c>HW2/Verify Scene Setup</c> audits every other empty reference on a game
    /// component, and an optional part that was left out is a decision rather than a mistake.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalReferenceAttribute : Attribute
    {
    }
}
