#nullable disable
using System.Collections.Generic;

namespace UTSTwitchIntegration.Config
{
    public class ConfigValidationResult
    {
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        public bool IsValid => _errors.Count == 0;

        public bool HasWarnings => _warnings.Count > 0;

        public IReadOnlyList<string> Errors => _errors;

        public IReadOnlyList<string> Warnings => _warnings;

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                _errors.Add(error);
            }
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
            {
                _warnings.Add(warning);
            }
        }

        /// <summary>
        /// Formats errors and warnings into a multi-line message suitable for logging
        /// </summary>
        public string GetFormattedMessage()
        {
            List<string> parts = new List<string>();

            if (_errors.Count > 0)
            {
                parts.Add($"Configuration validation failed with {_errors.Count} error(s):");
                foreach (string error in _errors)
                {
                    parts.Add($"  - {error}");
                }
            }

            if (_warnings.Count > 0)
            {
                parts.Add($"Configuration validation produced {_warnings.Count} warning(s):");
                foreach (string warning in _warnings)
                {
                    parts.Add($"  - {warning}");
                }
            }

            return string.Join("\n", parts);
        }

        /// <summary>
        /// Returns a concise single-line summary of validation status
        /// </summary>
        public string GetSummaryMessage()
        {
            if (IsValid && !HasWarnings)
            {
                return "Configuration is valid";
            }

            List<string> parts = new List<string>();
            if (_errors.Count > 0)
            {
                parts.Add($"{_errors.Count} error(s)");
            }
            if (_warnings.Count > 0)
            {
                parts.Add($"{_warnings.Count} warning(s)");
            }

            return $"Configuration validation: {string.Join(", ", parts)}";
        }
    }
}

