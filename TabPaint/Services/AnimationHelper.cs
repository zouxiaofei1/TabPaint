using System;

namespace TabPaint
{
    public static class AnimationHelper
    {
        public static TimeSpan GetScaledTimeSpan(double baseMilliseconds)
        {
            var multiplier = SettingsManager.Instance.Current.AnimationSpeedMultiplier;
            if (multiplier <= 0)
                multiplier = 0.01;
            return TimeSpan.FromMilliseconds(baseMilliseconds * multiplier);
        }
    }
}
