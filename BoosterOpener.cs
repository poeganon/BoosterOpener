using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoosterOpener;
public class BoosterOpener : BaseSettingsPlugin<BoosterOpenerSettings> {
	private SyncTask<bool> _boosterTask;
	private List<ServerInventory.InventSlotItem> _boosterPacks;
	private List<Vector2> _emptyInventorySlots;
	private RectangleF _inventoryPanelRect;

	public override bool Initialise() {
		//Perform one-time initialization here

		//Maybe load you custom config (only do so if builtin settings are inadequate for the job)
		//var configPath = Path.Join(ConfigDirectory, "custom_config.txt");
		//if (File.Exists(configPath))
		//{
		//    var data = File.ReadAllText(configPath);
		//}

		_boosterPacks = [];
		_emptyInventorySlots = [];
		_boosterTask = null;

		Settings.BoosterKey.OnValueChanged += () => Input.RegisterKey(Settings.BoosterKey);
		return true;
	}

	public override void AreaChange(AreaInstance area) {
		//Perform once-per-zone processing here
		//For example, Radar builds the zone map texture here
	}

	public override Job Tick() {
		//Perform non-render-related work here, e.g. position calculation.
		//This method is still called on every frame, so to really gain
		//an advantage over just throwing everything in the Render method
		//you have to return a custom job, but this is a bit of an advanced technique
		//here's how, just in case:
		//return new Job($"{nameof(BoosterOpener)}MainJob", () =>
		//{
		//    var a = Math.Sqrt(7);
		//});

		//otherwise, just run your code here
		//var a = Math.Sqrt(7);
		//return null;
		return new Job($"BoosterOpenerTick", () => {
			if (GameController.IngameState.IngameUi.InventoryPanel.IsVisible) {
				if (_inventoryPanelRect.IsEmpty) {
					_inventoryPanelRect = GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory].GetClientRect();
					ParseItems();
				}
			} else {
				_inventoryPanelRect = new RectangleF();
			}

			if (Settings.BoosterKey.PressedOnce()) {
				StartStopBoosterOpener();
			}

			if (_boosterTask != null) {
				ParseItems();
				if (ShouldRun()) {
					TaskUtils.RunOrRestart(ref _boosterTask, OpenBoosterAsync);
				}
				else {
					StopBoosterOpener();
				}
			}
		});
	}

	private void StartStopBoosterOpener() {
		if (Settings.DebugSettings.DebugLog) DebugWindow.LogMsg("OBR: Starting/Stopping BoosterOpener");
		if (_boosterTask == null && ShouldRun()) {
			StartBoosterOpener();
			return;
		} else if (_boosterTask != null) {
			StopBoosterOpener();
			return;
		}
		if (Settings.DebugSettings.DebugLog) DebugWindow.LogMsg("OBR: Nothing to do");
		return;
	}

	private void StartBoosterOpener() {
		if (Settings.DebugSettings.DebugLog)
			DebugWindow.LogMsg("OBR: Starting BoosterOpener Task");
		_boosterTask = OpenBoosterAsync();
	}

	private void StopBoosterOpener() {
		if (Settings.DebugSettings.DebugLog)
			DebugWindow.LogMsg("OBR: Stopping BoosterOpener Task");
		_boosterTask = null;
	}

	private bool ShouldRun() {
		return (_boosterPacks.Count > 0 && _emptyInventorySlots.Count > 0) || GameController.Game.IngameState.IngameUi.Cursor.ChildCount > 0;
	}

	private async SyncTask<bool> OpenBoosterAsync() {
		if (Settings.DebugSettings.DebugLog)
			DebugWindow.LogMsg("OBR: Running open booster");
		ExileCore.PoEMemory.MemoryObjects.Cursor cursor = GameController.Game.IngameState.IngameUi.Cursor;
		if (!ShouldRun()) {
			if (Settings.DebugSettings.DebugLog) DebugWindow.LogMsg("OBR: No Booster Packs or Empty Slots, returning false");
			return false;
		}
		if (cursor.ChildCount > 0) {
			if (Settings.DebugSettings.DebugLog)
				DebugWindow.LogMsg("OBR: Have Card");
			Input.SetCursorPos(_emptyInventorySlots[0].ToVector2Num());
			await Task.Delay(50);
			Input.Click(System.Windows.Forms.MouseButtons.Left);
			await Task.Delay(50);
		} else {
			if (Settings.DebugSettings.DebugLog)
				DebugWindow.LogMsg("OBR: Don't Have Card");
			Input.SetCursorPos(_boosterPacks[0].GetClientRect().Center.ToVector2Num());
			await Task.Delay(50);
			Input.Click(System.Windows.Forms.MouseButtons.Right);
			await Task.Delay(50);
		}
		DebugWindow.LogMsg("OBR: Done");
		return true;
	}

	public override void Render() {
		if (Settings.DebugSettings.DebugWindow) {
			ImGui.Begin("BoosterOpenerDebug");
			ImGui.Text("Current Rect is: " + _inventoryPanelRect);
			ImGui.Text("InventoryPanel is opened: " + GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory].IsVisible);
			ImGui.Text("BoosterPacks: " + _boosterPacks.Count);
			ImGui.Text("Empty Slots: " + _emptyInventorySlots.Count);
			ImGui.Text("Task: " + _boosterTask?.ToString());
			ImGui.Text("ShouldRun: " + ShouldRun());
			if (ImGui.Button("Run BoosterOpener")) {
				StartStopBoosterOpener();
			}
		}
	}

	private void ParseItems() {
		DebugWindow.LogMsg("PI: Parsing Items");
		ServerInventory inventory = GameController.Game.IngameState.ServerData.PlayerInventories[0].Inventory;
		List<Tuple<int, int>> slotsToSkip = [];
		_boosterPacks.Clear();
		_emptyInventorySlots.Clear();
		for (int x = 0 ; x < inventory.Columns ; x++) {
			for (int y = 0 ; y < inventory.Rows ; y++) {
				if (Settings.DebugSettings.DebugLog)
					DebugWindow.LogMsg("PI: Looking at " + x + ", " + y);
				if (slotsToSkip.Contains(Tuple.Create(x, y))) {
					if (Settings.DebugSettings.DebugLog)
						DebugWindow.LogMsg("PI: Skipping " + x + ", " + y + " slot");
					continue;
				}
				if (inventory[x, y] == null) {
					if (Settings.DebugSettings.DebugLog)
						DebugWindow.LogMsg("PI: Empty Slot at: " + x + ", " + y);
					Vector2 center = GetEmptySlotCenterPosition(x, y);
					if (Settings.DebugSettings.DebugLog)
						DebugWindow.LogMsg("PI: Center of empty slot is: " + center);
					_emptyInventorySlots.Add(center);
					continue;
				}
				if (inventory[x, y].Item.Path == "Metadata/Items/DivinationCards/DivinationCardDeck") {
					if (Settings.DebugSettings.DebugLog)
						DebugWindow.LogMsg("PI: Found Stacked Deck at: " + x + ", " + y);
					_boosterPacks.Add(inventory[x, y]);
					continue;
				}
				if (Settings.DebugSettings.DebugLog)
					DebugWindow.LogMsg("PI: The Fuck is this at: " + x + ", " + y);
				if (Settings.DebugSettings.DebugLog)
					DebugWindow.LogMsg(inventory[x, y].ToString());
				if (Settings.DebugSettings.DebugLog)
					DebugWindow.LogMsg("PI: Checking size of this shit: " + inventory[x, y].SizeX + ", " + inventory[x, y].SizeY);
				for (int sx = x ; sx < x + inventory[x, y].SizeX ; sx++) {
					if (Settings.DebugSettings.DebugLog)
						DebugWindow.LogMsg("PI: SizeX is : " + sx);
					for (int sy = y ; sy < y + inventory[x, y].SizeY ; sy++) {
						if (Settings.DebugSettings.DebugLog)
							DebugWindow.LogMsg("PI: SizeY is : " + sy);
						if (sx > x || sy > y) {
							if (Settings.DebugSettings.DebugLog)
								DebugWindow.LogMsg("PI: Adding to skip list: " + sx + ", " + sy);
							slotsToSkip.Add(Tuple.Create(sx, sy));
						}
					}
				}
			}
		}
	}

	private Vector2 GetEmptySlotCenterPosition(int x, int y) {
		RectangleF clientRect = GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory].GetClientRect();
		float cellsize = clientRect.Width / 12f;
		return new RectangleF(clientRect.Left + cellsize * (float)x, clientRect.Top + cellsize * (float)y, (float)x + 1 * cellsize, (float)y + 1 * cellsize).Center;
	}
}