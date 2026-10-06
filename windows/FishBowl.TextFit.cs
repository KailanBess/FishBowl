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
		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, object> scaled = new System.Runtime.CompilerServices.ConditionalWeakTable<Form, object>();
        // Lets the offscreen audit exercise a smaller desktop without changing the user's display.
        public static Rectangle? WorkingAreaOverride;
        private static Rectangle WorkingArea(Form form) { return WorkingAreaOverride ?? Screen.FromControl(form).WorkingArea; }

		public static void FitLabels(Form form)
		{
			if (form == null || form.IsDisposed) return;
			ScaleFixedLayout(form);
			FitContainers(form);
			FitRows(form);
			int grown = Fit(form);
            FitContainers(form);
            if (!form.TopLevel) { form.MinimumSize = Size.Empty; return; }
			if (grown <= 0) return;
			Rectangle area = WorkingArea(form);
			int height = Math.Min(form.ClientSize.Height + grown, area.Height - (form.Height - form.ClientSize.Height));
			if (height < form.ClientSize.Height + grown) form.AutoScroll = true;
			if (form.MinimumSize.Height > 0 && form.MinimumSize.Height < form.Height + grown) form.MinimumSize = new Size(form.MinimumSize.Width, Math.Min(form.MinimumSize.Height + grown, area.Height));
			form.ClientSize = new Size(form.ClientSize.Width, height);
			if (form.Bottom > area.Bottom) form.Top = Math.Max(area.Top, area.Bottom - form.Height);
		}

		// Older dialogs place every control at fixed pixel positions, so larger text sizes enlarge the fonts but not the
		// layout. Scale their layout by the text size once, keeping the dialog on screen. Table and flow layouts already adapt.
		private static void ScaleFixedLayout(Form form)
		{
			object done;
			if (NextUi.TextPercent <= 100 || scaled.TryGetValue(form, out done)) return;
			scaled.Add(form, true);
			int positioned = form.Controls.Cast<Control>().Count(c => c.Dock == DockStyle.None && !(c is TableLayoutPanel) && !(c is FlowLayoutPanel));
			if (positioned < 3 || form.Controls.Cast<Control>().Any(c => c.Dock == DockStyle.Fill && (c is TableLayoutPanel || c is FlowLayoutPanel))) return;
			Rectangle area = WorkingArea(form);
			float factor = NextUi.TextPercent / 100f;
			factor = Math.Min(factor, Math.Min(area.Width * 0.95f / Math.Max(1, form.Width), area.Height * 0.95f / Math.Max(1, form.Height)));
			if (factor <= 1.01f) return;
			// Accessibility has already set fonts. Scale coordinates without scaling text a second time.
			var fonts = Walk(form).ToDictionary(c => c, c => c.Font);
			form.Scale(new SizeF(factor, factor));
			foreach (var entry in fonts) entry.Key.Font = entry.Value;
			if (form.Right > area.Right) form.Left = Math.Max(area.Left, area.Right - form.Width);
			if (form.Bottom > area.Bottom) form.Top = Math.Max(area.Top, area.Bottom - form.Height);
		}

        private static IEnumerable<Control> Walk(Control root)
        {
            yield return root;
            foreach (Control child in root.Controls) foreach (Control item in Walk(child)) yield return item;
        }
        private static void FitContainers(Control root)
        {
            foreach (Control child in root.Controls.Cast<Control>().ToArray()) FitContainers(child);
            var flow = root as FlowLayoutPanel;
            if (flow != null) {
                foreach (ButtonBase button in flow.Controls.OfType<ButtonBase>()) button.Width = Math.Max(button.Width, Need(button));
                if (!flow.WrapContents && flow.ClientSize.Width > 0 && flow.Controls.Cast<Control>().Any(c => (c.Visible || !flow.Visible) && c.Right + c.Margin.Right > flow.ClientSize.Width - flow.Padding.Right)) {
                    flow.WrapContents = true;
                    flow.PerformLayout();
                }
                if (flow.Dock == DockStyle.Top || flow.Dock == DockStyle.Bottom || flow.Dock == DockStyle.None) {
                    int bottom = flow.Controls.Cast<Control>().Where(c => c.Visible || !flow.Visible).Select(c => c.Bottom + c.Margin.Bottom + flow.Padding.Bottom).DefaultIfEmpty(flow.Height).Max();
                    if (bottom > flow.ClientSize.Height) flow.Height += bottom - flow.ClientSize.Height;
                }
                var parentTable = flow.Parent as TableLayoutPanel;
                if (parentTable != null && flow.Dock == DockStyle.Fill) {
                    int row = parentTable.GetRow(flow);
                    int need = flow.Controls.Cast<Control>().Where(c => c.Visible || !flow.Visible).Select(c => c.Bottom + c.Margin.Bottom).DefaultIfEmpty(0).Max() + flow.Padding.Bottom + flow.Margin.Vertical;
                    if (row >= 0 && row < parentTable.RowStyles.Count && parentTable.GetRowHeights()[row] < need) {
                        int delta = need - parentTable.GetRowHeights()[row];
                        parentTable.RowStyles[row] = new RowStyle(SizeType.Absolute, need);
                        var form = parentTable.FindForm();
                        if (form != null && form.TopLevel) form.ClientSize = new Size(form.ClientSize.Width, form.ClientSize.Height + delta);
                    }
                }
            }
            if (root is SettingsDialog && NextUi.TextPercent > 100) {
                var checks = root.Controls.OfType<CheckBox>().ToList();
                var motion = checks.FirstOrDefault(c => c.Text == "Enable hover and selection motion");
                var info = checks.FirstOrDefault(c => c.Text == "Show emulator information pane");
                var icons = checks.FirstOrDefault(c => c.Text == "Show emulator icons");
                if (motion != null && info != null && (motion.Bounds.IntersectsWith(info.Bounds) || icons != null && motion.Bounds.IntersectsWith(icons.Bounds))) {
                    int oldBottom = motion.Bottom, delta = Math.Max(info.Bottom, icons == null ? 0 : icons.Bottom) + 8 - motion.Top;
                    motion.Top += delta;
                    foreach (Control other in root.Controls) if (other != motion && other.Top >= oldBottom) other.Top += delta;
                }
                var alternate = checks.FirstOrDefault(c => c.Text == "Use subtle alternating rows");
                var caption = root.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Selection contrast");
                if (alternate != null && caption != null && caption.Top < alternate.Bottom + 8) {
                    int oldTop = caption.Top, delta = alternate.Bottom + 8 - oldTop;
                    foreach (Control other in root.Controls) if (other != alternate && other.Top >= oldTop) other.Top += delta;
                }
            }
            if (root is TableLayoutPanel) {
                var table = (TableLayoutPanel)root;
                while (table.RowStyles.Count < table.RowCount) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                foreach (Control input in table.Controls) {
                    var text = input as TextBox;
                    bool singleLine = text != null && !text.Multiline;
                    if (!(table.FindForm() is GameDialog) && !(table.FindForm() is WebsiteLinkDialog)) continue;
                    if (!(input is ComboBox) && !singleLine) continue;
                    int row = table.GetRow(input), need = input.PreferredSize.Height + input.Margin.Vertical;
                    if (row >= 0 && row < table.RowStyles.Count && table.GetRowHeights()[row] < need) table.RowStyles[row] = new RowStyle(SizeType.Absolute, need);
                }
                foreach (Label label in table.Controls.OfType<Label>().Where(l => l.Text == "Filter:")) {
                    int column = table.GetColumn(label);
                    if (column >= 0 && column < table.ColumnStyles.Count) table.ColumnStyles[column] = new ColumnStyle(SizeType.Absolute, TextRenderer.MeasureText(label.Text, label.Font).Width + label.Margin.Horizontal + 8);
                }
                var owner = table.Parent as Form;
                int overflow = table.GetRowHeights().Sum() + table.Padding.Vertical - table.ClientSize.Height;
                if (owner != null && owner.TopLevel && !(owner is MainForm) && overflow > 0) owner.ClientSize = new Size(owner.ClientSize.Width, owner.ClientSize.Height + overflow);
            }
            if (root is UpdateMonitorDialog) {
                var check = root.Controls.OfType<ButtonBase>().FirstOrDefault(b => b.Text == "Check emulator releases");
                var close = root.Controls.OfType<ButtonBase>().FirstOrDefault(b => b.Text == "Close");
                if (check != null && close != null) { check.Width = Math.Max(check.Width, Need(check)); check.Left = Math.Min(check.Left, close.Left - check.Width - 8); }
            }
            // Header panels contain a title followed by an instruction. Keep their spacing at larger fonts.
            if (root is Panel && !(root is TableLayoutPanel) && !(root is FlowLayoutPanel)) {
                if (root.Dock == DockStyle.Top) foreach (Label caption in root.Controls.OfType<Label>().Where(l => l.Dock == DockStyle.Fill)) {
                    int needed = TextRenderer.MeasureText(caption.Text, caption.Font, new Size(Math.Max(1, root.ClientSize.Width - root.Padding.Horizontal - caption.Margin.Horizontal), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height;
                    root.Height = Math.Max(root.Height, needed + caption.Padding.Vertical + root.Padding.Vertical + caption.Margin.Vertical);
                }
                var labels = root.Controls.OfType<Label>().Where(l => l.Visible && l.Dock == DockStyle.None).OrderBy(l => l.Top).ToArray();
                for (int i = 1; i < labels.Length; i++) if (labels[i].Top < labels[i - 1].Bottom + 4 && labels[i].Left < labels[i - 1].Right) {
                    int oldBottom = labels[i].Bottom, delta = labels[i - 1].Bottom + 4 - labels[i].Top;
                    foreach (Control other in root.Controls) if (other != labels[i] && other.Top >= oldBottom) other.Top += delta;
                    labels[i].Top += delta;
                }
                int bottom = labels.Select(l => l.Bottom + root.Padding.Bottom).DefaultIfEmpty(0).Max();
                if (root.Dock == DockStyle.Top && bottom > root.Height) root.Height = bottom;
            }
        }

		// In fixed layouts: lay rows of buttons out again at the widths their text needs (left-aligned groups from the
		// left, right-anchored groups from the right) when the row has room, and let one-line labels that run past
		// their container wrap at its edge instead.
		private static void FitRows(Control container)
		{
			foreach (Control child in container.Controls.Cast<Control>().ToList())
				if (child.HasChildren && !(child is FlowLayoutPanel)) FitRows(child);
			if (container is FlowLayoutPanel || container is TableLayoutPanel) return;
			int edge = container.ClientSize.Width - 8;
            foreach (Label label in container.Controls.OfType<Label>().Where(l => l.Visible && l.Dock == DockStyle.None && !l.AutoSize && !l.Text.Contains(" ") && !l.Text.Contains("\n")))
                label.Width = Math.Max(label.Width, TextRenderer.MeasureText(label.Text, label.Font).Width + label.Padding.Horizontal + 4);
            if (container is Form) {
                var labels = container.Controls.OfType<Label>().Where(l => l.Visible && l.Dock == DockStyle.None).OrderBy(l => l.Top).ToList();
                for (int i = 1; i < labels.Count; i++) {
                    var previous = labels[i - 1]; var current = labels[i];
                    if (current.Top <= previous.Top + 8 || current.Top >= previous.Bottom + 4 || current.Left >= previous.Right || current.Right <= previous.Left) continue;
                    int oldBottom = current.Bottom, delta = previous.Bottom + 6 - current.Top;
                    foreach (Control sibling in container.Controls) if (sibling != current && sibling.Dock == DockStyle.None && sibling.Top >= oldBottom) sibling.Top += delta;
                    current.Top += delta;
                }
                foreach (ButtonBase button in container.Controls.OfType<ButtonBase>().Where(b => b.Visible && b.Dock == DockStyle.None && Need(b) > b.Width)) {
                    var field = container.Controls.OfType<TextBox>().FirstOrDefault(t => t.Dock == DockStyle.None && t.Left < button.Left && t.Top < button.Bottom && t.Bottom > button.Top);
                    if (field == null) continue;
                    button.Width = Need(button); button.Left = Math.Min(button.Left, edge - button.Width - 8);
                    field.Width = Math.Max(80, button.Left - field.Left - 8);
                }
            }
			foreach (Label label in container.Controls.OfType<Label>())
				if (label.Visible && label.AutoSize && label.Dock == DockStyle.None && label.Right > edge && label.Left < edge - 40)
					label.MaximumSize = new Size(edge - label.Left, 0);
			var buttons = container.Controls.OfType<ButtonBase>().Where(b => b.Visible && b.Dock == DockStyle.None && !b.AutoSize && Need(b) > b.Width).ToList();
			// Auto-sized buttons stay in their row (at their own width) so the row shifts together.
			foreach (var row in container.Controls.OfType<ButtonBase>().Where(b => b.Visible && b.Dock == DockStyle.None).GroupBy(b => b.Top / 8))
			{
				var ordered = row.OrderBy(b => b.Left).ToList();
				bool overlapping = ordered.Zip(ordered.Skip(1), (x, y) => x.Right > y.Left + 1).Any(o => o);
				if (!overlapping && !row.Any(b => buttons.Contains(b))) continue;
				var others = container.Controls.Cast<Control>().Where(c => c.Visible && !(c is ButtonBase) && c.Top < row.Max(b => b.Bottom) && c.Bottom > row.Min(b => b.Top)).ToList();
				var right = row.Where(b => (b.Anchor & AnchorStyles.Right) != 0 && (b.Anchor & AnchorStyles.Left) == 0).OrderByDescending(b => b.Right).ToList();
				var left = row.Except(right).OrderBy(b => b.Left).ToList();
				int leftLimit = right.Count > 0 ? right.Min(b => b.Left) - 8 : edge;
				foreach (var other in others) if (left.Count > 0 && other.Left > left[0].Left) leftLimit = Math.Min(leftLimit, other.Left - 8);
				Relay(left, leftLimit, false);
				int rightLimit = left.Count > 0 ? left.Max(b => b.Right) + 8 : 8;
				foreach (var other in others) if (right.Count > 0 && other.Right < right[0].Right) rightLimit = Math.Max(rightLimit, other.Right + 8);
				Relay(right, rightLimit, true);
			}
		}

		private static int Need(ButtonBase button)
		{
			var action = button as FishBowlActionButton;
			if (action != null && action.IconOnly) return button.Width;
			var own = button.GetType().GetMethod("TextWidthNeeded");
			return own != null ? (int)own.Invoke(button, null) : TextRenderer.MeasureText(button.Text, button.Font).Width + 16;
		}

		// Re-lays buttons in order with their original gaps, widened to fit, only if the whole group fits before limit.
		private static void Relay(List<ButtonBase> group, int limit, bool fromRight)
		{
			if (group.Count == 0) return;
			var widths = group.Select(b => b.AutoSize ? b.Width : Math.Max(b.Width, Need(b))).ToList();
			var gaps = new List<int>();
			for (int i = 1; i < group.Count; i++) gaps.Add(Math.Max(6, fromRight ? group[i - 1].Left - group[i].Right : group[i].Left - group[i - 1].Right));
			int total = widths.Sum() + gaps.Sum();
            if (fromRight ? group[0].Right - total < limit : group[0].Left + total > limit) {
                if (fromRight) return;
                int start = group[0].Left, top = group.Min(b => b.Top), oldBottom = group.Max(b => b.Bottom), rowHeight = group.Max(b => b.Height) + 8, wrapX = start;
                if (limit - start < widths.Max()) return;
                for (int i = 0; i < group.Count; i++) {
                    if (wrapX + widths[i] > limit && wrapX > start) { wrapX = start; top += rowHeight; }
                    group[i].SetBounds(wrapX, top, widths[i], group[i].Height); wrapX += widths[i] + 8;
                }
                int delta = group.Max(b => b.Bottom) - oldBottom;
                var parent = group[0].Parent;
                foreach (Control other in parent.Controls) if (!group.Contains(other as ButtonBase) && other.Dock == DockStyle.None && other.Top >= oldBottom && (other.Anchor & AnchorStyles.Bottom) == 0) other.Top += delta;
                return;
            }
			int x = fromRight ? group[0].Right : group[0].Left;
			for (int i = 0; i < group.Count; i++)
			{
				if (fromRight) { group[i].SetBounds(x - widths[i], group[i].Top, widths[i], group[i].Height); x -= widths[i] + (i < gaps.Count ? gaps[i] : 0); }
				else { group[i].SetBounds(x, group[i].Top, widths[i], group[i].Height); x += widths[i] + (i < gaps.Count ? gaps[i] : 0); }
			}
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
			if (label.Dock == DockStyle.Fill && !(label.Parent is TableLayoutPanel)) return 0;
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
				if (row >= 0 && row < table.RowStyles.Count && table.RowStyles[row].SizeType != SizeType.Absolute) {
                    table.RowStyles[row] = new RowStyle(SizeType.Absolute, table.GetRowHeights()[row] + delta);
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
			if (control.Dock == DockStyle.Top || control.Dock == DockStyle.Bottom) { control.Height += delta; return delta; }
			if (control.Dock == DockStyle.Fill && container is Form) return delta;
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
