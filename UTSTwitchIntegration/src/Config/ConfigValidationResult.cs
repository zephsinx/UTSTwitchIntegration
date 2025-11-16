using System.Collections.Generic;

namespace UTSTwitchIntegration.Config
{
    public class ConfigValidationResult
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();

        public bool IsValid => this.errors.Count == 0;

        public bool HasWarnings => this.warnings.Count > 0;

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                this.errors.Add(error);
            }
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
            {
                this.warnings.Add(warning);
            }
        }

        /// <summary>
        /// Formats errors and warnings into a multi-line message suitable for logging
        /// </summary>
        public string GetFormattedMessage()
        {
            List<string> parts = new List<string>();

            if (this.errors.Count > 0)
            {
                parts.Add($"Configuration validation failed with {this.errors.Count} error(s):");
                foreach (string error in this.errors)
                {
                    parts.Add($"  - {error}");
                }
            }

            if (this.warnings.Count <= 0)
                return string.Join("\n", parts);

            parts.Add($"Configuration validation produced {this.warnings.Count} warning(s):");
            foreach (string warning in this.warnings)
            {
                parts.Add($"  - {warning}");
            }

            return string.Join("\n", parts);
        }
    }
}