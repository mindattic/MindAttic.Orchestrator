using System.Text.Json;
using MindAttic.Vault.Credentials;
using MindAttic.Vault.Paths;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// The owner's Vault-held secret values, read once as the gateway starts (M.ORCH-T935) so the masker can hide them as literal
/// strings wherever they show up: the LLM and broker keyrings (each provider's key, its pool and any key, secret, token or
/// password field), every bucket's <c>tokens.json</c> under the Vault's roaming root, and the FTP password. They are kept in
/// memory only, and neither they nor their names are ever logged: only how many were loaded. A store that can't be read is
/// skipped; reading never throws.
/// </summary>
internal static class VaultSecrets
{
    public static IReadOnlyList<string> Load(Action<string>? note = null)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        void Add(string? value)
        {
            var v = value?.Trim();
            if (v is { Length: >= SecretMasker.MinLiteral }) found.Add(v);
        }
        void Try(string what, Action read)
        {
            try { read(); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { note?.Invoke($"couldn't read the Vault's {what} ({ex.GetType().Name}); skipped"); }
        }

        foreach (var (what, store) in new (string, Func<ICredentialStore>)[] { ("LLM keyring", () => LlmCredentialStore.Default), ("broker keyring", () => BrokerCredentialStore.Default) })
        {
            Try(what, () =>
            {
                var s = store();
                foreach (var key in s.LoadAll().Values) Add(key);
                foreach (var raw in s.LoadAllRaw().Values) SecretFields(raw, Add);
            });
        }
        Try("FTP credentials", () => Add(FtpCredentialStore.Default.Get()?.Password));
        Try("token buckets", () =>
        {
            var root = VaultPaths.RoamingRoot;
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                if (!File.Exists(Path.Combine(dir, TokenStore.TokensJsonFile))) continue;
                foreach (var token in new TokenStore(dir).LoadAll().Values) Add(token);
            }
        });
        return [.. found];
    }

    /// <summary>Every string under a key, secret, token or password field of a provider's JSON (its pool's entries too).</summary>
    internal static void SecretFields(string? json, Action<string?> add)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            Walk(doc.RootElement, secret: false, add);
        }
        catch (JsonException) { }
    }

    private static void Walk(JsonElement e, bool secret, Action<string?> add)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject()) Walk(p.Value, !NotSecret(p.Name) && (secret || SecretName(p.Name)), add);
                break;
            case JsonValueKind.Array:
                foreach (var item in e.EnumerateArray()) Walk(item, secret, add);
                break;
            case JsonValueKind.String when secret:
                add(e.GetString());
                break;
        }
    }

    private static bool SecretName(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("key") || n.Contains("secret") || n.Contains("token") || n.Contains("password");
    }

    /// <summary>A label names a key, it isn't one; nor is a model, a URL or a limit.</summary>
    private static bool NotSecret(string name) =>
        name.ToLowerInvariant() is "label" or "name" or "keyname" or "model" or "baseurl" or "url" or "endpoint" or "maxtokens" or "tokenlimit";
}
