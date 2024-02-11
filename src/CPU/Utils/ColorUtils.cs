using SFML.Graphics;
using SFML.System;

public static class ColorUtils
{
    private static Vector3f newColor = new(0.5f, 0.5f, 0.5f);
    private static Vector3f colorDirection = new(-0.04f, 0.03f, 0.02f);
    public static Color NextFadeColor(float speed = 1)
    {
        var tempNewColor = newColor + colorDirection * speed;
        if(tempNewColor.X > 1 || tempNewColor.X < 0) colorDirection.X *= -1;
        if(tempNewColor.Y > 1 || tempNewColor.Y < 0) colorDirection.Y *= -1;
        if(tempNewColor.Z > 1 || tempNewColor.Z < 0) colorDirection.Z *= -1;

        newColor += colorDirection;
        Color c = new Color((byte)(newColor.X * 255), (byte)(newColor.Y * 255), (byte)(newColor.Z * 255));
        return c;
    }
}