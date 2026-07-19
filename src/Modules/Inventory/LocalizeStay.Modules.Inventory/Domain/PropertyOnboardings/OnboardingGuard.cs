using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal static class OnboardingGuard
{
    public static void EnsureNotClosed(OnboardingLifecycleStatus status)
    {
        if (status == OnboardingLifecycleStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                "Onboarding is closed and cannot be modified.",
                "ONBOARDING_CLOSED");
        }
    }

    public static void EnsureCanProgress(OnboardingLifecycleStatus status)
    {
        if (status != OnboardingLifecycleStatus.InProgress
            && status != OnboardingLifecycleStatus.ReturnedByCuration)
        {
            throw new BusinessRuleViolationException(
                "Onboarding must be in progress to perform this operation.",
                "ONBOARDING_NOT_IN_PROGRESS");
        }
    }

    public static void EnsureSubmittedToCuration(OnboardingLifecycleStatus status)
    {
        if (status != OnboardingLifecycleStatus.SubmittedToCuration)
        {
            throw new BusinessRuleViolationException(
                "Curation return can only be recorded for a submitted onboarding.",
                "ONBOARDING_NOT_SUBMITTED");
        }
    }
}
