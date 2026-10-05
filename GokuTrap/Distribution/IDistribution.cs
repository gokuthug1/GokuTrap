using GokuTrap.AppData;

namespace GokuTrap.Distribution
{
    public interface IDistribution
    {
        public string RobloxDomain { get; }
        public Dictionary<string, int> CdnUrls { get; }
        public IAppData RobloxPlayerData { get; }
        public IAppData RobloxStudioData { get; }
    }
}
