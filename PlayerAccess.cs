using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ProgressionCheat
{
    /// <summary>
    /// Reaches the character's "what have I discovered" sets.
    ///
    /// Player.AddKnownItem() is the public way in, but it queues a HUD unlock popup per
    /// item - fine for one pickup, unusable for the ~500 items of a full unlock. The sets
    /// themselves (m_knownMaterial, m_knownRecipes) are private, so they are reached by
    /// reflection and filled directly.
    /// </summary>
    internal static class PlayerAccess
    {
        private static readonly FieldInfo KnownMaterialField = typeof(Player).GetField(
            "m_knownMaterial", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo KnownRecipesField = typeof(Player).GetField(
            "m_knownRecipes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly MethodInfo UpdateKnownRecipesMethod = typeof(Player).GetMethod(
            "UpdateKnownRecipesList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, new Type[0], null);

        private static readonly MethodInfo UpdatePiecesMethod = typeof(Player).GetMethod(
            "UpdateAvailablePiecesList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, new Type[0], null);

        public static bool Available
        {
            get { return KnownMaterialField != null && KnownRecipesField != null; }
        }

        private static HashSet<string> Set(FieldInfo field, Player player)
        {
            if (player == null || field == null)
                return null;
            return field.GetValue(player) as HashSet<string>;
        }

        /// <summary>
        /// Marks every item in ObjectDB as discovered, plus the recipe and build piece for
        /// each one, and refreshes the crafting and build menus.
        /// </summary>
        /// <returns>How many new names were added, or -1 if the sets could not be reached.</returns>
        public static int UnlockEverything(Player player)
        {
            HashSet<string> materials = Set(KnownMaterialField, player);
            HashSet<string> recipes = Set(KnownRecipesField, player);
            if (materials == null || recipes == null)
                return -1;

            ObjectDB odb = ObjectDB.instance;
            if (odb == null)
                return -1;

            int added = 0;

            if (odb.m_items != null)
            {
                for (int i = 0; i < odb.m_items.Count; i++)
                {
                    GameObject prefab = odb.m_items[i];
                    if (prefab == null)
                        continue;

                    ItemDrop drop = prefab.GetComponent<ItemDrop>();
                    if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                        continue;

                    ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
                    if (materials.Add(shared.m_name))
                        added++;

                    // Hammer, hoe and cultivator each carry the table of everything they
                    // can place; that is the only list of build pieces in the game.
                    added += UnlockPieces(recipes, shared.m_buildPieces);
                }
            }

            if (odb.m_recipes != null)
            {
                for (int i = 0; i < odb.m_recipes.Count; i++)
                {
                    Recipe recipe = odb.m_recipes[i];
                    if (recipe == null || recipe.m_item == null
                        || recipe.m_item.m_itemData == null || recipe.m_item.m_itemData.m_shared == null)
                        continue;

                    if (recipes.Add(recipe.m_item.m_itemData.m_shared.m_name))
                        added++;
                }
            }

            Refresh(player);
            return added;
        }

        private static int UnlockPieces(HashSet<string> recipes, PieceTable table)
        {
            if (table == null || table.m_pieces == null)
                return 0;

            int added = 0;
            for (int i = 0; i < table.m_pieces.Count; i++)
            {
                GameObject prefab = table.m_pieces[i];
                if (prefab == null)
                    continue;

                Piece piece = prefab.GetComponent<Piece>();
                if (piece == null || string.IsNullOrEmpty(piece.m_name))
                    continue;

                if (recipes.Add(piece.m_name))
                    added++;
            }
            return added;
        }

        /// <summary>
        /// Both menus cache their contents, so they have to be told the sets changed.
        /// </summary>
        private static void Refresh(Player player)
        {
            try
            {
                if (UpdateKnownRecipesMethod != null)
                    UpdateKnownRecipesMethod.Invoke(player, null);
                if (UpdatePiecesMethod != null)
                    UpdatePiecesMethod.Invoke(player, null);
            }
            catch (Exception)
            {
                // The lists rebuild themselves the next time a station is opened anyway.
            }
        }
    }
}
