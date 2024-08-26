using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BoosterOpener;
public class BoosterOpener : BaseSettingsPlugin<BoosterOpenerSettings> {
	private const string _coroutineName = "Open_Booster_Routine";
	private Vector2 _clickWindowOffset;
	private uint _coroutineIteration;
	private Coroutine _coroutineWorker;
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

		Input.RegisterKey(System.Windows.Forms.Keys.O);

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

			if (Settings.ProfilerHotkey.PressedOnce()) {
				if (Core.ParallelRunner.FindByName(_coroutineName) == null) {
					Core.ParallelRunner.Run(new Coroutine(OpenBoosterRoutine, null, this, _coroutineName));
				} else {
					Coroutine routine = Core.ParallelRunner.FindByName(_coroutineName);
					routine.Done();
				}
			}
		});
	}

	private async void OpenBoosterRoutine() {
		//DebugWindow.LogMsg("OBR: Running open booster");
		Cursor cursor = GameController.Game.IngameState.IngameUi.Cursor;
		//DebugWindow.LogMsg("OBR: Start loop");
		ParseItems();
		while (_boosterPacks.Count > 0 && _emptyInventorySlots.Count > 0) {
			if (cursor.ChildCount > 0) {
				//DebugWindow.LogMsg("OBR: Have Card");
				Input.SetCursorPos(_emptyInventorySlots[0].ToVector2Num());
				await Task.Delay(50);
				Input.Click(System.Windows.Forms.MouseButtons.Left);
				await Task.Delay(50);
			} else {
				//DebugWindow.LogMsg("OBR: Don't Have Card");
				Input.SetCursorPos(_boosterPacks[0].GetClientRect().Center.ToVector2Num());
				await Task.Delay(50);
				Input.Click(System.Windows.Forms.MouseButtons.Right);
				await Task.Delay(50);
			}
			//DebugWindow.LogMsg("OBR: Update list");
			await Task.Delay(150);
			ParseItems();
		}
		if (cursor.ChildCount > 0) {
			//DebugWindow.LogMsg("OBR: Have Card");
			Input.SetCursorPos(_emptyInventorySlots[0].ToVector2Num());
			await Task.Delay(50);
			Input.Click(System.Windows.Forms.MouseButtons.Left);
			await Task.Delay(50);
		}
		DebugWindow.LogMsg("OBR: Done");
	}

	public override void Render() {
		//Any Imgui or Graphics calls go here. This is called after Tick
		if (GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible && !_inventoryPanelRect.IsEmpty && false) {
			ImGui.SetNextWindowPos(new Vector2(_inventoryPanelRect.Left - 150, _inventoryPanelRect.Top).ToVector2Num());
			ImGui.SetNextWindowSize(new Vector2(150, 100).ToVector2Num());
			ImGui.Begin("BoosterOpenerWindow",
				ImGuiWindowFlags.NoTitleBar |
				ImGuiWindowFlags.NoScrollbar |
				ImGuiWindowFlags.NoResize |
				ImGuiWindowFlags.NoDocking |
				ImGuiWindowFlags.NoMove |
				ImGuiWindowFlags.NoInputs
			);
			ImGui.Text(Settings.ProfilerHotkey.Value.ToString());
			ImGui.Text("dc " + _boosterPacks.Count);
			ImGui.Text("es " + _emptyInventorySlots.Count);
			ImGui.End();
		}
		if (false) {
			ImGui.Begin("BoosterOpenerDebug");
			ImGui.Text("Current Rect is: " + _inventoryPanelRect);
			ImGui.Text("InventoryPanel is opened: " + GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory].IsVisible);
		}
	}

	private void ParseItems() {
		DebugWindow.LogMsg("PI: Parsing Items");
		ServerInventory inventory = GameController.Game.IngameState.ServerData.PlayerInventories[0].Inventory;
		List<Tuple<int, int>> slotsToSkip = [];
		_clickWindowOffset = GameController.Window.GetWindowRectangle().TopLeft;
		_boosterPacks.Clear();
		_emptyInventorySlots.Clear();
		for (int x = 0 ; x < inventory.Columns; x++) {
			for (int y = 0 ; y < inventory.Rows ; y++) {
				//DebugWindow.LogMsg("PI: Looking at " + x + ", " + y);
				if (slotsToSkip.Contains(Tuple.Create(x, y))) {
					//DebugWindow.LogMsg("PI: Skipping " + x + ", " + y + " slot");
					continue;
				}
				if (inventory[x, y] == null) {
					//DebugWindow.LogMsg("PI: Empty Slot at: " + x + ", " + y);
					Vector2 center = GetEmptySlotCenterPosition(x, y);
					//DebugWindow.LogMsg("PI: Center of empty slot is: " + center);
					_emptyInventorySlots.Add(center);
					continue;
				}
				if (inventory[x, y].Item.Path == "Metadata/Items/DivinationCards/DivinationCardDeck") {
					//DebugWindow.LogMsg("PI: Found Stacked Deck at: " + x + ", " + y);
					_boosterPacks.Add(inventory[x, y]);
					continue;
				}
				//DebugWindow.LogMsg("PI: The Fuck is this at: " + x + ", " + y);
				//DebugWindow.LogMsg(inventory[x, y].ToString());
				//DebugWindow.LogMsg("PI: Checking size of this shit: " + inventory[x, y].SizeX + ", " + inventory[x, y].SizeY);
				for (int sx = x ; sx < x + inventory[x, y].SizeX ; sx++) {
					//DebugWindow.LogMsg("PI: SizeX is : " + sx);
					for (int sy = y ; sy < y + inventory[x, y].SizeY ; sy++) {
						//DebugWindow.LogMsg("PI: SizeY is : " + sy);
						if (sx > x || sy > y) {
							//DebugWindow.LogMsg("PI: Adding to skip list: " + sx + ", " + sy);
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
		return new RectangleF(clientRect.Left + cellsize * (float)x, clientRect.Top + cellsize * (float)y, (float)x+1 * cellsize, (float)y+1 * cellsize).Center;
	}
}