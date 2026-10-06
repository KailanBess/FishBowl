using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EmulatorHub
{
	// Gives fixed-size labels the lines their text needs (larger text sizes wrap onto lines that were hidden).
	// A clipped label grows, controls below it move down by the same amount, and its container and dialog grow
	// with it. Labels side by side share one shift so rows stay aligned. Nothing ever shrinks.
	public static class TextFit
	{
		public static void FitLabels(Form form)
		{
			if (form == null || form.IsDisposed) return;
			int grown = Fit(form);
			if (grown <= 0) return;
			Rectangle area = Screen.FromControl(form).WorkingArea;
			int height = Math.Min(form.ClientSize.Height + grown, area.Height - (form.Height - form.ClientSize.Height));
			if (height < form.ClientSize.Height + grown) form.AutoScroll = true;
			if (form.MinimumSize.Height > 0 && form.MinimumSize.Height < form.Height + grown) form.MinimumSize = new Size(form.MinimumSize.Width, Math.Min(form.MinimumSize.Height + grown, area.Height));
			form.ClientSize = new Size(form.ClientSize.Width, height);
			if (form.Bottom > area.Bottom) form.Top = Math.Max(area.Top, area.Bottom - form.Height);
		}

		// Fits labels inside container and returns how much taller its content became.
		private static int Fit(Control container)
		{
			int grown = 0;
			foreach (Control child in container.Controls.Cast<Control>().ToList())
			{
				if (!child.Visible || child is Label || !child.HasChildren || child is UserControl && child.Controls.Count == 0) continue;
				int inner = Fit(child);
				if (inner > 0) grown = Math.Max(grown, Grow(container, child, inner));
			}
			for (int pass = 0; pass < 50; pass++)
			{
				var clipped = container.Controls.OfType<Label>().Where(l => l.Visible && Missing(l) > 0).OrderBy(l => l.Top).ToList();
				if (clipped.Count == 0) break;
				var first = clipped[0];
				var band = clipped.Where(l => l.Top < first.Bottom).ToList();
				int shift = 0;
				foreach (var label in band) shift = Math.Max(shift, Grow(container, label, Missing(label)));
				grown = Math.Max(grown, shift);
				if (shift <= 0) break;
			}
			return grown;
		}

		// Extra height a label needs for all its lines, or 0.
		private static int Missing(Label label)
		{
			if (label.AutoSize || label.AutoEllipsis || String.IsNullOrWhiteSpace(label.Text)) return 0;
			if (label.Dock != DockStyle.None && !(label.Parent is TableLayoutPanel)) return 0; // docked labels follow their container
			int width = label.ClientSize.Width - label.Padding.Horizontal, height = label.ClientSize.Height - label.Padding.Vertical;
			if (width <= 0) return 0;
			int line = TextRenderer.MeasureText("Ag", label.Font).Height;
			int needed = TextRenderer.MeasureText(label.Text, label.Font, new Size(width, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height;
			// Ignore a few pixels: the last line is still readable.
			return needed > height + line / 3 ? needed - height : 0;
		}

		// Makes control taller by delta inside container and moves what is below it. Returns how much the container's
		// content grew.
		private static int Grow(Control container, Control control, int delta)
		{
			var table = container as TableLayoutPanel;
			if (table != null)
			{
				int row = table.GetRow(control);
				if (row >= 0 && row < table.RowStyles.Count && table.RowStyles[row].SizeType == SizeType.Absolute)
				{
					table.RowStyles[row].Height += delta;
					if (control.Dock == DockStyle.None) control.Height += delta;
					return delta;
				}
				if (control.Dock == DockStyle.None) control.Height += delta;
				return row >= 0 && row < table.RowStyles.Count && table.RowStyles[row].SizeType == SizeType.AutoSize ? delta : 0;
			}
			if (container is FlowLayoutPanel)
			{
				control.Height += delta;
				return delta;
			}
			if (control.Dock != DockStyle.None) return 0;
			int oldBottom = control.Bottom;
			control.Height += delta;
			foreach (Control sibling in container.Controls)
			{
				if (sibling == control || sibling.Dock != DockStyle.None || sibling.Top < oldBottom - 1) continue;
				// Bottom-anchored controls (dialog footers) move when the container grows.
				if ((sibling.Anchor & AnchorStyles.Bottom) != 0 && (sibling.Anchor & AnchorStyles.Top) == 0) continue;
				sibling.Top += delta;
			}
			int contentBottom = container.Controls.Cast<Control>().Where(c => c.Visible && c.Dock == DockStyle.None).Select(c => c.Bottom).DefaultIfEmpty(0).Max();
			int overflow = contentBottom - container.ClientSize.Height;
			if (overflow <= 0) return 0;
			var scroll = container as ScrollableControl;
			if (container is Form || scroll != null && scroll.AutoScroll) return container is Form ? overflow : 0;
			if (container.Dock == DockStyle.Fill || container.Dock == DockStyle.Left || container.Dock == DockStyle.Right) return 0;
			if (!container.AutoSize) container.Height += overflow;
			return overflow;
		}
	}
}
