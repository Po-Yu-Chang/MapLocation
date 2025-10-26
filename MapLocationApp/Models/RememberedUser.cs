namespace MapLocationApp.Models
{
    /// <summary>
    /// Represents a remembered user with secure password storage using BCrypt hashing.
    /// T012: Implements BCrypt.Net-Next for secure password hashing (replaces plaintext storage)
    /// </summary>
    public class RememberedUser
    {
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// BCrypt hashed password. Never store plaintext passwords.
        /// Use SetPassword() to hash and VerifyPassword() to validate.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Legacy property for backward compatibility. DO NOT USE for new code.
        /// This property is deprecated and will be removed in future versions.
        /// </summary>
        [Obsolete("Use PasswordHash property with SetPassword() and VerifyPassword() methods instead.")]
        public string Password
        {
            get => string.Empty; // Always return empty for security
            set => SetPassword(value); // Auto-hash on set for backward compatibility
        }

        public bool RememberMe { get; set; } = false;
        public DateTime LastRememberedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Sets the password by hashing it with BCrypt (work factor: 12)
        /// T012: BCrypt hashing with salt rounds = 12 for security
        /// </summary>
        /// <param name="plainTextPassword">The plaintext password to hash</param>
        public void SetPassword(string plainTextPassword)
        {
            if (string.IsNullOrWhiteSpace(plainTextPassword))
            {
                throw new ArgumentException("Password cannot be null or empty.", nameof(plainTextPassword));
            }

            // BCrypt with work factor 12 (2^12 = 4096 iterations, ~0.3s on modern CPU)
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainTextPassword, workFactor: 12);
        }

        /// <summary>
        /// Verifies the provided password against the stored BCrypt hash
        /// T012: Secure password verification using BCrypt.Verify
        /// </summary>
        /// <param name="plainTextPassword">The plaintext password to verify</param>
        /// <returns>True if password matches, false otherwise</returns>
        public bool VerifyPassword(string plainTextPassword)
        {
            if (string.IsNullOrWhiteSpace(plainTextPassword) || string.IsNullOrWhiteSpace(PasswordHash))
            {
                return false;
            }

            try
            {
                return BCrypt.Net.BCrypt.Verify(plainTextPassword, PasswordHash);
            }
            catch
            {
                // Invalid hash format or verification error
                return false;
            }
        }
    }
}