using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;

namespace StepH_GameEventScript_Tests.Compiler;

[TestClass]
public sealed class GesBinaryBuilderTests
{
    [TestMethod]
    public void BuildDeduplicatesTextConstantsAndRetargetsSingleUseTemporaryMove()
    {
        var builder = new GesBinaryBuilder().WithModuleName("BuilderSmoke");
        var left = builder.AddRegister("left");
        var right = builder.AddRegister("right");
        var result = builder.AddRegister("result");
        var textA = builder.AddRegister("textA");
        var textB = builder.AddRegister("textB");
        var temp = builder.AddTemporaryRegister();

        var binary = builder
            .LoadInteger(left, 10)
            .LoadInteger(right, 20)
            .LoadText(textA, "same")
            .LoadText(textB, "same")
            .Multiply(temp, left, right)
            .Move(result, temp)
            .Build();

        Assert.AreEqual("BuilderSmoke", binary.ModuleName);
        Assert.HasCount(1, binary.TextConstantTable.Slices);
        Assert.AreEqual("same", binary.TextConstantTable.Resolve(0));
        Assert.HasCount(5, binary.InstructionTable);

        var multiply = binary.InstructionTable[4];
        Assert.AreEqual(GameEventScriptBytecodeOpCode.Multiply, multiply.OpCode);
        Assert.AreEqual((ushort)2, multiply.DestinationSlot);
        Assert.AreEqual((ushort)0, multiply.XSlot);
        Assert.AreEqual((ushort)1, multiply.YSlot);
    }
}
