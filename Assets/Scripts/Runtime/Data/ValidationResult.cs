using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonstersVsZombies.Data
{
    public enum ValidationCode
    {
        MissingId,
        MissingDisplayName,
        MissingSocketName,
        MissingReference,
        DuplicateUnitId,
        DuplicatePoolId,
        InvalidFaction,
        InvalidPositiveValue,
        InvalidNonNegativeValue,
        InvalidDeliveryType,
        InvalidStatusEffect,
        InvalidCapacityPolicy,
        IncompatibleDeliveryType,
        MissingProjectileDefinition,
        AttackRangeExceedsChaseRange,
        PrewarmExceedsRetainedCount,
        InvalidActiveLimit
    }

    public readonly struct ValidationIssue
    {
        public ValidationCode Code { get; }
        public string Message { get; }

        public ValidationIssue(ValidationCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }
    }

    public sealed class ValidationResult
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();

        public IReadOnlyList<ValidationIssue> Issues => _issues;
        public bool IsValid => _issues.Count == 0;

        public void AddError(ValidationCode code, string message)
        {
            _issues.Add(new ValidationIssue(code, message));
        }

        public void Merge(ValidationResult other)
        {
            if (other == null)
            {
                return;
            }

            foreach (ValidationIssue issue in other.Issues)
            {
                _issues.Add(issue);
            }
        }

        public bool HasError(ValidationCode code)
        {
            foreach (ValidationIssue issue in _issues)
            {
                if (issue.Code == code)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class ValidationReporter
    {
        public static void Report(UnityEngine.Object context, ValidationResult result)
        {
            if (result == null || result.IsValid)
            {
                return;
            }

            foreach (ValidationIssue issue in result.Issues)
            {
                Debug.LogError($"[{issue.Code}] {issue.Message}", context);
            }
        }
    }

    internal static class NumericValidation
    {
        public static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsNonNegativeFinite(float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
