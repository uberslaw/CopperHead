namespace CopperHead;

internal static class AppIcons
{
    private static Icon? _appIcon;

    public static Icon AppIcon => _appIcon ??= LoadAppIcon();

    private static Icon LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "copperhead.ico");
        if (File.Exists(path))
            return new Icon(path);

        // Embedded fallback if file missing next to exe
        var asm = typeof(AppIcons).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("copperhead.ico", StringComparison.OrdinalIgnoreCase));
        if (name is not null)
        {
            using var stream = asm.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException("Icon resource stream was null.");
            return new Icon(stream);
        }

        return SystemIcons.Application;
    }
}
