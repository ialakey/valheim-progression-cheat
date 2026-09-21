using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProgressionCheat
{
    /// <summary>
    /// IMGUI window: pick individual items to grant, and set each skill by hand.
    /// </summary>
    internal class CheatMenu
    {
        private const int WindowId = 0x5A17;
        private const string ResourcePreset = "Ресурсы";
        private const string AllTypes = "Все типы";

        private readonly ProgressionCheatPlugin _plugin;

        private Rect _window = new Rect(60f, 60f, 940f, 660f);
        private int _tab;
        private GUIStyle _windowStyle;
        private readonly string[] _tabs = new string[] { "Предметы", "Инвентарь", "Читы", "Навыки" };

        // --- items ---
        private class ItemEntry
        {
            public GameObject Prefab;
            public string DisplayName;
            public string PrefabName;
            public string SharedName;
            public ItemDrop.ItemData.ItemType Type;
            public int MaxStack;
            public bool QuestItem;
            public string Dlc;

            /// <summary>
            /// Items the game keeps out of normal reach: quest handouts that only exist
            /// inside a scripted errand, and items belonging to content this install
            /// does not have.
            /// </summary>
            public bool Unavailable
            {
                get { return QuestItem || !string.IsNullOrEmpty(Dlc); }
            }
        }

        private readonly List<ItemEntry> _allItems = new List<ItemEntry>();
        private readonly List<ItemEntry> _shownItems = new List<ItemEntry>();
        private string[] _categories = new string[] { ResourcePreset, AllTypes };
        private int _category;
        private string _search = "";
        private bool _knownOnly = true;
        private bool _showUnavailable;
        private string _amountText = "50";
        private Vector2 _itemScroll;
        private bool _itemsDirty = true;
        private float _nextAutoRefresh;
        private string _status = "";

        // The resource preset - what the mod used to grant in bulk.
        private static readonly ItemDrop.ItemData.ItemType[] ResourceTypes = new ItemDrop.ItemData.ItemType[]
        {
            ItemDrop.ItemData.ItemType.Material,
            ItemDrop.ItemData.ItemType.Consumable,
            ItemDrop.ItemData.ItemType.Ammo,
            ItemDrop.ItemData.ItemType.AmmoNonEquipable
        };

        // --- inventory ---
        private readonly List<ItemDrop.ItemData> _invItems = new List<ItemDrop.ItemData>();
        private string _invSearch = "";
        private Vector2 _invScroll;
        private bool _invDirty = true;

        // Removing rows while the list is being laid out desyncs IMGUI, so a click only
        // records what to do and the work happens once the scroll view is closed.
        private ItemDrop.ItemData _pendingRemove;
        private int _pendingRemoveAmount;

        // "Delete everything" asks twice: the first click arms the button, a second one
        // within a few seconds goes through.
        private float _wipeArmedUntil;
        private int _wipeArmedKind;

        // --- skills ---
        private Vector2 _skillScroll;
        private readonly Dictionary<Skills.SkillType, string> _skillEdit = new Dictionary<Skills.SkillType, string>();
        private string _setAllText = "50";

        public CheatMenu(ProgressionCheatPlugin plugin)
        {
            _plugin = plugin;
            _knownOnly = plugin.CfgKnownOnly.Value;
            _showUnavailable = plugin.CfgShowUnavailable.Value;
            _amountText = plugin.CfgDefaultAmount.Value.ToString();
        }

        public void OnOpen()
        {
            _itemsDirty = true;
            _invDirty = true;
            _wipeArmedKind = 0;
            _status = "";
        }

        public void Draw()
        {
            EnsureStyles();
            _window = GUILayout.Window(WindowId, _window, DrawWindow,
                "Progression Cheat " + ProgressionCheatPlugin.PluginVersion, _windowStyle);
        }

        /// <summary>
        /// The stock IMGUI window skin is mostly transparent, which is unreadable on top
        /// of the game. Swap in a solid background with a thin border.
        /// </summary>
        private void EnsureStyles()
        {
            if (_windowStyle != null && _windowStyle.normal.background != null)
                return;

            Texture2D background = MakeFrameTexture(
                new Color(0.09f, 0.10f, 0.12f, 0.98f),
                new Color(0.45f, 0.40f, 0.30f, 1f));

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = background;
            _windowStyle.onNormal.background = background;
            _windowStyle.normal.textColor = Color.white;
            _windowStyle.onNormal.textColor = Color.white;
            _windowStyle.border = new RectOffset(3, 3, 3, 3);
        }

        private static Texture2D MakeFrameTexture(Color fill, Color edge)
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool border = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    texture.SetPixel(x, y, border ? edge : fill);
                }
            }
            texture.Apply();
            return texture;
        }

        private void DrawWindow(int id)
        {
            GUI.contentColor = Color.white;

            GUILayout.BeginHorizontal();
            _tab = GUILayout.Toolbar(_tab, _tabs, GUILayout.Height(26f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрыть", GUILayout.Width(90f), GUILayout.Height(26f)))
                _plugin.SetMenuOpen(false);
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            if (Player.m_localPlayer == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Персонаж не в мире. Зайди в игру, потом открой меню снова.");
                GUILayout.FlexibleSpace();
            }
            else if (_tab == 0)
            {
                DrawItemsTab();
            }
            else if (_tab == 1)
            {
                DrawInventoryTab();
            }
            else if (_tab == 2)
            {
                DrawCheatsTab();
            }
            else
            {
                DrawSkillsTab();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Space(2f);
                GUILayout.Label(_status);
            }

            GUI.DragWindow(new Rect(0f, 0f, 100000f, 22f));
        }

        // ------------------------------------------------------------------
        // items
        // ------------------------------------------------------------------

        private void DrawItemsTab()
        {
            EnsureItemsLoaded();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Поиск:", GUILayout.Width(52f));
            string search = GUILayout.TextField(_search, GUILayout.Width(220f));
            if (search != _search)
            {
                _search = search;
                _itemsDirty = true;
            }
            if (GUILayout.Button("X", GUILayout.Width(26f)) && _search.Length > 0)
            {
                _search = "";
                _itemsDirty = true;
            }

            GUILayout.Space(12f);

            bool knownOnly = GUILayout.Toggle(_knownOnly, " Только открытое мной", GUILayout.Width(180f));
            if (knownOnly != _knownOnly)
            {
                _knownOnly = knownOnly;
                _itemsDirty = true;
            }

            bool showUnavailable = GUILayout.Toggle(_showUnavailable, " Показывать недоступные", GUILayout.Width(200f));
            if (showUnavailable != _showUnavailable)
            {
                _showUnavailable = showUnavailable;
                _plugin.CfgShowUnavailable.Value = showUnavailable;
                _itemsDirty = true;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Кол-во:", GUILayout.Width(60f));
            _amountText = GUILayout.TextField(_amountText, 5, GUILayout.Width(50f));
            if (GUILayout.Button("-10", GUILayout.Width(40f)))
                _amountText = Mathf.Max(1, ReadAmount() - 10).ToString();
            if (GUILayout.Button("+10", GUILayout.Width(40f)))
                _amountText = (ReadAmount() + 10).ToString();
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            int category = GUILayout.SelectionGrid(_category, _categories, 6);
            if (category != _category)
            {
                _category = category;
                _itemsDirty = true;
            }

            GUILayout.Space(4f);

            // Rebuilding between the layout and repaint passes would change the number of
            // rows halfway through a frame, which IMGUI will not forgive.
            if (Event.current.type == EventType.Layout
                && (_itemsDirty || Time.realtimeSinceStartup > _nextAutoRefresh))
                RebuildShownItems();

            GUILayout.Label(string.Format("Показано: {0} из {1}", _shownItems.Count, _allItems.Count));

            _itemScroll = GUILayout.BeginScrollView(_itemScroll);
            for (int i = 0; i < _shownItems.Count; i++)
            {
                ItemEntry entry = _shownItems[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(entry.DisplayName, GUILayout.Width(250f));
                GUILayout.Label(entry.Type.ToString(), GUILayout.Width(150f));
                GUILayout.Label(UnavailableTag(entry), GUILayout.Width(60f));
                GUILayout.Label("стак " + entry.MaxStack, GUILayout.Width(80f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+1", GUILayout.Width(44f)))
                    Give(entry, 1);
                if (GUILayout.Button("+" + ReadAmount(), GUILayout.Width(64f)))
                    Give(entry, ReadAmount());
                if (GUILayout.Button("Стак", GUILayout.Width(56f)))
                    Give(entry, Mathf.Max(1, entry.MaxStack));
                GUILayout.EndHorizontal();
            }
            if (_shownItems.Count == 0)
                GUILayout.Label("Ничего не найдено. Сними галочку «Только открытое мной» или смени тип.");
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Обновить список", GUILayout.Width(150f)))
            {
                _allItems.Clear();
                _itemsDirty = true;
            }
            if (GUILayout.Button("Открыть всё", GUILayout.Width(130f)))
                UnlockEverything();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(string.Format("Выдать всё из списка ({0} шт. каждого)", ReadAmount()),
                    GUILayout.Width(300f)))
                GiveAllShown();
            GUILayout.EndHorizontal();
        }

        private int ReadAmount()
        {
            int amount;
            if (!int.TryParse(_amountText, out amount))
                return 1;
            return Mathf.Clamp(amount, 1, 9999);
        }

        private void EnsureItemsLoaded()
        {
            if (_allItems.Count > 0)
                return;

            ObjectDB odb = ObjectDB.instance;
            if (odb == null || odb.m_items == null)
                return;

            HashSet<string> typeNames = new HashSet<string>();
            List<string> orderedTypes = new List<string>();

            List<GameObject> prefabs = odb.m_items;
            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null)
                    continue;

                ItemDrop drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                    continue;

                ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;

                ItemEntry entry = new ItemEntry();
                entry.Prefab = prefab;
                entry.PrefabName = prefab.name;
                entry.SharedName = shared.m_name;
                entry.Type = shared.m_itemType;
                entry.MaxStack = shared.m_maxStackSize > 0 ? shared.m_maxStackSize : 1;
                entry.QuestItem = shared.m_questItem;
                entry.Dlc = shared.m_dlc;
                entry.DisplayName = Translate(shared.m_name, prefab.name);
                _allItems.Add(entry);

                string typeName = shared.m_itemType.ToString();
                if (typeNames.Add(typeName))
                    orderedTypes.Add(typeName);
            }

            _allItems.Sort(delegate(ItemEntry a, ItemEntry b)
            {
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase);
            });

            orderedTypes.Sort(StringComparer.OrdinalIgnoreCase);
            List<string> categories = new List<string>();
            categories.Add(ResourcePreset);
            categories.Add(AllTypes);
            categories.AddRange(orderedTypes);
            _categories = categories.ToArray();

            _itemsDirty = true;
            _plugin.Logger.LogInfo(string.Format("Item list built: {0} items, {1} types",
                _allItems.Count, orderedTypes.Count));
        }

        private static string Translate(string token, string fallback)
        {
            try
            {
                Localization localization = Localization.instance;
                if (localization != null)
                {
                    string translated = localization.Localize(token);
                    if (!string.IsNullOrEmpty(translated))
                        return translated;
                }
            }
            catch (Exception)
            {
                // Localisation is not critical - fall through to the prefab name.
            }
            return fallback;
        }

        private void RebuildShownItems()
        {
            _itemsDirty = false;
            _nextAutoRefresh = Time.realtimeSinceStartup + 1f;
            _shownItems.Clear();

            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            string needle = _search.Trim();
            bool hasNeedle = needle.Length > 0;
            string categoryName = _category < _categories.Length ? _categories[_category] : AllTypes;
            bool preset = categoryName == ResourcePreset;
            bool anyType = categoryName == AllTypes;

            for (int i = 0; i < _allItems.Count; i++)
            {
                ItemEntry entry = _allItems[i];

                if (entry.Unavailable && !_showUnavailable)
                    continue;

                if (preset)
                {
                    if (Array.IndexOf(ResourceTypes, entry.Type) < 0)
                        continue;
                }
                else if (!anyType && entry.Type.ToString() != categoryName)
                {
                    continue;
                }

                // m_knownMaterial holds the shared name of every item this character has ever picked up.
                if (_knownOnly && !player.IsKnownMaterial(entry.SharedName))
                    continue;

                if (hasNeedle
                    && entry.DisplayName.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) < 0
                    && entry.PrefabName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                _shownItems.Add(entry);
            }
        }

        private void Give(ItemEntry entry, int amount)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            Inventory inventory = player.GetInventory();
            bool dropIfFull = _plugin.CfgDropIfFull.Value;

            if (!dropIfFull && !inventory.CanAddItem(entry.Prefab, amount))
            {
                _status = string.Format("Нет места в инвентаре для «{0}»", entry.DisplayName);
                return;
            }

            ItemDrop.ItemData added = inventory.AddItem(entry.PrefabName, amount, 1, 0, 0L, "",
                new Vector2i(-1, -1), _plugin.CfgMarkCheated.Value, false, dropIfFull);

            _status = added != null
                ? string.Format("Выдано: {0} x{1}", entry.DisplayName, amount)
                : string.Format("Не удалось выдать «{0}»", entry.DisplayName);
        }

        private void GiveAllShown()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            Inventory inventory = player.GetInventory();
            bool dropIfFull = _plugin.CfgDropIfFull.Value;
            int amount = ReadAmount();
            int granted = 0;
            int noRoom = 0;

            for (int i = 0; i < _shownItems.Count; i++)
            {
                ItemEntry entry = _shownItems[i];
                int give = Mathf.Min(amount, entry.MaxStack);

                if (!dropIfFull && !inventory.CanAddItem(entry.Prefab, give))
                {
                    noRoom++;
                    continue;
                }

                ItemDrop.ItemData added = inventory.AddItem(entry.PrefabName, give, 1, 0, 0L, "",
                    new Vector2i(-1, -1), _plugin.CfgMarkCheated.Value, false, dropIfFull);
                if (added != null)
                    granted++;
                else
                    noRoom++;
            }

            _status = string.Format("Выдано стаков: {0}", granted);
            if (noRoom > 0)
                _status += string.Format(", пропущено из-за места: {0}", noRoom);
        }

        private static string UnavailableTag(ItemEntry entry)
        {
            if (entry.QuestItem)
                return "квест";
            if (!string.IsNullOrEmpty(entry.Dlc))
                return "DLC";
            return "";
        }

        /// <summary>
        /// Marks every item in the game as discovered, so the list stops hiding things
        /// behind the "known only" filter and the crafting and build menus show the lot.
        /// </summary>
        private void UnlockEverything()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            int added = PlayerAccess.UnlockEverything(player);
            if (added < 0)
            {
                _status = "Не удалось открыть предметы: игра изменилась, нужна новая сборка плагина.";
                return;
            }

            _status = string.Format(
                "Открыто нового: {0}. Теперь всё видно и в списке, и в верстаке.", added);
            _itemsDirty = true;
        }

        // ------------------------------------------------------------------
        // inventory
        // ------------------------------------------------------------------

        private void DrawInventoryTab()
        {
            Player player = Player.m_localPlayer;
            Inventory inventory = player.GetInventory();
            if (inventory == null)
            {
                GUILayout.Label("Инвентарь недоступен.");
                return;
            }

            if (Event.current.type == EventType.Layout && _invDirty)
                RebuildInventoryList(inventory);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Поиск:", GUILayout.Width(52f));
            string search = GUILayout.TextField(_invSearch, GUILayout.Width(220f));
            if (search != _invSearch)
            {
                _invSearch = search;
                _invDirty = true;
            }
            if (GUILayout.Button("X", GUILayout.Width(26f)) && _invSearch.Length > 0)
            {
                _invSearch = "";
                _invDirty = true;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Кол-во:", GUILayout.Width(60f));
            _amountText = GUILayout.TextField(_amountText, 5, GUILayout.Width(50f));
            if (GUILayout.Button("-10", GUILayout.Width(40f)))
                _amountText = Mathf.Max(1, ReadAmount() - 10).ToString();
            if (GUILayout.Button("+10", GUILayout.Width(40f)))
                _amountText = (ReadAmount() + 10).ToString();
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label(string.Format("Занято слотов: {0} из {1}",
                inventory.NrOfItems(), inventory.GetWidth() * inventory.GetHeight()));

            _invScroll = GUILayout.BeginScrollView(_invScroll);
            for (int i = 0; i < _invItems.Count; i++)
            {
                ItemDrop.ItemData item = _invItems[i];
                if (item == null || item.m_shared == null)
                    continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label(ItemName(item), GUILayout.Width(250f));
                GUILayout.Label(item.m_shared.m_itemType.ToString(), GUILayout.Width(150f));
                GUILayout.Label("x" + item.m_stack, GUILayout.Width(70f));
                GUILayout.Label(player.IsItemEquiped(item) ? "надет" : "", GUILayout.Width(60f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("-1", GUILayout.Width(44f)))
                    QueueRemove(item, 1);
                if (GUILayout.Button("-" + ReadAmount(), GUILayout.Width(64f)))
                    QueueRemove(item, ReadAmount());
                if (GUILayout.Button("Всё", GUILayout.Width(56f)))
                    QueueRemove(item, 0);
                GUILayout.EndHorizontal();
            }
            if (_invItems.Count == 0)
                GUILayout.Label(_invSearch.Length > 0
                    ? "По этому запросу в инвентаре ничего нет."
                    : "Инвентарь пуст.");
            GUILayout.EndScrollView();

            ApplyPendingRemove(player, inventory);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Обновить", GUILayout.Width(110f)))
                _invDirty = true;
            GUILayout.FlexibleSpace();
            DrawWipeButton(player, inventory, 1, "Удалить всё, кроме надетого", 250f);
            DrawWipeButton(player, inventory, 2, "Удалить всё", 170f);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Both wipe buttons ask twice. The first click arms one for a few seconds and
        /// changes its label; only the second click empties the bag.
        /// </summary>
        private void DrawWipeButton(Player player, Inventory inventory, int kind, string label, float width)
        {
            bool armed = _wipeArmedKind == kind && Time.realtimeSinceStartup < _wipeArmedUntil;
            if (!GUILayout.Button(armed ? "Точно? Нажми ещё раз" : label, GUILayout.Width(width)))
                return;

            if (armed)
            {
                WipeInventory(player, inventory, kind == 1);
                return;
            }

            _wipeArmedKind = kind;
            _wipeArmedUntil = Time.realtimeSinceStartup + 4f;
            _status = "Нажми ту же кнопку ещё раз, чтобы подтвердить.";
        }

        private void RebuildInventoryList(Inventory inventory)
        {
            _invDirty = false;
            _invItems.Clear();

            List<ItemDrop.ItemData> items = inventory.GetAllItemsSortedByName();
            if (items == null)
                return;

            string needle = _invSearch.Trim();
            bool hasNeedle = needle.Length > 0;

            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData item = items[i];
                if (item == null || item.m_shared == null)
                    continue;

                if (hasNeedle)
                {
                    string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
                    if (ItemName(item).IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) < 0
                        && prefab.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                _invItems.Add(item);
            }
        }

        private static string ItemName(ItemDrop.ItemData item)
        {
            string token = item.m_shared.m_name;
            string fallback = item.m_dropPrefab != null ? item.m_dropPrefab.name : token;
            return Translate(token, fallback);
        }

        /// <summary>Amount 0 means the whole stack.</summary>
        private void QueueRemove(ItemDrop.ItemData item, int amount)
        {
            _pendingRemove = item;
            _pendingRemoveAmount = amount;
        }

        private void ApplyPendingRemove(Player player, Inventory inventory)
        {
            ItemDrop.ItemData item = _pendingRemove;
            if (item == null)
                return;

            int amount = _pendingRemoveAmount;
            _pendingRemove = null;
            _invDirty = true;

            if (!inventory.ContainsItem(item))
            {
                _status = "Этого предмета уже нет в инвентаре.";
                return;
            }

            string name = ItemName(item);
            int have = item.m_stack;
            int take = amount <= 0 ? have : Mathf.Min(amount, have);

            // Pulling a worn item out from under the character leaves the model and its
            // bonuses behind, so take it off first.
            if (take >= have && player.IsItemEquiped(item))
                player.UnequipItem(item, false);

            inventory.RemoveItem(item, take);
            _status = string.Format("Удалено: {0} x{1}", name, take);
        }

        private void WipeInventory(Player player, Inventory inventory, bool keepEquipped)
        {
            _wipeArmedKind = 0;
            _invDirty = true;

            // Copy first: the list handed back is the inventory's own.
            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
            int slots = 0;
            int pieces = 0;

            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData item = items[i];
                if (item == null)
                    continue;

                bool equipped = player.IsItemEquiped(item);
                if (equipped && keepEquipped)
                    continue;
                if (equipped)
                    player.UnequipItem(item, false);

                slots++;
                pieces += item.m_stack;
                inventory.RemoveItem(item);
            }

            _status = string.Format("Удалено слотов: {0} (предметов: {1})", slots, pieces);
        }

        // ------------------------------------------------------------------
        // cheats
        // ------------------------------------------------------------------

        private void DrawCheatsTab()
        {
            Player player = Player.m_localPlayer;

            GUILayout.Space(4f);
            Cheats.Immortal = CheatToggle(Cheats.Immortal, "Бессмертие",
                "Урон не проходит вообще, здоровье всегда полное.");
            Cheats.InfiniteStamina = CheatToggle(Cheats.InfiniteStamina, "Бесконечная стамина",
                "Бег, прыжки и удары ничего не стоят.");
            Cheats.InfiniteEitr = CheatToggle(Cheats.InfiniteEitr, "Бесконечный эйтр",
                "То же самое для магии.");
            Cheats.Fly = CheatToggle(Cheats.Fly, "Полёт",
                "Пробел — вверх, Left Ctrl — вниз, Shift — быстрее.");

            GUILayout.Space(10f);
            GUILayout.Label(string.Format("Здоровье {0:0}/{1:0}    Стамина {2:0}/{3:0}    Эйтр {4:0}/{5:0}",
                player.GetHealth(), player.GetMaxHealth(),
                player.GetStamina(), player.GetMaxStamina(),
                player.GetEitr(), player.GetMaxEitr()));

            GUILayout.Space(10f);
            GUILayout.Label("Галочки живут между запусками игры — кроме полёта, он всегда выключен на старте.");
            GUILayout.Label("Управление работает только с закрытым меню: закрой окно, потом лети.");
            GUILayout.Label("Выключишь полёт в воздухе — упадёшь. Без бессмертия это больно.");
            GUILayout.FlexibleSpace();
        }

        private bool CheatToggle(bool value, string label, string hint)
        {
            GUILayout.BeginHorizontal();
            bool result = GUILayout.Toggle(value, " " + label, GUILayout.Width(230f));
            GUILayout.Label(hint);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            return result;
        }

        // ------------------------------------------------------------------
        // skills
        // ------------------------------------------------------------------

        private void DrawSkillsTab()
        {
            Player player = Player.m_localPlayer;
            Skills skills = player.GetSkills();
            if (skills == null)
            {
                GUILayout.Label("Система навыков недоступна.");
                return;
            }

            if (_plugin.CfgDisableSkillCap.Value)
                skills.m_useSkillCap = false;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Поставить всем:", GUILayout.Width(130f));
            _setAllText = GUILayout.TextField(_setAllText, 4, GUILayout.Width(50f));
            if (GUILayout.Button("Применить", GUILayout.Width(110f)))
            {
                float value;
                if (float.TryParse(_setAllText, out value))
                    SetAllSkills(skills, Mathf.Clamp(value, 0f, Skills.c_MaxSkillLevel));
            }
            GUILayout.Space(16f);
            if (GUILayout.Button("Все на 0", GUILayout.Width(90f)))
                SetAllSkills(skills, 0f);
            if (GUILayout.Button("Все на 100", GUILayout.Width(100f)))
                SetAllSkills(skills, Skills.c_MaxSkillLevel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(string.Format("Лимит суммы навыков: {0}",
                skills.m_useSkillCap ? "включён" : "выключен"));
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            _skillScroll = GUILayout.BeginScrollView(_skillScroll);
            Array all = Enum.GetValues(typeof(Skills.SkillType));
            for (int i = 0; i < all.Length; i++)
            {
                Skills.SkillType type = (Skills.SkillType)all.GetValue(i);
                if (type == Skills.SkillType.None || type == Skills.SkillType.All)
                    continue;

                DrawSkillRow(skills, type);
            }
            GUILayout.EndScrollView();
        }

        private void DrawSkillRow(Skills skills, Skills.SkillType type)
        {
            float level = SkillAccess.GetLevel(skills, type);

            string buffer;
            if (!_skillEdit.TryGetValue(type, out buffer))
            {
                buffer = Mathf.RoundToInt(level).ToString();
                _skillEdit[type] = buffer;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(type.ToString(), GUILayout.Width(150f));

            string typed = GUILayout.TextField(buffer, 4, GUILayout.Width(45f));
            if (typed != buffer)
            {
                _skillEdit[type] = typed;
                float parsed;
                if (float.TryParse(typed, out parsed))
                {
                    level = Mathf.Clamp(parsed, 0f, Skills.c_MaxSkillLevel);
                    SkillAccess.SetLevel(skills, type, level);
                }
            }

            float moved = GUILayout.HorizontalSlider(level, 0f, Skills.c_MaxSkillLevel, GUILayout.Width(260f));
            if (Mathf.Abs(moved - level) > 0.01f)
            {
                level = moved;
                SkillAccess.SetLevel(skills, type, level);
                _skillEdit[type] = Mathf.RoundToInt(level).ToString();
            }

            if (GUILayout.Button("-1", GUILayout.Width(36f)))
            {
                level = Mathf.Clamp(level - 1f, 0f, Skills.c_MaxSkillLevel);
                SkillAccess.SetLevel(skills, type, level);
                _skillEdit[type] = Mathf.RoundToInt(level).ToString();
            }
            if (GUILayout.Button("+1", GUILayout.Width(36f)))
            {
                level = Mathf.Clamp(level + 1f, 0f, Skills.c_MaxSkillLevel);
                SkillAccess.SetLevel(skills, type, level);
                _skillEdit[type] = Mathf.RoundToInt(level).ToString();
            }

            GUILayout.Space(10f);
            GUILayout.Label(level.ToString("0.0"), GUILayout.Width(45f));
            GUILayout.EndHorizontal();
        }

        private void SetAllSkills(Skills skills, float value)
        {
            Array all = Enum.GetValues(typeof(Skills.SkillType));
            int changed = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Skills.SkillType type = (Skills.SkillType)all.GetValue(i);
                if (type == Skills.SkillType.None || type == Skills.SkillType.All)
                    continue;

                SkillAccess.SetLevel(skills, type, value);
                _skillEdit[type] = Mathf.RoundToInt(value).ToString();
                changed++;
            }
            _status = string.Format("Навыков изменено: {0} -> {1:0}", changed, value);
        }
    }
}
