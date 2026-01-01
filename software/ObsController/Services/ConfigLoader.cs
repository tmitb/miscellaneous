using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using ObsController.Models;

namespace ObsController.Services;

/// <summary>
/// Loads the JSON mapping file (mapping.json) and provides a strongly‑typed Mapping instance.
/// The loader also allows overriding values via command‑line arguments later in Program.cs.
/// </summary>
public static class ConfigLoader
{
    private const string DefaultMappingFile = "mapping.json"; // file name of the mapping setting.

    /// <summary>
    /// Reads the mapping file and returns a Mapping object. Throws if the file cannot be parsed.
    /// </summary>
    public static Mapping Load(string customPath = null)
    {
        var path = customPath ?? GetDefaultMapping(); 
        if (!File.Exists(path))
            throw new FileNotFoundException($"Mapping file not found: {path}");

        var json = File.ReadAllText(path);
        try
        {
            var mapping = JsonConvert.DeserializeObject<Mapping>(json) ?? new Mapping();
            return mapping;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse mapping file '{path}'.", ex);
        }
    }

    private static string GetDefaultMapping()
    {
        string codeBase = Assembly.GetExecutingAssembly().Location;
        
        return Path.Combine(Path.GetDirectoryName(codeBase), DefaultMappingFile);
    }

    /// <summary>
    /// Applies command‑line overrides onto a loaded Mapping instance.
    /// </summary>
    public static void ApplyOverrides(Mapping mapping, string host, int? port, string password)
    {
        if (!string.IsNullOrWhiteSpace(host))
            mapping.Host = host;
        if (port.HasValue && port > 0)
            mapping.Port = port.Value;
        if (!string.IsNullOrEmpty(password))
            mapping.Password = password;
    }
}
