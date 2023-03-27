namespace PrincessRTFM.StackSellPrice;

using System;
using System.Collections.Generic;

using Dalamud.Data;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;

using Lumina.Excel.GeneratedSheets;

using XivCommon;
using XivCommon.Functions.Tooltips;

public class Plugin: IDalamudPlugin {
	private bool disposed;

	public string Name { get; } = "StackSellPrice";

	[PluginService] public static DalamudPluginInterface Interface { get; private set; } = null!;
	[PluginService] public static GameGui GameGui { get; private set; } = null!;
	[PluginService] public static DataManager GameData { get; private set; } = null!;
	public XivCommonBase Common { get; private set; } = null!;

	public Plugin() {
		this.Common = new(Hooks.Tooltips);
		this.Common.Functions.Tooltips.OnItemTooltip += this.modifyTooltip;
		PluginLog.Information("Registered tooltip construction handler!");
	}

	private void modifyTooltip(ItemTooltip tooltip, ulong itemId) {
		if (!tooltip.Fields.HasFlag(ItemTooltipFields.VendorSellPrice))
			return;

		// 0: nothing
		// 1 - 499,999: NQ
		// 500,000 - 999,999: collectibles
		// 1,000,000 - 1,499,999: HQ
		// 1,500,000 - 1,999,999: -n/a-
		// 2,000,000+: quest/event
		if (itemId is not (>= 1 and < 500_000) and not (>= 1_000_000 and < 1_500_000)) {
			//PluginLog.Information($"Item ID <{itemId}> out of range");
			return;
		}
		bool hq = false;
		if (itemId is >= 1_000_000 and < 1_500_000) {
			itemId -= 1_000_000;
			hq = true;
		}
		double price = GameData.GetExcelSheet<Item>()?.GetRow((uint)itemId)?.PriceLow ?? 0;
		if (price <= 0) {
			//PluginLog.Information($"Price <{price}> out of range");
			return;
		}
		if (hq) {
			double adjust = price / 10;
			price += (adjust - Math.Truncate(adjust)) < 0.5
				? Math.Floor(adjust)
				: Math.Ceiling(adjust);
		}
		string quantityLine = tooltip[ItemTooltipString.Quantity].TextValue;
		uint quantity;
		string[] parts = quantityLine.Split('/');
		if (parts.Length > 1) {
			if (!uint.TryParse(parts[0], out quantity)) {
				quantity = 1;
				//PluginLog.Error($"Cannot parse <{parts[0]}> as uint");
			}
		}
		else {
			quantity = 1;
			//PluginLog.Error($"Failed to split <{quantityLine}> on '/'");
		}
		if (quantity > 1) {
			List<Payload> line = new() {
				new TextPayload($"{price}{SeIconChar.Gil.ToIconString()}"),
				new UIForegroundPayload(3),
				new TextPayload($" (x{quantity:N0} = "),
				new UIForegroundPayload(0),
				new UIForegroundPayload(529),
				new TextPayload($"{price * quantity:N0}{SeIconChar.Gil.ToIconString()}"),
				new UIForegroundPayload(0),
				new UIForegroundPayload(3),
				new TextPayload(")"),
				new UIForegroundPayload(0),
			};
			tooltip[ItemTooltipString.VendorSellPrice] = new(line);
		}
	}

	#region IDisposable
	protected virtual void Dispose(bool disposing) {
		if (this.disposed)
			return;
		this.disposed = true;

		if (disposing) {
			this.Common.Functions.Tooltips.OnItemTooltip -= this.modifyTooltip;
			this.Common.Dispose();
			PluginLog.Information("Unregistered tooltip construction handler!");
		}

		PluginLog.Information("Goodbye friend :)");
	}

	public void Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}
	#endregion
}
