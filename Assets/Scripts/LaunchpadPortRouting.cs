using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>Pure routing rules shared by Windows discovery and hardware-free tests.</summary>
public static class LaunchpadPortRouting
{
    public sealed class Port
    {
        public uint Id;
        public string Name;
        public string Interface;
        public string Model => ModelOf(Name);
        public string PhysicalKey => PhysicalInterface(Interface);
        public string DeviceKey => PhysicalKey.Length > 0 ? Model + ":" + PhysicalKey : "port:" + Id + ":" + CanonicalName(Name);
    }

    public static string ModelOf(string name)
    {
        string compact = Regex.Replace(name ?? "", @"\s+", "").ToUpperInvariant();
        if (compact.Contains("LAUNCHPADMK2")) return "MK2";
        if (compact.Contains("LAUNCHPADX") || compact.Contains("LPX")) return "X";
        return "";
    }

    public static string PhysicalInterface(string path)
    {
        string value = (path ?? "").Trim().ToUpperInvariant();
        int suffix = value.IndexOf("#{", StringComparison.Ordinal);
        return suffix >= 0 ? value.Substring(0, suffix) : value;
    }

    public static string CanonicalName(string name)
    {
        string value = (name ?? "").ToUpperInvariant();
        value = value.Replace("MIDIOUT", "MIDIPORT").Replace("MIDIIN", "MIDIPORT");
        value = Regex.Replace(value, @"\b(IN|OUT)\b", "");
        return Regex.Replace(value, @"\s+", "");
    }

    private static int Preference(Port port)
    {
        if (port.Model.Length == 0 || (port.Name ?? "").IndexOf("DAW", StringComparison.OrdinalIgnoreCase) >= 0) return -1;
        if (port.Model == "X" && Regex.IsMatch(port.Name, @"MIDI(IN|OUT)2\b", RegexOptions.IgnoreCase)) return 30;
        if (port.Model == "X" && (port.Name ?? "").IndexOf("MIDI", StringComparison.OrdinalIgnoreCase) >= 0) return 20;
        return 10;
    }

    public static List<Port> SelectPorts(IEnumerable<Port> ports)
    {
        var selected = new Dictionary<string, Port>(StringComparer.Ordinal);
        foreach (Port port in ports)
        {
            int rank = Preference(port);
            if (rank < 0) continue;
            string key = port.DeviceKey;
            if (!selected.TryGetValue(key, out Port old) || rank > Preference(old)) selected[key] = port;
        }
        var result = new List<Port>(selected.Values);
        result.Sort((a, b) => a.Id.CompareTo(b.Id));
        return result;
    }

    // Never choose 'the first output of this model': two identical MK2s may enumerate in reverse order.
    public static Port FindOutput(Port input, IList<Port> inputs, IList<Port> outputs, ISet<uint> claimed)
    {
        Port found = null;
        int best = 0;
        bool ambiguous = false;
        foreach (Port output in outputs)
        {
            if (claimed.Contains(output.Id) || output.Model != input.Model) continue;
            bool bothIdentified = input.PhysicalKey.Length > 0 && output.PhysicalKey.Length > 0;
            if (bothIdentified && input.PhysicalKey != output.PhysicalKey) continue;
            int rank = bothIdentified ? 3 : CanonicalName(input.Name) == CanonicalName(output.Name) ? 2 : 0;
            // A model-only fallback is safe only when there is exactly one input AND output of that model.
            int inCount = 0, outCount = 0;
            foreach (Port p in inputs) if (p.Model == input.Model) inCount++;
            foreach (Port p in outputs) if (p.Model == input.Model) outCount++;
            if (rank == 0 && inCount == 1 && outCount == 1) rank = 1;
            if (!bothIdentified && rank == 2)
            {
                int sameNames = 0;
                foreach (Port p in inputs) if (CanonicalName(p.Name) == CanonicalName(input.Name)) sameNames++;
                if (sameNames > 1) rank = 0;
            }
            if (rank > best) { found = output; best = rank; ambiguous = false; }
            else if (rank > 0 && rank == best) ambiguous = true;
        }
        return ambiguous ? null : found;
    }
}
