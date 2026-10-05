using System.Runtime.InteropServices;
using GokuTrap.FeatureServices;

namespace GokuTrap
{
    public sealed class SaiyanPerformanceManager : IDisposable
    {
        private readonly Dictionary<int, OriginalProcessPolicy> _originalPolicies = new();
        private readonly object _gate = new();
        public CpuTopology Topology { get; } = CpuTopology.Inspect();
        public string TopologyDescription => Topology.Description;
        public bool SupportsManualAffinity => Topology.ValidMask != 0;
        public string LastStatus { get; private set; } = FeatureText.Get("WindowsDefaults");

        public static bool IsRobloxPlayer(Process process)
        {
            try
            {
                // A process name alone is not sufficient. Match the installed Player path.
                return process.ProcessName.Equals(Path.GetFileNameWithoutExtension(App.RobloxPlayerAppName), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(process.MainModule?.FileName, App.Distribution.RobloxPlayerData.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException) { return false; }
        }

        public bool ApplyForProcess(int processId)
        {
            if (!App.Settings.Prop.SaiyanMode.Enabled) return false;
            lock (_gate)
            {
                try
                {
                    using Process process = Process.GetProcessById(processId);
                    if (!IsRobloxPlayer(process)) return false;
                    long started = process.StartTime.ToUniversalTime().Ticks;
                    var settings = App.Settings.Prop.SaiyanMode;
                    ulong mask = settings.AutomaticEfficiencySelection ? Topology.FasterMask : settings.AffinityMask ?? 0;
                    if (mask != 0 && (Topology.ValidMask == 0 || (mask & ~Topology.ValidMask) != 0))
                        throw new ArgumentException("Invalid affinity mask");
                    bool priorityRequested = settings.Priority != RobloxPriorityPolicy.WindowsDefault;
                    if (!_originalPolicies.TryGetValue(processId, out var original) || original.StartTicks != started)
                    {
                        _originalPolicies.Remove(processId);
                        // Windows defaults are a real no-op. In particular, never read/write
                        // ProcessorAffinity on grouped systems just to change priority.
                        if (!priorityRequested && mask == 0)
                        { LastStatus = FeatureText.Get("WindowsDefaults"); return true; }
                        _originalPolicies[processId] = original = new(started, process.MainModule!.FileName!, process.PriorityClass);
                    }
                    if (priorityRequested || original.PriorityChanged)
                    {
                        ProcessPriorityClass desired = settings.Priority switch
                        {
                            RobloxPriorityPolicy.High => ProcessPriorityClass.High,
                            RobloxPriorityPolicy.AboveNormal => ProcessPriorityClass.AboveNormal,
                            _ => original.Priority
                        };
                        if (process.PriorityClass != desired)
                        {
                            original.PriorityChanged = true;
                            process.PriorityClass = desired;
                        }
                        if (!priorityRequested) original.PriorityChanged = false;
                    }
                    if (mask != 0 || original.AffinityChanged)
                    {
                        original.Affinity ??= process.ProcessorAffinity;
                        IntPtr desired = mask == 0 ? original.Affinity.Value : (IntPtr)unchecked((long)mask);
                        if (process.ProcessorAffinity != desired)
                        {
                            original.AffinityChanged = true;
                            process.ProcessorAffinity = desired;
                        }
                        if (mask == 0) original.AffinityChanged = false;
                    }
                    LastStatus = string.Format(FeatureText.Get("PolicyApplied"), processId);
                    return true;
                }
                catch (Exception ex) when (IsProcessError(ex))
                {
                    // A partially applied policy must not be left behind on failure.
                    RestoreForProcess(processId);
                    LastStatus = FeatureText.Get("PolicyDenied");
                    App.Logger.WriteLine("SaiyanMode", "Process policy unavailable: " + ex.GetType().Name);
                    return false;
                }
            }
        }

        public int ApplyToRunningRobloxPlayers()
        {
            int count = 0;
            var observed = new HashSet<int>();
            foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(App.RobloxPlayerAppName)))
                using (process) { observed.Add(process.Id); if (ApplyForProcess(process.Id)) count++; }
            lock (_gate)
                foreach (int pid in _originalPolicies.Keys.Where(pid => !observed.Contains(pid)).ToArray()) _originalPolicies.Remove(pid);
            if (count == 0) LastStatus = FeatureText.Get("NoPlayer");
            return count;
        }

