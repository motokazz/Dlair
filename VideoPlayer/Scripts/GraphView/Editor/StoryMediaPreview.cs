using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

internal static class StoryMediaPreview
{
    public const int Size = 32;
    private const int MaxBindDimension = 128;
    private const int MaxRetries = 8;
    private const long RetryMs = 250;

    public static Image CreateImage()
    {
        Image image = new Image
        {
            name = "media-thumbnail",
            scaleMode = ScaleMode.ScaleToFit,
            pickingMode = PickingMode.Ignore
        };

        image.style.width = Size;
        image.style.height = Size;
        image.style.minWidth = Size;
        image.style.minHeight = Size;
        image.style.maxWidth = Size;
        image.style.maxHeight = Size;
        image.style.flexShrink = 0;
        image.style.marginLeft = 4;
        image.style.marginRight = 2;
        image.style.marginTop = 2;
        image.style.marginBottom = 2;
        image.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.35f));
        image.style.display = DisplayStyle.None;
        return image;
    }

    public static void Cancel(ref IVisualElementScheduledItem item)
    {
        item?.Pause();
        item = null;
    }

    public static IVisualElementScheduledItem Bind(Image image, Object asset)
    {
        if (image == null) return null;

        image.userData = asset;
        if (asset == null)
        {
            image.image = null;
            image.tooltip = string.Empty;
            image.style.display = DisplayStyle.None;
            return null;
        }

        image.tooltip = asset.name;
        image.style.display = DisplayStyle.Flex;
        ApplyTexture(image, AssetPreview.GetMiniThumbnail(asset));

        if (TryApplyGeneratedPreview(image, asset)) return null;

        int retries = 0;
        bool settled = false;
        return image.schedule.Execute(() =>
        {
            retries++;
            settled = TryApplyGeneratedPreview(image, asset) || retries >= MaxRetries;
        }).Every(RetryMs).Until(() => settled);
    }

    private static bool TryApplyGeneratedPreview(Image image, Object asset)
    {
        if (image == null || asset == null) return true;
        if (!ReferenceEquals(image.userData, asset)) return true;

        Texture2D preview = AssetPreview.GetAssetPreview(asset);
        if (preview == null) return false;

        ApplyTexture(image, preview);
        return true;
    }

    private static bool ApplyTexture(Image image, Texture texture)
    {
        if (image == null || texture == null) return false;
        if (texture.width > MaxBindDimension || texture.height > MaxBindDimension) return false;

        image.image = texture;
        return true;
    }
}
