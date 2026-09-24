using System;
using System.Drawing;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    // Opened from the panel header's right-click "Settings..." menu
    // (Plugin.GetMenuItems). There's no documented way to reuse or share state
    // with the native Track Information panel's own config dialog, so this is a
    // dedicated one, modeled after what the native dialog offers: up to 7 free
    // text rows using <Tag> placeholders, an artwork position, and a rating toggle.
    internal class ConfigForm : Form
    {
        private const int RowTop = 16;
        private const int RowHeight = 26;

        private readonly TextBox[] rowBoxes = new TextBox[PanelSettings.RowCount];
        private readonly ComboBox artworkPositionCombo;
        private readonly CheckBox ratingCheck;
        private readonly CheckBox propertiesCheck;

        public ConfigForm()
        {
            Text = "Information Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, RowTop + PanelSettings.RowCount * RowHeight + 166);

            for (int i = 0; i < PanelSettings.RowCount; i++)
            {
                int y = RowTop + i * RowHeight;

                var label = new Label
                {
                    Text = $"Row {i + 1}:",
                    AutoSize = true,
                    Location = new Point(16, y + 3),
                };

                var box = new TextBox
                {
                    Location = new Point(80, y),
                    Width = 364,
                    Text = PanelSettings.Rows[i],
                };

                rowBoxes[i] = box;
                Controls.Add(label);
                Controls.Add(box);
            }

            int hintY = RowTop + PanelSettings.RowCount * RowHeight + 4;
            var hintLabel = new Label
            {
                Text = "Tags use MusicBee's own field names, e.g. <Title> <Artist> <Album> <Album Artist> <Genre> <Full Year>",
                AutoSize = true,
                MaximumSize = new Size(428, 0),
                Location = new Point(16, hintY),
                ForeColor = SystemColors.GrayText,
            };
            Controls.Add(hintLabel);

            int artworkY = hintY + 36;
            var artworkLabel = new Label
            {
                Text = "Artwork position:",
                AutoSize = true,
                Location = new Point(16, artworkY + 3),
            };
            artworkPositionCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, artworkY),
                Width = 120,
            };
            artworkPositionCombo.Items.AddRange(Enum.GetNames(typeof(ArtworkPosition)));
            artworkPositionCombo.SelectedItem = PanelSettings.ArtworkPosition.ToString();
            Controls.Add(artworkLabel);
            Controls.Add(artworkPositionCombo);

            int ratingY = artworkY + 30;
            ratingCheck = new CheckBox
            {
                Text = "Show track rating (editable)",
                AutoSize = true,
                Location = new Point(16, ratingY),
                Checked = PanelSettings.ShowRating,
            };
            Controls.Add(ratingCheck);

            int propertiesY = ratingY + 26;
            propertiesCheck = new CheckBox
            {
                Text = "Show track properties (type, sample rate, bitrate, channels, duration)",
                AutoSize = true,
                Location = new Point(16, propertiesY),
                Checked = PanelSettings.ShowTrackProperties,
            };
            Controls.Add(propertiesCheck);

            int buttonY = propertiesY + 34;
            var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(280, buttonY) };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(368, buttonY) };
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (DialogResult == DialogResult.OK)
            {
                for (int i = 0; i < PanelSettings.RowCount; i++)
                    PanelSettings.Rows[i] = rowBoxes[i].Text;

                if (Enum.TryParse(artworkPositionCombo.SelectedItem as string, out ArtworkPosition position))
                    PanelSettings.ArtworkPosition = position;

                PanelSettings.ShowRating = ratingCheck.Checked;
                PanelSettings.ShowTrackProperties = propertiesCheck.Checked;

                PanelSettings.Save();
            }
        }
    }
}
