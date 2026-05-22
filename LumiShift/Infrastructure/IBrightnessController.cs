namespace LumiShift.Infrastructure
{
    public interface IBrightnessController
    {
        string DeviceId { get; }
        string DisplayName { get; }
        int GetBrightness();
        void SetBrightness(int percent);
        bool IsDDC { get; }
        bool IsSupported { get; }
    }
}