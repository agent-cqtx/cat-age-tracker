namespace CatTracker.Services;

/// <summary>
/// Holds the single password required to make changes. If none is
/// configured via the APP_PASSWORD environment variable, a random one
/// is generated for this run and logged at startup, so the app never
/// silently ships wide open.
/// </summary>
public class PasswordProvider
{
    public string Password { get; }
    public bool WasGenerated { get; }

    public PasswordProvider(IConfiguration config)
    {
        var configured = config["APP_PASSWORD"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            Password = configured;
            WasGenerated = false;
        }
        else
        {
            Password = Guid.NewGuid().ToString("N")[..12];
            WasGenerated = true;
        }
    }
}
