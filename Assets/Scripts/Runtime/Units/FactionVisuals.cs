using UnityEngine;

namespace MonstersVsZombies.Units
{
    /// <summary>
    /// Provides the shared visual language for gameplay factions and applies it
    /// through material property blocks, so renderers remain on their shared
    /// materials and pooled objects can be recolored without material cloning.
    /// </summary>
    public static class FactionVisuals
    {
        private static readonly int s_baseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int s_colorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// Resolves the deliberately simple sandbox colors requested for quick
        /// identification: Player green, Ally blue, and Enemy red.
        /// </summary>
        public static bool TryGetColor(UnitFaction faction, out Color color)
        {
            switch (faction)
            {
                case UnitFaction.Player:
                    color = Color.green;
                    return true;
                case UnitFaction.Ally:
                    color = Color.blue;
                    return true;
                case UnitFaction.Enemy:
                    color = Color.red;
                    return true;
                default:
                    color = default;
                    return false;
            }
        }

        /// <summary>
        /// Applies a faction color to every supplied renderer without changing
        /// or instantiating the renderer's shared material asset.
        /// </summary>
        public static void Apply(
            Renderer[] renderers,
            UnitFaction faction,
            MaterialPropertyBlock propertyBlock)
        {
            if (renderers == null || propertyBlock == null ||
                !TryGetColor(faction, out Color color))
            {
                return;
            }

            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(s_baseColorId, color);
                propertyBlock.SetColor(s_colorId, color);
                targetRenderer.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }
        }
    }
}
