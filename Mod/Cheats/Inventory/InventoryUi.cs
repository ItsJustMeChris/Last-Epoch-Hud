#nullable disable
using System;
using System.Collections;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace Mod.Cheats.Inventory
{
    // Footer buttons (STASH / STASH ALL / VENDOR) injected after the game's
    // Transfer and Sort — as native-skinned clones of the Sort button.
    //
    // v2 covenant with the game's UI: WE DO NOT TOUCH IT. No resizing
    // Transfer/Sort, no squeezing the footer layout, no currency-row
    // mutations (v1's TryCompactCurrency debug fossil is gone).
    internal static class InventoryUi
    {
        // The ONE in-game-verified Sort-button path (ARCHAEOLOGY truth table).
        // Speculative fallbacks were axed in review — they anticipate nothing,
        // and the latched warning below is the honest response to a real move.
        const string SortPath =
            "Tab Contents/Items Tab/Inventory Tab Footer Base/Left_Buttons_Container/Sort";

        internal static Transform ButtonBar;   // the keep-alive target (see mod OnLateUpdate)

        static GameObject _stashBtn, _stashAllBtn, _vendorBtn;
        static bool _pathWarned;
        static bool _stashAllRunning;

        // Council A4: a scene transition cannot leave the Stash All re-entrancy guard latched.
        public static void ResetStashAllGuard() => _stashAllRunning = false;

        public static void Inject(EnableWovenEchoesTabIfRelevant panel)
        {
            try
            {
                if (panel.transform.Find("Tab Contents") == null && panel.transform.childCount == 0)
                    return;

                // Idempotent per panel instance: rebuild only when absent.
                Transform existing = FindOurButton(panel.transform);
                if (existing != null)
                {
                    ApplyVisibility();
                    return;
                }

                Transform sortTransform = panel.transform.Find(SortPath);
                if (sortTransform == null)
                {
                    if (!_pathWarned)
                    {
                        _pathWarned = true;
                        MelonLogger.Warning(
                            "inventory footer changed — footer buttons AND Quick Teleport unavailable " +
                            "(a game update likely moved the Sort button)");
                    }
                    return;
                }

                GameObject sortGO = sortTransform.gameObject;
                Transform bar = sortGO.transform.parent;

                _stashBtn = MakeFooterButton(sortGO, bar, "medick_StashBtn", "STASH",
                    () =>
                    {
                        if (UIBase.instanceExists && UIBase.instance != null)
                            UIBase.instance.openStash(true, false);
                    });

                _stashAllBtn = MakeFooterButton(sortGO, bar, "medick_StashAllBtn", "STASH ALL",
                    () =>
                    {
                        // Re-entrancy guard: two interleaved dump loops would
                        // halve the 3-frame server-safe cadence (rule #3 class).
                        if (_stashAllRunning) { Dbg.Log("Stash All already running — click ignored"); return; }
                        _stashAllRunning = true;
                        MelonCoroutines.Start(StashAllCoroutine());
                    });

                _vendorBtn = MakeFooterButton(sortGO, bar, "medick_VendorBtn", "VENDOR",
                    () =>
                    {
                        if (UIBase.instanceExists && UIBase.instance != null)
                            UIBase.instance.openShop(true);
                    });

                // Survivor from the Steam Deck saga: stops the layout group
                // inflating buttons at small viewports. The ONLY layout
                // property we set on a game object, and it's additive-safe.
                try
                {
                    var hlg = bar.GetComponent<HorizontalLayoutGroup>();
                    if (hlg != null)
                    {
                        hlg.childForceExpandWidth  = false;
                        hlg.childForceExpandHeight = false;
                    }
                }
                catch { }

                ButtonBar = bar;
                ApplyVisibility();

                // Build the teleport column with the same native template.
                TeleportMenu.Inject(panel.transform, sortGO);

                // Layout-tuning instrument: with DebugLog on, dump the panel's
                // child rects so slot positions (helmet/amulet row) become
                // exact numbers instead of screenshot estimates.
                DumpLayout(panel.transform);

                // First inventory open each session: silently prime the era
                // controllers so every teleport works without the map.
                TravelService.EnsurePrimed();

                Dbg.Log("footer buttons injected");
            }
            catch (Exception e)
            {
                MelonLogger.Error("footer injection failed: " + e);
                // Destroyed in catch: a half-built footer must not latch the
                // idempotency marker, or this panel never heals (keep-alive
                // dead, teleport menu never built).
                try
                {
                    if (_stashBtn)    UnityEngine.Object.DestroyImmediate(_stashBtn);
                    if (_stashAllBtn) UnityEngine.Object.DestroyImmediate(_stashAllBtn);
                    if (_vendorBtn)   UnityEngine.Object.DestroyImmediate(_vendorBtn);
                }
                catch { }
                _stashBtn = _stashAllBtn = _vendorBtn = null;
                ButtonBar = null;
            }
        }

        static Transform FindOurButton(Transform root)
        {
            Transform sort = root.Find(SortPath);
            return sort != null ? sort.parent.Find("medick_StashBtn") : null;
        }

        // Dbg-gated coordinate dump (3 levels) for layout symmetry work.
        static void DumpLayout(Transform root)
        {
            if (Prefs.DebugLog == null || !Prefs.DebugLog.Value) return;
            try
            {
                void Walk(Transform t, int depth)
                {
                    if (depth > 3) return;
                    var rt = t.GetComponent<RectTransform>();
                    Dbg.Log($"{new string(' ', depth * 2)}{t.name}  " +
                            $"pos={(rt != null ? rt.anchoredPosition.ToString("F0") : "?")}  " +
                            $"size={(rt != null ? rt.sizeDelta.ToString("F0") : "?")}");
                    for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), depth + 1);
                }
                Walk(root, 0);
            }
            catch { }
        }

        static GameObject MakeFooterButton(GameObject template, Transform bar, string name,
            string label, Action onClick)
        {
            GameObject go = NativeClone.Button(template, bar, name, onClick);
            NativeClone.HideIcon(go);                      // Sort's dots overlap custom text
            NativeClone.SetLabel(go, label, 8f, 13f);      // auto-size into the native shape
            NativeClone.SetLayoutSize(go, 64f, 26f);       // sized for ours only — natives untouched
            return go;
        }

        // Per-button visibility — the April wish, applied live from settings.
        public static void ApplyVisibility()
        {
            SetActiveSafe(_stashBtn,    Prefs.ShowStash.Value);
            SetActiveSafe(_stashAllBtn, Prefs.ShowStashAll.Value);
            SetActiveSafe(_vendorBtn,   Prefs.ShowVendor.Value);
            TeleportMenu.ApplyVisibility();
        }

        public static bool AnyFooterButtonShown =>
            Prefs.ShowStash.Value || Prefs.ShowStashAll.Value || Prefs.ShowVendor.Value;

        public static void KeepAlive()
        {
            if (!AnyFooterButtonShown) return;
            var bar = ButtonBar;
            try
            {
                if (bar == null) return;
                if (!bar.gameObject.activeSelf &&
                    bar.parent != null &&
                    bar.parent.gameObject.activeInHierarchy)
                {
                    bar.gameObject.SetActive(true);
                }
            }
            catch { ButtonBar = null; }
        }

        static void SetActiveSafe(GameObject go, bool on)
        {
            try { if (go != null && go.activeSelf != on) go.SetActive(on); } catch { }
        }

        // ── Stash All ─────────────────────────────────────────────
        // The flagship. Uses the game's own quick-move at KG's 3-frame
        // server-safe cadence. Respects stash affinity; when the priority
        // tab is full the game drops items in the open tab (game behavior —
        // this is a dump button, not a sorting service).

        static IEnumerator StashAllCoroutine()
        {
            // try/finally is CS1626-legal (only try/CATCH bars yields) and
            // guarantees the re-entrancy flag clears on every exit path.
            try
            {
                ItemContainersManager mgr = ItemContainersManager.Instance;
                if (mgr == null) yield break;
                ItemContainer inv = mgr.inventory;
                if (inv == null) yield break;

                if (UIBase.instanceExists && UIBase.instance != null)
                    UIBase.instance.openStash(false, false);   // items need somewhere to go

                yield return null;                              // let the stash UI settle

                (Vector2Int position, ItemDataUnpacked item)[] queued;
                try
                {
                    // Council A3: bind every queued position to the unpacked item object occupying it.
                    queued = inv.content.ToArray()
                        .Select(e => (e._Position_k__BackingField,
                            e.data?.TryCast<ItemDataUnpacked>()))
                        .ToArray();
                }
                catch (Exception e)
                {
                    MelonLogger.Warning("Stash All — could not read inventory: " + e.Message);
                    yield break;
                }

                int moved = 0;
                int failed = 0;
                int skipped = 0;
                foreach (var move in queued)
                {
                    ItemDataUnpacked currentItem = null;
                    try
                    {
                        currentItem = inv.GetEntryAt(move.position)?.data?.TryCast<ItemDataUnpacked>();
                    }
                    catch (Exception e)
                    {
                        Dbg.Log($"Stash All skipped — slot changed at {move.position}: {e.Message}");
                    }

                    // Council A3 repair: compare native pointers because IL2CPP managed wrappers are not stable across reads.
                    bool sameItem = move.item != null && currentItem != null && currentItem.Pointer == move.item.Pointer;
                    if (!sameItem)
                    {
                        skipped++;
                        if (currentItem != null)
                            Dbg.Log($"Stash All skipped — slot changed at {move.position}");
                    }
                    else
                    {
                        try
                        {
                            bool didMove = mgr.TryQuickMove(
                                ContainerID.INVENTORY, ContainerID.STASH, move.position, false, false);
                            if (didMove) moved++;
                            else
                            {
                                failed++;
                                Dbg.Log($"Stash All move failed at {move.position}: TryQuickMove returned false");
                            }
                        }
                        catch (Exception e)
                        {
                            failed++;
                            Dbg.Log($"Stash All move failed at {move.position}: {e.Message}");
                        }
                    }
                    yield return null;                          // 3-frame cadence — do not speed up;
                    yield return null;                          // this is what keeps the server calm
                    yield return null;
                }

                Dbg.Log($"Stash All complete — moved {moved} of {queued.Length}, {failed} failed, " +
                        $"{skipped} skipped — slot changed");
            }
            finally
            {
                _stashAllRunning = false;
            }
        }
    }
}
