using CS.PlasmaLibrary;

namespace CS.UnitTest.PlasmaServer
{
    [TestClass]
    public class State : TestBase
    {
        [TestMethod]
        public void EvenState_Filled()
        {
            // arrange
            DatabaseDefinition definition = new DatabaseDefinition();
            definition.ServerCount = 4;
            definition.ServerCopyCount = 4;
            DatabaseState state = new DatabaseState(definition);

            // act
            state.SetupInitialSlots();
            int[] data = new int[Constant.SlotCount];

            // assert
            Assert.IsFalse(state.Slots.Any(o => o.ServerNumber == Constant.ServerNumberUnfilled));
        }

        [TestMethod]
        public void OddState_Filled()
        {
            // arrange
            DatabaseDefinition definition = new DatabaseDefinition();
            definition.ServerCount = 3;
            definition.ServerCopyCount = 3;
            DatabaseState state = new DatabaseState(definition);

            // act
            state.SetupInitialSlots();
            int[] data = new int[Constant.SlotCount];

            // assert
            Assert.IsFalse(state.Slots.Any(o => o.ServerNumber == Constant.ServerNumberUnfilled));
        }
    }
}
