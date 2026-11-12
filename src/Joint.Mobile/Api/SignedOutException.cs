namespace Joint.Mobile.Api;

/// <summary>The session is missing or no longer accepted; the user has to sign in again.</summary>
public class SignedOutException() : Exception("You are signed out.");
