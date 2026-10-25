using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FFDimAddin2020
{
    public partial class FireDimSettingsForm : Form
    {
        private ComboBox cboFilter = new ComboBox();
        private CheckBox chkAnySystem = new CheckBox();
        private TextBox txtKeywords = new TextBox();
        private CheckBox chkRun = new CheckBox();
        private CheckBox chkWall = new CheckBox();
        private CheckBox chkHide = new CheckBox();
        private NumericUpDown numOffset = new NumericUpDown();
        private NumericUpDown numWall = new NumericUpDown();
        private NumericUpDown numMinSeg = new NumericUpDown();
        private NumericUpDown numGap = new NumericUpDown();
        private ComboBox cboSide = new ComboBox();

        public FireDimSettingsForm()
        {
            Text = "Fire Dim - Settings";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 470;
            Height = 470;

            cboFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFilter.Items.AddRange(new object[] { "Fire system classification", "Name contains keyword", "All pipes in view" });
            cboSide.DropDownStyle = ComboBoxStyle.DropDownList;
            cboSide.Items.AddRange(new object[] { "Side +1", "Side -1" });

            numOffset.Maximum = 5000;
            numWall.Maximum = 20000;
            numMinSeg.Maximum = 1000;
            numGap.Maximum = 2000;

            int y = 15;
            AddRow(y, "Pipe filter mode:", cboFilter); y += 30;
            chkAnySystem.Text = "Include ANY piping system"; AddRow(y, "", chkAnySystem); y += 26;
            AddRow(y, "Keywords (comma):", txtKeywords); y += 30;
            chkRun.Text = "Run chains (fittings + wall ends)"; AddRow(y, "Sequences:", chkRun); y += 26;
            chkWall.Text = "One route-to-wall dim per route"; AddRow(y, "", chkWall); y += 26;
            chkHide.Text = "Hide witness ticks"; AddRow(y, "", chkHide); y += 30;
            AddRow(y, "Dim line offset (mm):", numOffset); y += 30;
            AddRow(y, "Max wall search (mm):", numWall); y += 30;
            AddRow(y, "Min segment (mm):", numMinSeg); y += 30;
            AddRow(y, "Max fitting gap (mm):", numGap); y += 30;
            AddRow(y, "Dimension side:", cboSide); y += 40;

            Button btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 260, Top = y, Width = 85 };
            Button btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 355, Top = y, Width = 85 };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            LoadValues();
        }

        private void AddRow(int y, string label, Control ctrl)
        {
            if (!string.IsNullOrEmpty(label))
            {
                Label l = new Label { Text = label, Left = 15, Top = y + 3, Width = 150 };
                Controls.Add(l);
            }
            ctrl.Left = 170;
            ctrl.Top = y;
            ctrl.Width = 260;
            Controls.Add(ctrl);
        }

        private void LoadValues()
        {
            cboFilter.SelectedIndex = (int)FireDimSettings.FilterMode;
            chkAnySystem.Checked = FireDimSettings.IncludeAnyPipingSystem;
            txtKeywords.Text = string.Join(", ", FireDimSettings.FireKeywords);
            chkRun.Checked = FireDimSettings.CreateRunDimensions;
            chkWall.Checked = FireDimSettings.CreateWallDimensions;
            chkHide.Checked = FireDimSettings.HideTicks;
            numOffset.Value = (decimal)FireDimSettings.DimOffsetMm;
            numWall.Value = (decimal)FireDimSettings.MaxWallSearchMm;
            numMinSeg.Value = (decimal)FireDimSettings.MinSegmentMm;
            numGap.Value = (decimal)FireDimSettings.MaxFittingGapMm;
            cboSide.SelectedIndex = FireDimSettings.DimensionSide >= 0 ? 0 : 1;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                FireDimSettings.FilterMode = (FireDimSettings.FireFilterMode)cboFilter.SelectedIndex;
                FireDimSettings.IncludeAnyPipingSystem = chkAnySystem.Checked;

                List<string> kw = new System.Collections.Generic.List<string>();
                foreach (string s in txtKeywords.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) kw.Add(s.Trim());
                FireDimSettings.FireKeywords = kw.ToArray();

                FireDimSettings.CreateRunDimensions = chkRun.Checked;
                FireDimSettings.CreateWallDimensions = chkWall.Checked;
                FireDimSettings.HideTicks = chkHide.Checked;
                FireDimSettings.DimOffsetMm = (double)numOffset.Value;
                FireDimSettings.MaxWallSearchMm = (double)numWall.Value;
                FireDimSettings.MinSegmentMm = (double)numMinSeg.Value;
                FireDimSettings.MaxFittingGapMm = (double)numGap.Value;
                FireDimSettings.DimensionSide = cboSide.SelectedIndex == 0 ? 1 : -1;

                SettingsStore.Save();
            }
            base.OnFormClosed(e);
        }
    }
}