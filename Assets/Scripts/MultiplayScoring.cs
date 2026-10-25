using System;

/// <summary>Uses integer totals so an all-perfect chart always yields exactly 1,000,000.</summary>
public static class MultiplayScoring
{
    public static int Normalize(long points, long maximumPoints)
    {
        if (maximumPoints <= 0) return 0;
        return (int)(Math.Max(0, Math.Min(points, maximumPoints)) * 1000000L / maximumPoints);
    }

    public static int JudgmentPoints(float deltaSeconds)
    {
        float delta = Math.Abs(deltaSeconds);
        return delta <= 0.06f ? 1000 : delta <= 0.12f ? 800 : delta <= 0.22f ? 500 : 200;
    }
}
