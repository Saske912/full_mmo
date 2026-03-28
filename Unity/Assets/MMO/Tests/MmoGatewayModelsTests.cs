#if UNITY_EDITOR
using System.Collections.Generic;
using Mmo.Client.Gateway;
using NUnit.Framework;

namespace Mmo.Tests
{
    /// <summary>EditMode-тесты DTO без Newtonsoft: среда иногда не резолвит сборку пакета для отдельного .asmdef.</summary>
    public sealed class MmoGatewayModelsTests
    {
        [Test]
        public void ResolvePreview_Model_HoldsValues()
        {
            var o = new ResolvePreviewResponse
            {
                ResolveX = 1.5,
                ResolveZ = -2,
                Resolved = new ResolvedCellBundle { Found = true, CellId = "c1", GrpcEndpoint = "h:1" },
                LastCell = new LastCellBundle { Found = false },
                CellIdMismatch = false,
            };
            Assert.AreEqual(1.5, o.ResolveX, 0.0001);
            Assert.IsTrue(o.Resolved.Found);
            Assert.AreEqual("c1", o.Resolved.CellId);
            Assert.IsFalse(o.CellIdMismatch);
        }

        [Test]
        public void QuestProgress_NewlyUnlocked_List()
        {
            var o = new QuestProgressResponse
            {
                Ok = true,
                Completed = true,
                Progress = 3,
                TargetProgress = 3,
                NewlyUnlockedQuests = new List<string> { "tutorial_followup", "branch_path_mercenary" },
            };
            Assert.IsTrue(o.Completed);
            Assert.AreEqual(2, o.NewlyUnlockedQuests.Count);
            Assert.Contains("branch_path_mercenary", o.NewlyUnlockedQuests);
        }

        [Test]
        public void ResolvePreview_CellIdMismatch_LastCell_HasResolveCoords()
        {
            var o = new ResolvePreviewResponse
            {
                ResolveX = 10,
                ResolveZ = -20,
                CellIdMismatch = true,
                LastCell = new LastCellBundle
                {
                    Found = true,
                    CellId = "cell_old",
                    ResolveX = 1.25,
                    ResolveZ = -3.5,
                },
                Resolved = new ResolvedCellBundle { Found = true, CellId = "cell_new" },
            };
            Assert.IsTrue(o.CellIdMismatch);
            Assert.IsTrue(o.LastCell.Found);
            Assert.AreEqual(1.25, o.LastCell.ResolveX!.Value, 0.001);
            Assert.AreEqual(-3.5, o.LastCell.ResolveZ!.Value, 0.001);
        }

        [Test]
        public void Session_QuestRow_Prerequisite()
        {
            var o = new SessionResponse
            {
                Token = "x",
                Quests = new List<QuestApiRow>
                {
                    new QuestApiRow
                    {
                        QuestId = "q1",
                        State = "active",
                        Progress = 0,
                        TargetProgress = 2,
                        PrerequisiteQuestId = "p0",
                    },
                },
            };
            Assert.AreEqual(1, o.Quests.Count);
            Assert.AreEqual("p0", o.Quests[0].PrerequisiteQuestId);
        }
    }
}
#endif
