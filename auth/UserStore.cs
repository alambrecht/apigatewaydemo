using Microsoft.AspNetCore.Identity;

record User(string Id, string Username, string[] Roles, string PasswordHash = "");

/// <summary>
/// In-memory user list for learning. In a real system this would be a database
/// table (e.g. SQL Server via ASP.NET Core Identity + EF Core).
/// Passwords are never stored in plain text: PasswordHasher uses PBKDF2 with a salt.
/// </summary>
sealed class UserStore
{
    private readonly PasswordHasher<User> _hasher = new();
    private readonly Dictionary<string, User> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly User _dummy;

    public UserStore()
    {
        Add("6f1c2a9e-0000-4000-8000-000000000001", "alice", "alice", ["reader"]);
        Add("6f1c2a9e-0000-4000-8000-000000000002", "bob",   "bob",   ["reader", "admin"]);

        // Used to burn the same CPU time when the username doesn't exist,
        // so response timing doesn't reveal which usernames are real.
        var d = new User("", "", []);
        _dummy = d with { PasswordHash = _hasher.HashPassword(d, Guid.NewGuid().ToString()) };
    }

    private void Add(string id, string username, string password, string[] roles)
    {
        var user = new User(id, username, roles);
        _users[username] = user with { PasswordHash = _hasher.HashPassword(user, password) };
    }

    public User? Validate(string? username, string? password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return null;

        if (!_users.TryGetValue(username, out var user))
        {
            _hasher.VerifyHashedPassword(_dummy, _dummy.PasswordHash, password);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }
}