        public IReadOnlyList<PlayerMeasurement> Measure()
        {
            var result = new List<PlayerMeasurement>();
            foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(App.RobloxPlayerAppName)))
            {
                using (process)
                {
                    try
                    {
                        if (IsRobloxPlayer(process)) result.Add(new(process.Id, process.StartTime.ToUniversalTime().Ticks, process.WorkingSet64 / 1048576, process.PrivateMemorySize64 / 1048576));
                    }
                    catch (Exception ex) when (IsProcessError(ex)) { }
                }
            }
            return result;
        }

        public bool TrimWorkingSet(PlayerMeasurement target)
        {
            try
            {
                using Process process = Process.GetProcessById(target.ProcessId);
                if (!IsRobloxPlayer(process) || process.StartTime.ToUniversalTime().Ticks != target.StartTicks) return false;
                bool success = NativeOperations.EmptyWorkingSet(process.Handle);
                LastStatus = FeatureText.Get(success ? "TrimSucceeded" : "PolicyDenied");
                return success;
            }
            catch (Exception ex) when (IsProcessError(ex)) { LastStatus = FeatureText.Get("PolicyDenied"); return false; }
        }

        public void RestoreForProcess(int processId)
        {
            lock (_gate)
            {
                if (!_originalPolicies.TryGetValue(processId, out var original)) return;
                try
                {
                    using Process process = Process.GetProcessById(processId);
                    if (process.StartTime.ToUniversalTime().Ticks != original.StartTicks ||
                        !string.Equals(process.MainModule?.FileName, original.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                    { _originalPolicies.Remove(processId); return; }
                    if (original.PriorityChanged) { process.PriorityClass = original.Priority; original.PriorityChanged = false; }
                    if (original.AffinityChanged && original.Affinity is IntPtr affinity)
                    { process.ProcessorAffinity = affinity; original.AffinityChanged = false; }
                    _originalPolicies.Remove(processId);
                }
                catch (ArgumentException) { _originalPolicies.Remove(processId); }
                catch (Exception ex) when (IsProcessError(ex))
                {
                    // Retain the restore journal for retry while the process is still alive.
                    LastStatus = FeatureText.Get("RestoreDenied");
                    App.Logger.WriteLine("SaiyanMode", "Restore unavailable: " + ex.GetType().Name);
                }
            }
        }

        public void RestoreAll()
        {
            lock (_gate)
            {
                foreach (int pid in _originalPolicies.Keys.ToArray()) RestoreForProcess(pid);
                LastStatus = FeatureText.Get(_originalPolicies.Count == 0 ? "WindowsDefaults" : "RestoreDenied");
            }
        }
        private static bool IsProcessError(Exception ex) => ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException or NotSupportedException;
        public void Dispose() => RestoreAll();
        private sealed record OriginalProcessPolicy(long StartTicks, string ExecutablePath, ProcessPriorityClass Priority)
        {
            public IntPtr? Affinity { get; set; }
            public bool PriorityChanged { get; set; }
            public bool AffinityChanged { get; set; }
        }
    }

    public sealed record PlayerMeasurement(int ProcessId, long StartTicks, long WorkingSetMb, long PrivateBytesMb)
    {
        public string Summary => string.Format(FeatureText.Get("MemorySample"), ProcessId, WorkingSetMb, PrivateBytesMb);
    }

    public sealed record CpuTopology(ulong ValidMask, ulong FasterMask, int LogicalCount, int CoreCount, int GroupCount)
    {
        public string Description => string.Format(FeatureText.Get("TopologySummary"), LogicalCount, CoreCount, GroupCount,
            FeatureText.Get(ValidMask == 0 ? "TopologyUnsupported" : FasterMask == 0 ? "TopologyUniform" : "TopologyHeterogeneous"));
        public static CpuTopology Inspect()
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                NativeOperations.GetSystemCpuSetInformation(IntPtr.Zero, 0, out uint length, IntPtr.Zero, 0);
                if (length == 0 || length > 1024 * 1024) return new(0, 0, Environment.ProcessorCount, 0, 0);
                buffer = Marshal.AllocHGlobal((int)length);
                if (!NativeOperations.GetSystemCpuSetInformation(buffer, length, out uint returned, IntPtr.Zero, 0) || returned > length) return new(0, 0, Environment.ProcessorCount, 0, 0);
                var cpus = new List<(ushort group, byte logical, byte core, byte efficiency, byte flags)>();
                for (int offset = 0; offset < returned;)
                {
                    IntPtr p = IntPtr.Add(buffer, offset);
                    int size = Marshal.ReadInt32(p);
                    if (size < 8 || size > returned - offset) return new(0, 0, Environment.ProcessorCount, 0, 0);
                    if (Marshal.ReadInt32(p, 4) == 0 && size >= 32)
                        cpus.Add((unchecked((ushort)Marshal.ReadInt16(p, 12)), Marshal.ReadByte(p, 14), Marshal.ReadByte(p, 15), Marshal.ReadByte(p, 18), Marshal.ReadByte(p, 19)));
                    offset += size;
                }
                int groups = cpus.Select(x => x.group).Distinct().Count();
                ulong valid = 0, faster = 0;
                if (groups == 1 && cpus.All(x => x.group == 0 && x.logical < 64) && cpus.Count > 0)
                {
                    foreach (var cpu in cpus.Where(x => (x.flags & 2) == 0)) valid |= 1UL << cpu.logical;
                    if (cpus.Select(x => x.efficiency).Distinct().Count() > 1)
                        foreach (var cpu in cpus.Where(x => x.efficiency == cpus.Max(y => y.efficiency) && (x.flags & 3) == 0)) faster |= 1UL << cpu.logical;
                }
                return new(valid, faster, cpus.Count, cpus.Select(x => (x.group, x.core)).Distinct().Count(), groups);
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException) { return new(0, 0, Environment.ProcessorCount, 0, 0); }
            finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
        }
    }
}
