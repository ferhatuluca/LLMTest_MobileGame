using MonstersVsZombies.Combat.Attacks;
using MonstersVsZombies.Combat.Interaction;
using UnityEngine;

namespace MonstersVsZombies.Tests.EditMode
{
    public sealed class StepSevenAttackResultPolicy : MonoBehaviour,
        IAttackResultPolicy
    {
        public int SuccessfulInteractionCount { get; private set; }
        public AttackExecutionContext LastExecutionContext { get; private set; }
        public InteractionResult LastInteractionResult { get; private set; }

        public void HandleSuccessfulInteraction(
            AttackExecutionContext executionContext,
            InteractionResult interactionResult)
        {
            SuccessfulInteractionCount++;
            LastExecutionContext = executionContext;
            LastInteractionResult = interactionResult;
        }
    }
}
